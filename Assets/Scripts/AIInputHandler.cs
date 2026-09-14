using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace RabbleHouse
{
    /// <summary>
    /// AI input handler — provides the same input interface as PhysicInputHandler
    /// but driven by AI logic. Replaces PlayerInput on AI-controlled characters.
    /// </summary>
    public class AIInputHandler : MonoBehaviour
    {
        // --- INPUT INTERFACE (mirrors PhysicInputHandler) ---
        public Vector2 MoveInput { get; private set; }
        public bool GrabPressed { get; private set; }
        public bool LightAttackPressed { get; private set; }
        public bool HeavyAttackPressed { get; private set; }
        public bool SprintPressed { get; private set; }

        // --- AI CONFIGURATION ---
        [Header("Detection")]
        [SerializeField] private float chaseRange = 15f;
        [SerializeField] private float grabRange = 2.5f;
        [SerializeField] private float grabSearchRadius = 5f; // wider search for retreat-to-grab

        [Header("Movement / Obstacle Avoidance")]
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private float obstacleCheckDistance = 1.5f;
        [SerializeField] private float obstacleCheckRadius = 0.45f;
        [Tooltip("How strongly the AI prefers moving toward the target over avoiding an obstacle.")]
        [Range(0f, 1f)]
        [SerializeField] private float targetDirectionWeight = 0.7f;
        [SerializeField] private float stuckTime = 0.8f;
        [SerializeField] private float minimumProgress = 0.15f;
        [SerializeField] private float avoidanceCommitTime = 1f;

        private float stuckTimer;
        private Vector3 lastMovementCheckPosition;

        private int avoidanceSide = 0;
        // -1 = left
        //  1 = right
        //  0 = no committed side

        private float avoidanceEndTime;

        [Header("Attack Range")]
        [Tooltip("Base reach used for unarmed attacks.")]
        [SerializeField] private float attackRange = 2f;
        [Tooltip("Extra padding added to physical weapon reach.")]
        [SerializeField] private float meleeRangePadding = 0.15f;
        [Tooltip("Maximum angle from the held object's forward direction at which " + "the AI considers a melee target attackable.")]
        [Range(0f, 180f)]
        [SerializeField] private float meleeAttackAngle = 100f;
        [Tooltip("Weapons with AIRangeBonus at or above this value are treated as ranged.")]
        [SerializeField] private float rangedWeaponThreshold = 2f;

        [Header("Timing")]
        [SerializeField] private float decisionInterval = 0.3f;
        [SerializeField] private float grabCooldown = 2f;
        [SerializeField] private float attackCooldown = 1.5f;

        [Header("Intercept")]
        [SerializeField] private float leadTime = 0.8f; // fixed seconds to predict ahead — keeps intercept aggressive even at close range
        [SerializeField] private float velocitySmoothing = 0.1f; // smoothing factor for target velocity history

        [Header("Unarmed vs Unarmed Behaviors")]
        [SerializeField] private float dodgeChance = 0.2f;
        [SerializeField] private float circleChance = 0.15f;
        [SerializeField] private float retreatChance = 0.1f;
        [SerializeField] private float chargeChance = 0.1f;
        [SerializeField] private float minAttackInterval = 0.8f;
        [SerializeField] private float maxAttackInterval = 2.5f;

        [Header("Unarmed vs Armed Behaviors")]
        [SerializeField] private float baitChance = 0.2f;          // sprint in, sprint out when target swings
        [SerializeField] private float backingUpChance = 0.25f;    // maintain distance from armed target
        [SerializeField] private float chargeAgainstArmedChance = 0.15f;       // sprint in with heavy punch
        [SerializeField] private float baitDistance = 3f;          // distance to approach when baiting
        [SerializeField] private float safeDistance = 4f;          // preferred distance from armed target
        [SerializeField] private float retreatGrabChance = 0.5f;   // chance to seek a nearby object while backing up
        [SerializeField] private float retreatGrabRange = 8f;      // how far an object can be to consider grabbing during retreat
        private float scheduledRetreatTime = -1f;
        private float retreatAfterChargeDelay = 0.8f;

        [Header("Armed vs Armed Behaviors")]
        [SerializeField] private float armedBaitChance = 0.2f;
        [SerializeField] private float minThrowHoldTime = 2f;      // randomized throw timing
        [SerializeField] private float maxThrowHoldTime = 5f;

        // Internal personality state
        private float nextAttackTime;
        private float circleDirection = 1f;
        private float dodgeEndTime;
        private float circleEndTime;
        private float objectGrabTime = -1f;
        private float baitEndTime;
        [SerializeField] private float chargeDuration = 1.5f; // max time to charge before giving up
        private float randomThrowHoldTime;
        private float chargeEndTime;

        // Intercept velocity smoothing — avoids jittery Hips linearVelocity
        private Vector3 lastTargetPos;
        private float lastTargetTime;
        private Vector3 smoothedVelocity = Vector3.zero;

        // --- AI STATE ---
        private enum AIBehavior { Idle, Chase, Grab, Attack, Retreat, Throw }
        private AIBehavior currentBehavior = AIBehavior.Idle;

        private enum CombatPhase { None, Dodge, Circle, Charge, BaitPhase1, BaitPhase2 }
        private CombatPhase currentPhase = CombatPhase.None;
        private float nextDecisionTime;
        private float nextGrabTime;
        private Transform targetPlayer;
        private Rigidbody targetRb;
        private PhysicCharacterController.CharacterState currentState = PhysicCharacterController.CharacterState.Idle;
        private GrabbableObject nearestGrabbableStatic;

        // Cached references
        private Rigidbody coreRb;
        private PhysicCharacterController controller;

        private void Awake()
        {
            controller = GetComponent<PhysicCharacterController>();
            coreRb = FindCoreRigidbody();
        }

        private Rigidbody FindCoreRigidbody()
        {
            Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in bodies)
                if (rb.transform.name.Contains("Hips"))
                    return rb;
            return bodies.Length > 0 ? bodies[0] : null;
        }

        public void SetDifficulty(int difficulty)
        {
            PlayerHealth health = controller.GetComponent<PlayerHealth>();
            switch (difficulty)
            {
                case 0: // Easy
                    decisionInterval = 0.8f;
                    attackCooldown = 2f;
                    health.SetMaxHealth(150);
                    break;
                case 1: // Normal
                    decisionInterval = 0.5f;
                    attackCooldown = 1.5f;
                    health.SetMaxHealth(200);
                    break;
                case 2: // Hard
                    decisionInterval = 0.5f;
                    attackCooldown = 0.8f;
                    health.SetMaxHealth(300);
                    break;
                case 3: // Expert
                    decisionInterval = 0.3f;
                    attackCooldown = 0.5f;
                    health.SetMaxHealth(400);
                    break;
            }
        }

        private void Update()
        {
            // Clear one-frame presses at start of frame
            GrabPressed = false;
            LightAttackPressed = false;
            HeavyAttackPressed = false;

            // Read current state from controller
            if (controller != null)
                currentState = controller.CurrentState;

            // Don't act while stunned or ragdoll
            if (currentState == PhysicCharacterController.CharacterState.Stunned || currentState == PhysicCharacterController.CharacterState.Ragdoll)
            {
                MoveInput = Vector2.zero;
                SprintPressed = false;
                return;
            }

            // Periodic decision-making
            if (Time.time >= nextDecisionTime)
            {
                SprintPressed = false;
                nextDecisionTime = Time.time + decisionInterval;
                MakeDecision();
            }

            // Execute current behavior
            ExecuteBehavior();
        }

        // ATTACK RANGE SYSTEM

        /// <summary> 
        /// Returns true when the currently held object should be treated as 
        /// a ranged weapon. 
        /// 
        /// This is intentionally centralized so every part of the AI uses 
        /// the same ranged/melee classification. 
        /// </summary>
        private bool IsCurrentAttackTypeRanged() 
        { 
            if (controller == null || controller.HeldObject == null) return false; 
            return controller.HeldObject.AIRangeBonus >= rangedWeaponThreshold; 
        }

        /// <summary> 
        /// Gets the physical melee reach of the currently held object 
        /// in the direction of the target. 
        /// 
        /// BoxColliders use their actual transformed corners, meaning 
        /// object rotation affects the calculated reach. 
        /// 
        /// Other collider types fall back to their world-space bounds. 
        /// </summary>
        private float GetHeldObjectPhysicalReach(Vector3 attackDirection)
        {
            if (controller == null || controller.HeldObject == null) 
                return 0f;

            GrabbableObject heldObject = controller.HeldObject;
            Collider[] colliders = heldObject.GetComponentsInChildren<Collider>(true);

            if (colliders == null || colliders.Length == 0) 
                return 0f; 
            
            attackDirection.y = 0f;

            if (attackDirection.sqrMagnitude < 0.001f) 
                attackDirection = heldObject.transform.forward;

            attackDirection.Normalize();

            float maximumReach = 0f;
            foreach (Collider collider in colliders) 
            { 
                if (collider == null || !collider.enabled) continue; 
                float reach = GetColliderReach(collider, attackDirection); 

                if (reach > maximumReach) 
                    maximumReach = reach; 
            }

            return maximumReach;
        }
        private float GetColliderReach(Collider collider, Vector3 attackDirection)
        {
            Transform objectTransform = controller.HeldObject.transform;

            // BoxCollider
            // Calculate all 8 actual world-space corners.
            BoxCollider box = collider as BoxCollider;

            if (box != null)
            {
                Vector3 halfSize = box.size * 0.5f; 
                Vector3[] localCorners = 
                { 
                    box.center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), 
                    box.center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z), 
                    box.center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z), 
                    box.center + new Vector3(-halfSize.x, halfSize.y, halfSize.z), 
                    box.center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z), 
                    box.center + new Vector3(halfSize.x, -halfSize.y, halfSize.z), 
                    box.center + new Vector3(halfSize.x, halfSize.y, -halfSize.z), 
                    box.center + new Vector3(halfSize.x, halfSize.y, halfSize.z)
                };

                float maxProjection = 0f;

                foreach (Vector3 localCorner in localCorners)
                {
                    Vector3 worldCorner = box.transform.TransformPoint(localCorner);
                    Vector3 relative = worldCorner - objectTransform.position;

                    relative.y = 0f; 
                    float projection = Vector3.Dot(relative, attackDirection); 
                    if (projection > maxProjection) maxProjection = projection;
                }
                return maxProjection;
            }

            // SphereCollider
            SphereCollider sphere = collider as SphereCollider;
            if (sphere != null)
            {
                Vector3 center = sphere.transform.TransformPoint(sphere.center);
                float scale = Mathf.Max(Mathf.Abs(sphere.transform.lossyScale.x), 
                    Mathf.Abs(sphere.transform.lossyScale.y), 
                    Mathf.Abs(sphere.transform.lossyScale.z));

                float radius = sphere.radius * scale; 
                Vector3 relative = center - objectTransform.position; 
                relative.y = 0f;
                
                return Vector3.Dot(relative, attackDirection) + radius;
            }

            // CapsuleCollider
            CapsuleCollider capsule = collider as CapsuleCollider;
            if (capsule != null)
            {
                Bounds bounds = capsule.bounds; 

                Vector3 centerRelative = bounds.center - objectTransform.position; 
                centerRelative.y = 0f; 
                Vector3 extents = bounds.extents; 
                extents.y = 0f; 
                
                return Vector3.Dot(centerRelative, attackDirection) + extents.magnitude;
            }

            // Generic fallback
            Bounds fallbackBounds = collider.bounds;
            Vector3 fallbackRelative = fallbackBounds.center - objectTransform.position; 
            fallbackRelative.y = 0f; Vector3 fallbackExtents = fallbackBounds.extents; 
            fallbackExtents.y = 0f; 
            
            return Vector3.Dot(fallbackRelative, attackDirection) + fallbackExtents.magnitude;
        }
        private float GetEffectiveAttackRange(Transform target)
        {
            if (target == null) 
                return attackRange; 
            
            if (controller == null || !controller.IsHoldingObject || controller.HeldObject == null) 
                return attackRange;

            GrabbableObject heldObject = controller.HeldObject; // Ranged weapons use their AI range bonus.
            if (IsCurrentAttackTypeRanged()) 
                return attackRange + heldObject.AIRangeBonus;

            // ------------------------------------------------------------- 
            // Melee weapon 
            // -------------------------------------------------------------
            Vector3 directionToTarget = target.position - coreRb.position;

            directionToTarget.y = 0f; 
            if (directionToTarget.sqrMagnitude < 0.001f) 
                return attackRange; directionToTarget.Normalize(); 
            
            float physicalReach = GetHeldObjectPhysicalReach(directionToTarget); 

            return attackRange + physicalReach + meleeRangePadding;
        }

        /// <summary> 
        /// Determines whether the target is physically within melee range. 
        /// 
        /// This checks: 
        /// 1. Horizontal distance. 
        /// 2. Held object's attack-facing direction.
        /// 3. Physical collider reach. 
        /// </summary>
        private bool IsTargetInMeleeRange(Transform target)
        {
            if (target == null || coreRb == null) 
                return false;

            if (controller == null || !controller.IsHoldingObject || controller.HeldObject == null) 
            { 
                return GetHorizontalDistance(coreRb.position, target.position) <= attackRange; 
            }

            if (IsCurrentAttackTypeRanged()) return false; 

            Vector3 toTarget = target.position - coreRb.position;

            toTarget.y = 0f; 
            
            float distance = toTarget.magnitude; 
            
            if (distance < 0.001f) 
                return true; 
            
            Vector3 directionToTarget = toTarget / distance;

            // ------------------------------------------------------------- 
            // Check attack direction. 
            // The held object's rotation now matters. 
            // -------------------------------------------------------------
            Vector3 objectForward = controller.HeldObject.transform.forward; 
            objectForward.y = 0f; 
            if (objectForward.sqrMagnitude > 0.001f) 
            { 
                objectForward.Normalize(); 
                float angle = Vector3.Angle(objectForward, directionToTarget); 

                if (angle > meleeAttackAngle) 
                    return false; 
            }

            // ------------------------------------------------------------- 
            // Calculate actual physical reach in this direction. 
            // -------------------------------------------------------------
            float effectiveRange = GetEffectiveAttackRange(target); 
            return distance <= effectiveRange;
        }
        private float GetHorizontalDistance(Vector3 a, Vector3 b)
        { 
            a.y = 0f; 
            b.y = 0f; 
            return Vector3.Distance(a, b);
        }

        // DECISION MAKING
        private void MakeDecision()
        {
            targetPlayer = FindNearestPlayer();

            // Only pick a new target object if we don't already have a valid one.
            // (Re-rolling every tick made the AI wander between objects.)
            if (nearestGrabbableStatic == null || nearestGrabbableStatic.IsHeld)
                nearestGrabbableStatic = FindRandomGrabbable();

            bool isHolding = controller != null && controller.IsHoldingObject;
            float distToTarget = targetPlayer != null ? Vector3.Distance(coreRb.position, targetPlayer.position) : float.MaxValue;

            // No alive targets found — idle/wander instead of attacking nothing
            if (targetPlayer == null)
            {
                currentBehavior = AIBehavior.Idle;
                MoveInput = Vector2.zero;
                SprintPressed = false;
                LightAttackPressed = false;
                HeavyAttackPressed = false;
                GrabPressed = false;
                return;
            }

            // Track object grab time and randomize throw timing (only when holding)
            if (isHolding && objectGrabTime < 0f)
            {
                objectGrabTime = Time.time;
                randomThrowHoldTime = Random.Range(minThrowHoldTime, maxThrowHoldTime);
            }

            if (isHolding)
            {
                // Armed vs armed/unarmed target — behavior selection extracted to helper below
                bool targetIsArmed = targetPlayer != null && IsTargetArmed(targetPlayer);
                float bonusRange = attackRange + (controller.HeldObject?.AIRangeBonus ?? 0f);
                if (targetIsArmed)
                {
                    // ---- ARMED AI vs ARMED TARGET ----
                    currentBehavior = AIBehavior.Attack; // HandleArmedCombat decides sub-behavior
                    return;
                }

                // Ranged weapons and melee objects can use their own physical/effective attack range.
                bool targetInRange = IsCurrentAttackTypeRanged() ? distToTarget <= GetEffectiveAttackRange(targetPlayer) : IsTargetInMeleeRange(targetPlayer);
                // ---- ARMED AI vs UNARMED TARGET ----
                if ((Time.time - objectGrabTime) > randomThrowHoldTime)
                    currentBehavior = AIBehavior.Throw;
                else if (targetInRange)
                    currentBehavior = AIBehavior.Attack;
                else if (targetPlayer != null && ((currentPhase != CombatPhase.BaitPhase1) || (currentPhase != CombatPhase.BaitPhase2)))
                    currentBehavior = AIBehavior.Chase;

                return;
            }
            else
            {
                // Reset object grab time when not holding
                objectGrabTime = -1f;

                bool targetIsArmed = targetPlayer != null && IsTargetArmed(targetPlayer);
                if (targetIsArmed && currentBehavior != AIBehavior.Grab) // If not heading for targeted object, go for unarmed combat state
                {
                    // ---- UNARMED AI vs ARMED TARGET ----
                    currentBehavior = AIBehavior.Attack; // HandleUnarmedCombat decides sub-behavior
                    return;
                }

                // ---- UNARMED AI vs UNARMED TARGET ----
                if (nearestGrabbableStatic != null && distToTarget > 1.5f)
                    currentBehavior = AIBehavior.Grab;
                else if (targetPlayer != null && distToTarget <= 1.5f)
                    currentBehavior = AIBehavior.Attack; // Only fight unarmed if really close
                else if (targetPlayer != null && ((currentPhase != CombatPhase.BaitPhase1) || (currentPhase != CombatPhase.BaitPhase2)))
                    currentBehavior = AIBehavior.Chase;
                else
                    currentBehavior = AIBehavior.Idle;
            }
        }


        // -----------------------------------------------------------------------
        //  DECISION HELPERS — armed-target behavior selection (holding an object)
        //  Extracted from MakeDecision() so the enum-state logic lives in one
        //  named place instead of inline. Returns the behavior; MakeDecision assigns it.
        // -----------------------------------------------------------------------
        private void ExecuteBehavior()
        {
            switch (currentBehavior)
            {
                case AIBehavior.Idle:
                    MoveInput = Vector2.zero;
                    break;

                case AIBehavior.Chase:
                    if (targetPlayer != null)
                    {
                        float distToTarget = Vector3.Distance(coreRb.position, targetPlayer.position);
                        bool targetIsArmed = IsTargetArmed(targetPlayer);

                        if (distToTarget > 0.5f)
                            SprintPressed = true;

                        HandleChasing(targetPlayer, distToTarget);
                    }
                    else
                        currentBehavior = AIBehavior.Idle;
                    break;

                case AIBehavior.Grab:
                    GrabbableObject grabbable = nearestGrabbableStatic;
                    if (grabbable != null)
                    {
                        float distToGrabbable = Vector3.Distance(coreRb.position, grabbable.transform.position);
                        if (distToGrabbable > 1f)
                            SprintPressed = true;
                        HandleChasing(grabbable.transform, distToGrabbable);
                        if (Time.time >= nextGrabTime && distToGrabbable < grabRange)
                        {
                            GrabPressed = true;
                            nextGrabTime = Time.time + grabCooldown;
                            objectGrabTime = Time.time;
                            randomThrowHoldTime = Random.Range(minThrowHoldTime, maxThrowHoldTime);
                            // Force transition to Chase — don't wait for MakeDecision to catch up
                            currentBehavior = AIBehavior.Chase;
                        }
                    }
                    else
                    {
                        currentBehavior = AIBehavior.Chase;
                    }
                    break;
                case AIBehavior.Attack:
                    if (targetPlayer != null)
                    {
                        float distToTarget = Vector3.Distance(coreRb.position, targetPlayer.position);
                        if (!controller.IsHoldingObject)
                        {
                            // Unarmed combat behaviors
                            HandleUnarmedCombat(targetPlayer, distToTarget);
                        }
                        else
                        {
                            // Holding object — swing when close, throw when too long
                            HandleArmedCombat(targetPlayer, distToTarget);
                        }
                    }
                    else
                    {
                        currentBehavior = AIBehavior.Idle;
                    }
                    break;

                case AIBehavior.Retreat:
                    SprintPressed = true;
                    Transform threat = FindNearestPlayer();
                    GrabbableObject retreatTarget = FindRetreatGrabbable(threat);
                    if (threat != null)
                    {
                        bool targetIsArmed = IsTargetArmed(threat);

                        // Chance to seek a random object while retreating
                        if (Random.value < retreatGrabChance && !controller.IsHoldingObject)
                        {
                            if (retreatTarget != null)
                            {
                                currentBehavior = AIBehavior.Grab;
                                break;
                            }
                        }

                        // Default: retreat straight away from threat
                        Vector3 away = (coreRb.position - threat.position).normalized;
                        MoveInput = GetSafeRetreatDirection(away);
                    }
                    else
                        currentBehavior = AIBehavior.Idle;
                    break;

                case AIBehavior.Throw:
                    if (targetPlayer != null)
                    {
                        MoveInput = Vector2.zero;
                        if (Time.time >= nextAttackTime)
                        {
                            HeavyAttackPressed = true;
                            nextAttackTime = Time.time + attackCooldown;
                            objectGrabTime = -1f;
                        }
                    }
                    else
                        currentBehavior = AIBehavior.Idle;
                    break;
            }
        }

        // -----------------------------------------------------------------------
        //  MOVEMENT HELPERS
        // -----------------------------------------------------------------------
        private void HandleChasing(Transform target, float distToTarget)
        {
            if (distToTarget < 1.5f)
                MoveToward(target.position);
            else
                MoveToward(InterceptPosition(target));
        }
        private void MoveToward(Vector3 targetPos)
        {
            if (coreRb == null) return;
            if (!controller.IsGrounded) return;

            // Sprint Handle
            bool isSprinting = controller.SprintPressed ? true : false;

            Vector3 toTarget = targetPos - coreRb.position;
            toTarget.y = 0;
            if (toTarget.sqrMagnitude < 0.01f)
            {
                MoveInput = Vector2.zero;
                ResetObstacleAvoidance();
                return;
            }

            Vector3 desiredDirection = toTarget.normalized;

            // ---------------------------------------------------------
            // Check whether the direct path is clear.
            // ---------------------------------------------------------

            if (!IsPathBlocked(desiredDirection))
            {
                // No obstacle -> go directly toward target.
                ResetObstacleAvoidance();

                MoveInput = new Vector2(
                    desiredDirection.x,
                    desiredDirection.z
                ).normalized;

                UpdateStuckDetection();
                return;
            }

            // ---------------------------------------------------------
            // Path is blocked -> find an avoidance direction.
            // ---------------------------------------------------------

            Vector3 avoidanceDirection = GetAvoidanceDirection(
                desiredDirection,
                targetPos
            );

            MoveInput = new Vector2(
                avoidanceDirection.x,
                avoidanceDirection.z
            ).normalized;

            UpdateStuckDetection();
        }

        private bool IsPathBlocked(Vector3 direction)
        {
            if (coreRb == null) return false;

            direction.y = 0;
            if (direction.sqrMagnitude < 0.001f) return false;

            direction.Normalize();

            Vector3 origin = coreRb.position + Vector3.up * 0.5f;

            return Physics.SphereCast(
                origin,
                obstacleCheckRadius,
                direction,
                out _,
                obstacleCheckDistance,
                obstacleMask,
                QueryTriggerInteraction.Ignore
            );
        }

        private Vector3 GetAvoidanceDirection(Vector3 desiredDirection, Vector3 targetPos)
        {
            desiredDirection.y = 0f;
            desiredDirection.Normalize();

            // Continue using the same side while committed
            // to navigating around the obstacle.
            if (Time.time < avoidanceEndTime && avoidanceSide != 0)
            {
                Vector3 committedDirection =
                    avoidanceSide < 0
                        ? Vector3.Cross(Vector3.up, desiredDirection)
                        : Vector3.Cross(desiredDirection, Vector3.up);

                if (!IsPathBlocked(committedDirection))
                    return committedDirection;
            }

            // Otherwise perform a fresh left/right evaluation
            Vector3 leftDirection = Quaternion.Euler(0f, -60f, 0f) * desiredDirection;

            Vector3 rightDirection = Quaternion.Euler(0f, 60f, 0f) * desiredDirection;

            bool leftBlocked = IsPathBlocked(leftDirection);
            bool rightBlocked = IsPathBlocked(rightDirection);

            // ---------------------------------------------------------
            // Both sides blocked.
            // Try moving more perpendicular to the obstacle.
            // ---------------------------------------------------------
            if (leftBlocked && rightBlocked)
            {
                Vector3 left = Quaternion.Euler(0f, -90f, 0f) * desiredDirection;

                Vector3 right = Quaternion.Euler(0f, 90f, 0f) * desiredDirection;

                bool leftSideBlocked = IsPathBlocked(left);
                bool rightSideBlocked = IsPathBlocked(right);

                if (!leftSideBlocked && !rightSideBlocked)
                {
                    return ChooseBetterSide(left, right, targetPos);
                }

                if (!leftSideBlocked) return left;
                if (!rightSideBlocked) return right;

                // Completely surrounded.
                return GetRecoveryDirection(desiredDirection);
            }

            // ---------------------------------------------------------
            // One side is blocked.
            // Take the open side.
            // ---------------------------------------------------------
            if (leftBlocked)
            {
                avoidanceSide = 1;
                avoidanceEndTime = Time.time + avoidanceCommitTime;

                return rightDirection;
            }
            if (rightBlocked)
            {
                avoidanceSide = -1;
                avoidanceEndTime = Time.time + avoidanceCommitTime;

                return leftDirection;
            }

            // ---------------------------------------------------------
            // Both sides are open.
            // Prefer the side that gets us closer to the target.
            // ---------------------------------------------------------
            return ChooseBetterSide(leftDirection, rightDirection, targetPos);
        }

        private Vector3 ChooseBetterSide(Vector3 leftDirection, Vector3 rightDirection, Vector3 targetPos)
        {
            Vector3 toTarget = targetPos - coreRb.position;

            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.001f)
                return leftDirection;

            toTarget.Normalize();

            float leftScore = Vector3.Dot(leftDirection, toTarget);

            float rightScore = Vector3.Dot(rightDirection, toTarget);

            if (leftScore > rightScore)
            {
                avoidanceSide = -1;
                avoidanceEndTime = Time.time + avoidanceCommitTime;

                return leftDirection;
            }
            else
            {
                avoidanceSide = 1;
                avoidanceEndTime = Time.time + avoidanceCommitTime;

                return rightDirection;
            }
        }

        private void UpdateStuckDetection()
        {
            if (coreRb == null) return;

            Vector3 currentPosition = coreRb.position;
            currentPosition.y = 0f;

            Vector3 previousPosition = lastMovementCheckPosition;
            previousPosition.y = 0f;

            float distanceMoved = Vector3.Distance(currentPosition, previousPosition);

            if (distanceMoved >= minimumProgress)
            {
                // AI is actually moving.
                stuckTimer = 0f;
                lastMovementCheckPosition = currentPosition;
                return;
            }

            stuckTimer += Time.deltaTime;

            if (stuckTimer >= stuckTime)
            {
                ForceAvoidanceRecovery();
            }
        }

        private void ForceAvoidanceRecovery()
        {
            stuckTimer = 0f;

            // Reverse the currently chosen side.
            if (avoidanceSide == 0)
                avoidanceSide = Random.value < 0.5f ? -1 : 1;
            else
                avoidanceSide *= -1;

            avoidanceEndTime =
                Time.time + avoidanceCommitTime;

            lastMovementCheckPosition = coreRb.position;
        }

        private Vector3 GetRecoveryDirection(Vector3 desiredDirection)
        {
            desiredDirection.y = 0f;
            desiredDirection.Normalize();

            Vector3 left = Vector3.Cross(Vector3.up, desiredDirection);

            Vector3 right = -left;

            if (avoidanceSide < 0)
            {
                if (!IsPathBlocked(left))
                    return left;

                if (!IsPathBlocked(right))
                    return right;
            }
            else
            {
                if (!IsPathBlocked(right))
                    return right;

                if (!IsPathBlocked(left))
                    return left;
            }

            // Nothing available.
            return Vector3.zero;
        }

        private void ResetObstacleAvoidance()
        {
            avoidanceSide = 0;
            stuckTimer = 0f;
        }

        /// <summary>
        /// Find nearest player within chase range
        /// </summary>
        private Transform FindNearestPlayer()
        {
            if (coreRb == null) return null;

            PhysicCharacterController[] allControllers = FindObjectsByType<PhysicCharacterController>();
            Transform nearest = null;
            float nearestDist = chaseRange;

            foreach (var ctrl in allControllers)
            {
                if (ctrl == controller) continue;

                var health = ctrl.GetComponent<PlayerHealth>();
                if (health != null && health.CurrentHealth <= 0) continue;

                Rigidbody otherRb = ctrl.CoreRigidbody;
                if (otherRb == null) continue;

                float dist = Vector3.Distance(coreRb.position, otherRb.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    // Return the Hips bone (core rigidbody) for correct position/movement targeting.
                    // IsTargetArmed uses GetComponentInParent<> so it still finds the controller on the root.
                    nearest = otherRb.transform;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find a random grabbable object within retreat range that's safe from the threat.
        /// Returns null if none are suitable. Picking randomly rather than the nearest
        /// makes multiple AIs less likely to converge on the same object.
        /// </summary>
        /// <summary>
        /// Find a random grabbable object within retreat range that's safe from the threat.
        /// Returns null if none are suitable. Picking randomly rather than the nearest
        /// makes multiple AIs less likely to converge on the same object.
        /// </summary>
        private GrabbableObject FindRetreatGrabbable(Transform threat)
        {
            if (coreRb == null) return null;

            Collider[] hits = Physics.OverlapSphere(coreRb.position + Vector3.up * 1f, retreatGrabRange, LayerMask.GetMask("Grabbable"));

            List<GrabbableObject> valid = new List<GrabbableObject>();
            foreach (var hit in hits)
            {
                var grabbable = hit.GetComponent<GrabbableObject>();
                if (grabbable == null) continue;

                // Threat-proximity filter: discard objects that are much closer to the
                // threat than to us (the threat would grab them first).
                if (threat != null)
                {
                    float distToThreat = Vector3.Distance(hit.transform.position, threat.position);
                    float distMeToThreat = Vector3.Distance(coreRb.position, threat.position);
                    if (distToThreat < distMeToThreat - 0.5f) continue;
                }

                // Held objects: only grab if close AND the holder is armed/threatening
                if (grabbable.IsHeld)
                {
                    float distToGrabbable = Vector3.Distance(coreRb.position, grabbable.transform.position);
                    float distThreatToGrabbable = threat != null ? Vector3.Distance(threat.position, grabbable.transform.position) : 0f;
                    float distMeToThreat = threat != null ? Vector3.Distance(coreRb.position, threat.position) : 0f;
                    // Only grab a held object if we are closer than the threat
                    if (distToGrabbable < 2.5f && distThreatToGrabbable > distToGrabbable - 0.5f)
                        valid.Add(grabbable);
                }
                else
                {
                    // Unheld objects are always valid at this point
                    valid.Add(grabbable);
                }
            }

            if (valid.Count == 0) return null;

            return valid[Random.Range(0, valid.Count)];
        }

        /// <summary>
        /// Find a random grabbable object in a wider radius around the AI
        /// Uses sphere overlap for 360-degree detection (not just in front)
        /// </summary>
        private GrabbableObject FindRandomGrabbable()
        {
            if (coreRb == null) return null;

            // Use sphere around the AI for wider detection — not just in front
            Collider[] hits = Physics.OverlapSphere(coreRb.position + Vector3.up * 1f, grabSearchRadius, LayerMask.GetMask("Grabbable"));

            List<GrabbableObject> valid = new List<GrabbableObject>();

            foreach (var hit in hits)
            {
                var grabbable = hit.GetComponent<GrabbableObject>();
                if (grabbable == null || grabbable.IsHeld) continue;

                valid.Add(grabbable);
            }

            if (valid.Count == 0) return null;

            // Pick one at random — more diverse behavior across multiple AIs
            return valid[Random.Range(0, valid.Count)];
        }

        /// <summary>
        /// Check if target is holding a grabbable object (armed)
        /// </summary>
        private bool IsTargetArmed(Transform target)
        {
            if (target == null) return false;
            // The target passed in may be a child bone (e.g. Hips). Climb to the
            // root GameObject where PhysicCharacterController actually lives.
            var targetController = target.GetComponentInParent<PhysicCharacterController>();
            if (targetController == null)
            {
                return false;
            }
            return targetController.IsHoldingObject;
        }

        /// <summary>
        /// Predict where the target will be and aim to cut them off.
        /// Instead of naively extrapolating current velocity (which makes the AI
        /// orbit alongside a circling player), we bias the aim point toward the
        /// inside of the player's path so the AI closes the gap across the circle.
        /// </summary>
        private Vector3 InterceptPosition(Transform target)
        {
            if (target == null) return coreRb.position;

            // Get the target's core rigidbody (may be on a child Hips bone)
            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            if (targetRb == null)
            {
                var pc = target.GetComponent<PhysicCharacterController>();
                if (pc != null) targetRb = pc.CoreRigidbody;
            }
            if (targetRb == null) return target.position;

            Vector3 targetPos = targetRb.position;
            Vector3 toTarget = targetPos - coreRb.position;
            toTarget.y = 0;
            float distance = toTarget.magnitude;
            if (distance < 0.001f) return targetPos;

            // Smooth target velocity to avoid jittery updates from Hips root
            float now = Time.time;
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, targetRb.linearVelocity, velocitySmoothing);
            lastTargetPos = targetPos;
            lastTargetTime = now;

            // Fixed lead time (seconds) instead of distance-based ETA for more predictable behavior
            float eta = leadTime;
            eta = Mathf.Clamp(eta, 0.1f, 5f);

            // Predict where target will be (more ahead)
            Vector3 predicted = targetPos + smoothedVelocity * eta;

            // Bias further toward cutting inside the circle (reduce Lerp weight from 0.5 to 0.3)
            // Lower Lerp value = aim point is closer to predicted (further ahead), making the intercept more aggressive.
            Vector3 aimPoint = Vector3.Lerp(targetPos, predicted, 0.3f);

            return aimPoint;
        }

        /// <summary>
        /// Given a desired retreat direction, check if it hits a wall.
        /// If clear, return it as-is. If blocked, try sliding left/right
        /// along the wall. If both sides are blocked, return zero (stop).
        /// </summary>
        private Vector2 GetSafeRetreatDirection(Vector3 desiredDir)
        {
            float checkDist = 2f;
            int wallMask = LayerMask.GetMask("Default", "Wall", "Environment");

            // Try the desired direction first
            if (!Physics.Raycast(coreRb.position + Vector3.up * 0.5f, desiredDir, checkDist, wallMask))
            {
                return new Vector2(desiredDir.x, desiredDir.z);
            }

            // Blocked — try perpendicular slide (left then right)
            Vector3 leftDir = Vector3.Cross(desiredDir, Vector3.up);  // perpendicular left
            Vector3 rightDir = -leftDir;                                // perpendicular right

            if (!Physics.Raycast(coreRb.position + Vector3.up * 0.5f, leftDir, checkDist, wallMask))
            {
                return new Vector2(leftDir.x, leftDir.z);
            }
            if (!Physics.Raycast(coreRb.position + Vector3.up * 0.5f, rightDir, checkDist, wallMask))
            {
                return new Vector2(rightDir.x, rightDir.z);
            }

            // Completely boxed in — stop
            return Vector2.zero;
        }

        /// <summary>
        /// Combat logic executed when the AI is unarmed.
        /// ARMED TARGET  -> only Bait / Back-Up / Charge can run (never punch/dodge/circle).
        /// UNARMED TARGET -> full personality (dodge, circle, retreat, punch).
        /// </summary>
        private void HandleUnarmedCombat(Transform target, float distToTarget)
        {
            if (controller == null || target == null) return;

            bool targetIsArmed = IsTargetArmed(target);
            // ===================================================================
            //  ARMED TARGET — ONLY these three behaviors are allowed.
            // ===================================================================
            if (targetIsArmed)
            {
                // --- Currently in an active armed-target sub-state? ----------
                // (These states persist across frames so the AI commits to one
                //  behaviour instead of re-rolling every frame.)

                // 1) BAIT: sprint in until within baitDistance, then step away
                if (currentPhase == CombatPhase.BaitPhase1 && Time.time < baitEndTime)
                {
                    // Phase A: close distance to baitDistance — sprint toward target
                    SprintPressed = true;
                    HandleChasing(target, distToTarget);
                    // Transition to Phase B once we are close enough to bait
                    if (distToTarget <= baitDistance)
                    {
                        currentPhase = CombatPhase.BaitPhase2;
                        baitEndTime = Time.time; // force Phase B next frame
                    }
                    return;
                }
                if (currentPhase == CombatPhase.BaitPhase2 && Time.time >= baitEndTime)
                {
                    // Phase 2: step away briefly after reaching bait distance
                    //SprintPressed = false;
                    Vector3 awayDir = (coreRb.position - target.position).normalized;
                    awayDir.y = 0;
                    // Wall-aware retreat: if backing into a wall, slide along it
                    MoveInput = GetSafeRetreatDirection(awayDir);
                    // Following up with a charge if step back far enough
                    if (distToTarget > baitDistance * 1.5f)
                    {
                        currentPhase = CombatPhase.Charge;
                        chargeEndTime = Time.time + chargeDuration;
                    }
                    return;
                }

                // CHARGE: sprint in and heavy punch when in range. Then fall back
                if (currentPhase == CombatPhase.Charge && Time.time < chargeEndTime)
                {
                    float bonusRange = attackRange * 2f;
                    SprintPressed = true;
                    HandleChasing(target, distToTarget);
                    if (distToTarget < bonusRange && controller.HeavyPunchReady && Time.time >= nextAttackTime)
                    {
                        HeavyAttackPressed = true;
                        nextAttackTime = Time.time + attackCooldown;
                        scheduledRetreatTime = Time.time + retreatAfterChargeDelay; // schedule retreat AFTER punch
                    }
                    if (scheduledRetreatTime > 0f && Time.time >= scheduledRetreatTime)
                    {
                        //Retreat after a set timer after heavy punch
                        scheduledRetreatTime = -1f;
                        currentPhase = CombatPhase.None;
                        currentBehavior = AIBehavior.Retreat;
                        chargeEndTime = Time.time; // force end charge
                    }
                    return;
                }

                // --- No active sub-state: pick one based on chances ----------
                if (Random.value < baitChance)
                {
                    currentPhase = CombatPhase.BaitPhase1;
                    // Phase A ends either when close enough (baitDistance) or after this max time
                    baitEndTime = Time.time + 2f;
                    return;
                }

                if (Random.value < backingUpChance)
                {
                    currentBehavior = AIBehavior.Retreat;
                    return;
                }

                if (Random.value < chargeAgainstArmedChance)
                {
                    currentPhase = CombatPhase.Charge;
                    chargeEndTime = Time.time + chargeDuration;
                    return;
                }

                // Default fallback: back up
                currentBehavior = AIBehavior.Retreat;
                return;
            }

            // ===================================================================
            //  UNARMED TARGET — full combat personality.
            // ===================================================================
            else
            {
                // Currently dodging
                if (currentPhase == CombatPhase.Dodge)
                {
                    SprintPressed = true;
                    Vector3 awayFromTarget = (coreRb.position - target.position).normalized;
                    awayFromTarget.y = 0;
                    MoveInput = new Vector2(awayFromTarget.x, awayFromTarget.z).normalized;
                    if (Time.time >= dodgeEndTime) currentPhase = CombatPhase.None;

                    return;
                }

                // Currently circling
                if (currentPhase == CombatPhase.Circle)
                {
                    SprintPressed = true;
                    Vector3 toTarget = (target.position - coreRb.position).normalized;
                    Vector3 perp = Vector3.Cross(toTarget, Vector3.up) * circleDirection;
                    MoveInput = new Vector2(perp.x, perp.z).normalized;

                    // Wall collision during circling -> reverse direction
                    if (Physics.Raycast(coreRb.position, perp.normalized, 1.5f, LayerMask.GetMask("Default", "Wall", "Environment")))
                    {
                        circleDirection *= -1f;
                    }
                    if (Time.time >= circleEndTime) currentPhase = CombatPhase.None;

                    return;
                }

                // Currently charging against unarmed — same logic as armed charge:
                // keep the charge alive through the full heavy punch duration.
                if (currentPhase == CombatPhase.Charge && Time.time < chargeEndTime)
                {
                    SprintPressed = true;
                    HandleChasing(target, distToTarget);
                    if (distToTarget < attackRange && controller.HeavyPunchReady && Time.time >= nextAttackTime)
                    {
                        HeavyAttackPressed = true;
                        nextAttackTime = Time.time + attackCooldown;
                        scheduledRetreatTime = Time.time + retreatAfterChargeDelay; // schedule retreat AFTER punch
                    }
                    if (scheduledRetreatTime > 0f && Time.time >= scheduledRetreatTime)
                    {
                        //Retreat after a set timer after heavy punch
                        scheduledRetreatTime = -1f;
                        currentPhase = CombatPhase.None;
                        currentBehavior = AIBehavior.Retreat;
                    }
                    return;
                }

                // In combat range — decide action
                MoveInput = Vector2.zero;
                SprintPressed = false;

                // Retreat?
                if (Random.value < retreatChance)
                {
                    GrabbableObject nearbyObject = nearestGrabbableStatic;
                    if (nearbyObject != null)
                        currentBehavior = AIBehavior.Grab;
                    else
                        currentBehavior = AIBehavior.Retreat;
                    return;
                }

                // Dodge?
                if (Random.value < dodgeChance)
                {
                    SprintPressed = true;
                    currentPhase = CombatPhase.Dodge;
                    dodgeEndTime = Time.time + Random.Range(0.3f, 0.6f);
                    return;
                }

                // Circle?
                if (Random.value < circleChance)
                {
                    SprintPressed = true;
                    currentPhase = CombatPhase.Circle;
                    circleDirection = Random.value > 0.5f ? 1f : -1f;
                    circleEndTime = Time.time + Random.Range(1f, 2.5f);
                    return;
                }

                // Charge?
                if (Random.value < chargeChance)
                {
                    SprintPressed = true;
                    currentPhase = CombatPhase.Charge;
                    chargeEndTime = Time.time + chargeDuration;
                    return;
                }

                // Attack (light combo / heavy)
                if (Time.time >= nextAttackTime)
                {
                    if (Random.value < 0.6f)
                    {
                        LightAttackPressed = true;
                        nextAttackTime = Time.time + Random.Range(0.2f, 0.4f);
                    }
                    else
                    {
                        HeavyAttackPressed = true;
                        nextAttackTime = Time.time + Random.Range(minAttackInterval, maxAttackInterval);
                    }
                }
            }
        }

        /// <summary>
        /// Combat logic executed when the AI is unarmed.
        /// ARMED TARGET  -> Bait.
        /// UNARMED TARGET -> Chase, Throw.
        /// </summary>
        private void HandleArmedCombat(Transform target, float distToTarget)
        {
            if (controller == null || target == null) return;

            bool targetIsArmed = IsTargetArmed(target);
            float bonusRange = attackRange + (controller.HeldObject?.AIRangeBonus ?? 0f);

            float aiRange = controller.HeldObject?.AIRangeBonus ?? 0f;
            bool isRanged = aiRange >= 2f; // guns, ranged tools — melee objects have AIRangeBonus ~0-2
            // ===================================================================
            //  ARMED TARGET — ONLY these three behaviors are allowed.
            // ===================================================================
            if (targetIsArmed)
            {
                if (isRanged)
                {
                    HandleRangedFire(target, distToTarget);
                }
                else
                {
                    // --- MELEE OBJECT: existing behavior ---
                    if (currentPhase == CombatPhase.BaitPhase1 && Time.time < baitEndTime)
                    {
                        // Phase A: close distance to baitDistance — sprint toward target
                        HandleChasing(target, distToTarget);
                        // Transition to Phase B once we are close enough to bait
                        if (distToTarget <= baitDistance * 1.5f)
                        {
                            currentPhase = CombatPhase.BaitPhase2;
                            baitEndTime = Time.time; // force Phase B next frame
                        }
                        return;
                    }
                    if (currentPhase == CombatPhase.BaitPhase2 && Time.time >= baitEndTime)
                    {
                        // Phase 2: step away briefly after reaching bait distance
                        Vector3 awayDir = (coreRb.position - target.position).normalized;
                        awayDir.y = 0;
                        // Wall-aware retreat: if backing into a wall, slide along it
                        MoveInput = GetSafeRetreatDirection(awayDir);
                        // Following up with a charge if step back far enough
                        if (distToTarget > baitDistance * 1.8f)
                        {
                            currentPhase = CombatPhase.Charge;
                            chargeEndTime = Time.time + chargeDuration;
                        }
                        return;
                    }

                    // CHARGE: close in with object swing
                    if (currentPhase == CombatPhase.Charge && Time.time < chargeEndTime)
                    {
                        HandleChasing(target, distToTarget);

                        // Check swing readiness
                        if (IsTargetInMeleeRange(target) && controller.HeavyPunchReady && Time.time >= nextAttackTime && controller.SwingReady)
                        {
                            if (controller.SwingReady)
                            {
                                LightAttackPressed = true;
                                nextAttackTime = Time.time + attackCooldown;
                                scheduledRetreatTime = Time.time + retreatAfterChargeDelay; // schedule retreat AFTER punch
                            }
                        }
                        // Swing — stop charging, retreat after hit lands
                        if (scheduledRetreatTime > 0f && Time.time >= scheduledRetreatTime)
                        {
                            //Retreat after a set timer after heavy punch
                            scheduledRetreatTime = -1f;
                            currentPhase = CombatPhase.None;
                            currentBehavior = AIBehavior.Retreat;
                        }
                        return;
                    }

                    // --- No active sub-state: pick one based on chances ----------
                    if (Random.value < armedBaitChance && controller.HeavyPunchReady)
                    {
                        currentPhase = CombatPhase.BaitPhase1;
                        // Phase A ends either when close enough (baitDistance) or after this max time
                        baitEndTime = Time.time + 2f;
                        return;
                    }

                    // Step back if swing isn't ready
                    if (!controller.SwingReady)
                    {
                        currentBehavior = AIBehavior.Retreat;
                        return;
                    }
                }
            }
            // ===================================================================
            //  UNARMED TARGET — ONLY these three behaviors are allowed.
            // ===================================================================
            if (isRanged)
            {
                HandleRangedFire(target, distToTarget);
            }
            else
            {
                bool inMeleeRange = IsTargetInMeleeRange(target);
                // --- MELEE OBJECT: existing behavior ---
                if (inMeleeRange)
                {
                    if (controller.SwingReady && Time.time >= nextAttackTime)
                    {
                        LightAttackPressed = true; // Swing
                        nextAttackTime = Time.time + Random.Range(minAttackInterval, maxAttackInterval);
                        scheduledRetreatTime = Time.time + retreatAfterChargeDelay; // schedule retreat AFTER swing
                    }
                    if (scheduledRetreatTime > 0f && Time.time >= scheduledRetreatTime)
                    {
                        //Retreat after a set timer after heavy punch
                        scheduledRetreatTime = -1f;
                        currentPhase = CombatPhase.None;
                        currentBehavior = AIBehavior.Retreat;
                    }
                    return;
                }
                else if ((Time.time - objectGrabTime) > randomThrowHoldTime)
                {
                    // Close but held too long, or far — throw it
                    if (Time.time >= nextAttackTime)
                    {
                        HeavyAttackPressed = true; // Throw
                        nextAttackTime = Time.time + attackCooldown;
                        objectGrabTime = -1f;
                        return;
                    }
                }

                // Step back if swing isn't ready
                if (!controller.HeavyPunchReady)
                {
                    currentBehavior = AIBehavior.Retreat;
                    return;
                }
                // If target is close, stop trying to intercept
                HandleChasing(target, distToTarget);
            }
        }

        // --- SHARED RANGED FIRE LOGIC: called from both armed and unarmed contexts ---
        private void HandleRangedFire(Transform target, float distToTarget)
        {
            float aiRange = controller.HeldObject?.AIRangeBonus ?? 0f;

            float optimalRange = attackRange + aiRange * 0.7f;   // comfortable firing zone
            float minimumRange = attackRange + 1f;                // too close — back up
            bool canFire = false;

            if (distToTarget < minimumRange)
            {
                SprintPressed = true;
                currentBehavior = AIBehavior.Retreat;
                canFire = false;
            }
            else if (distToTarget > optimalRange)
            {
                HandleChasing(target, distToTarget);
                canFire = false;
            }
            else
            {
                canFire = true;
                MoveInput = Vector2.zero;

                // Face the target (rotate hips toward target)
                Vector3 toTarget = (target.position - coreRb.position).normalized;
                toTarget.y = 0;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toTarget);
                    MoveInput = new Vector2(toTarget.x, toTarget.z).normalized * 0.1f;
                }
            }

            if (Time.time >= nextAttackTime && controller.SwingReady && canFire)
            {
                LightAttackPressed = true;
                nextAttackTime = Time.time + Random.Range(minAttackInterval, maxAttackInterval);
            }
        }
    }
}