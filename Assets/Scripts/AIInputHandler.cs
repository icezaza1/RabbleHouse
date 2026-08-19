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
        [SerializeField] private float attackRange = 2f;

        [Header("Timing")]
        [SerializeField] private float decisionInterval = 0.3f;
        [SerializeField] private float grabCooldown = 2f;
        [SerializeField] private float attackCooldown = 1.5f;

        [Header("AI Weight")]
        [Tooltip("How likely to attack vs grab (0=always grab, 1=always attack)")]
        [SerializeField] private float aggression = 0.5f;

        [Header("Intercept")]
        [SerializeField] private float moveSpeed = 5f; // predicted speed for intercept calculation

        [Header("Personality / Variance")]
        [SerializeField] private float dodgeChance = 0.2f;
        [SerializeField] private float circleChance = 0.15f;
        [SerializeField] private float retreatChance = 0.1f;
        [SerializeField] private float feintChance = 0.1f;
        [SerializeField] private float minAttackInterval = 0.8f;
        [SerializeField] private float maxAttackInterval = 2.5f;

        [Header("Unarmed vs Armed Behaviors")]
        [SerializeField] private float baitChance = 0.2f;          // sprint in, sprint out when target swings
        [SerializeField] private float backingUpChance = 0.25f;    // maintain distance from armed target
        [SerializeField] private float chargeChance = 0.15f;       // sprint in with heavy punch
        [SerializeField] private float baitDistance = 3f;          // distance to approach when baiting
        [SerializeField] private float safeDistance = 4f;          // preferred distance from armed target
        [SerializeField] private float minThrowHoldTime = 2f;      // randomized throw timing
        [SerializeField] private float maxThrowHoldTime = 5f;
        [SerializeField] private float retreatGrabChance = 0.5f;   // chance to seek a nearby object while backing up
        [SerializeField] private float retreatGrabRange = 8f;      // how far an object can be to consider grabbing during retreat

        // Internal personality state
        private float nextAttackTime;
        private bool isDodging = false;
        private bool isCircling = false;
        private float circleDirection = 1f;
        private float dodgeEndTime;
        private float circleEndTime;
        private float objectGrabTime = -1f;
        private bool isBaiting = false;
        private float baitEndTime;
        private bool isBackingUp = false;
        [SerializeField] private float chargeDuration = 1.5f; // max time to charge before giving up
        private float randomThrowHoldTime;
        private bool isCharging = false;
        private float chargeEndTime;
        private float backUpEndTime;

        // --- AI STATE ---
        private enum AIBehavior { Idle, Chase, Grab, Attack, Retreat, Throw, Charge }
        private AIBehavior currentBehavior = AIBehavior.Idle;
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
                // Holding an object — make armed-target decisions
                bool targetIsArmed = targetPlayer != null && IsTargetArmed(targetPlayer);
                float timeHeld = Time.time - objectGrabTime;

                if (targetIsArmed)
                {
                    // Target is armed — decide between bait/charge/throw based on chance and distance
                    int roll = Random.Range(0, 100);

                    if (distToTarget > attackRange + (controller.HeldObject?.AttackRangeBonus ?? 0f))
                    {
                        // Far — decide between charge or retreat-to-grab
                        if (roll < chargeChance * 100)
                        {
                            currentBehavior = AIBehavior.Charge; // Sprint in with heavy punch
                        }
                        else
                        {
                            // Throw the held object at the armed target
                            if ((Time.time - objectGrabTime) > randomThrowHoldTime)
                            {
                                currentBehavior = AIBehavior.Throw;
                            }
                            else
                            {
                                currentBehavior = AIBehavior.Chase; // Close with swing, then throw
                            }
                        }
                    }
                    else
                    {
                        // Close range against armed target — back off or throw
                        if (roll < backingUpChance * 100)
                        {
                            currentBehavior = AIBehavior.Retreat;
                        }
                        else if ((Time.time - objectGrabTime) > randomThrowHoldTime)
                        {
                            currentBehavior = AIBehavior.Throw; // Throw when close enough
                        }
                        else
                        {
                            currentBehavior = AIBehavior.Attack; // Swing at close range
                        }
                    }
                }
                else
                {
                    // Target is unarmed — standard armed AI behavior
                    float bonusRange = attackRange + (controller.HeldObject?.AttackRangeBonus ?? 0f);
                    if (distToTarget < bonusRange)
                    {
                        if (timeHeld > randomThrowHoldTime)
                            currentBehavior = AIBehavior.Throw;
                        else
                            currentBehavior = AIBehavior.Attack; // Swing at close range
                    }
                    else
                    {
                        if (distToTarget > attackRange * 3f + (controller.HeldObject?.AttackRangeBonus ?? 0f))
                            currentBehavior = AIBehavior.Chase; // Far — chase
                        else
                        {
                            if (timeHeld > randomThrowHoldTime)
                                currentBehavior = AIBehavior.Throw;
                            else
                                currentBehavior = AIBehavior.Chase; // Use swing to close distance
                        }
                    }
                }
            }
            else
            {
                // Reset object grab time when not holding
                objectGrabTime = -1f;

                bool targetIsArmed = targetPlayer != null && IsTargetArmed(targetPlayer);
                if (targetIsArmed)
                {
                    // ---- UNARMED AI vs ARMED TARGET ----
                    // This is where bait/charge/backing-up logic should go
                    // The actual behavior selection happens in HandleUnarmedCombat()
                    // which has the proper chance-based decision logic
                    currentBehavior = AIBehavior.Attack; // HandleUnarmedCombat decides sub-behavior
                    return;
                }

                // Not holding — prioritize grabbing objects
                if (nearestGrabbableStatic != null && distToTarget > 1.5f)
                    currentBehavior = AIBehavior.Grab;
                else if (targetPlayer != null && distToTarget <= 1.5f)
                    currentBehavior = AIBehavior.Attack; // Only fight unarmed if really close
                else if (targetPlayer != null)
                    currentBehavior = AIBehavior.Chase;
                else
                    currentBehavior = AIBehavior.Idle;
            }
        }

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

                        // Unarmed AI vs armed target — back away, don't chase
                        if (!controller.IsHoldingObject && targetIsArmed && distToTarget < safeDistance)
                        {
                            Vector3 awayFromTarget = (coreRb.position - targetPlayer.position).normalized;
                            awayFromTarget.y = 0;
                            MoveInput = new Vector2(awayFromTarget.x, awayFromTarget.z).normalized;
                        }
                        else
                        {
                            if (distToTarget > 0.5f)
                                SprintPressed = true;
                            MoveToward(InterceptPosition(targetPlayer));
                        }
                    }
                    else
                        MoveInput = Vector2.zero;
                    break;

                case AIBehavior.Grab:
                    GrabbableObject grabbable = nearestGrabbableStatic;
                    if (grabbable != null)
                    {
                        float distToGrabbable = Vector3.Distance(coreRb.position, grabbable.transform.position);
                        if (distToGrabbable > 1f)
                            SprintPressed = true;
                        MoveToward(grabbable.transform.position);
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

                case AIBehavior.Charge:
                    SprintPressed = true;
                    MoveToward(targetPlayer.position);
                    float chargeRange = attackRange + (controller.HeldObject?.AttackRangeBonus ?? 0f);
                    if (Vector3.Distance(transform.position, targetPlayer.position) <= chargeRange)
                    {
                        HeavyAttackPressed = true;
                    }
                    break;

                case AIBehavior.Attack:
                    if (targetPlayer != null)
                    {
                        if (controller == null || !controller.IsHoldingObject)
                        {
                            // Unarmed — fight with personality
                            float distToTarget = Vector3.Distance(coreRb.position, targetPlayer.position);
                            HandleUnarmedCombat(targetPlayer, distToTarget);
                        }
                        else
                        {
                            // Holding object — swing when close, throw when too long
                            float distToTarget = Vector3.Distance(coreRb.position, targetPlayer.position);
                            float effectiveRange = attackRange + (controller.HeldObject?.AttackRangeBonus ?? 0f);
                            if (distToTarget < effectiveRange)
                            {
                                MoveInput = Vector2.zero;
                                if (Time.time >= nextAttackTime)
                                {
                                    LightAttackPressed = true; // Swing
                                    nextAttackTime = Time.time + Random.Range(minAttackInterval, maxAttackInterval);
                                }
                            }
                            else if ((Time.time - objectGrabTime) > randomThrowHoldTime)
                            {
                                // Close but held too long, or far — throw it
                                MoveInput = Vector2.zero;
                                if (Time.time >= nextAttackTime)
                                {
                                    HeavyAttackPressed = true; // Throw
                                    nextAttackTime = Time.time + attackCooldown;
                                    objectGrabTime = -1f;
                                }
                            }
                            else
                            {
                                // Target is far — chase to get in range
                                MoveToward(targetPlayer.position);
                            }
                        }
                    }
                    else
                    {
                        currentBehavior = AIBehavior.Idle;
                    }
                    break;

                case AIBehavior.Retreat:
                    Transform threat = FindNearestPlayer();
                    if (threat != null)
                    {
                        // Chance to seek a nearby object while backing up (avoiding target)
                        if (Random.value < retreatGrabChance && !controller.IsHoldingObject)
                        {
                            GrabbableObject retreatTarget = FindRetreatGrabbable(threat);
                            if (retreatTarget != null)
                            {
                                Vector3 objPos = retreatTarget.transform.position;
                                Vector3 toObj = (objPos - coreRb.position).normalized;
                                Vector3 awayDir = (coreRb.position - threat.position).normalized;

                                // Blend: mostly toward the object, slightly away from threat
                                SprintPressed = true;
                                Vector3 blended = (toObj * 0.7f + awayDir * 0.3f).normalized;
                                Vector2 safeDir = GetSafeRetreatDirection(blended);
                                MoveInput = safeDir != Vector2.zero ? safeDir : new Vector2(awayDir.x, awayDir.z).normalized;

                                // If we're close enough to the object, grab it
                                float distToObj = Vector3.Distance(coreRb.position, objPos);
                                if (distToObj < grabRange && Time.time >= nextGrabTime)
                                {
                                    GrabPressed = true;
                                    nextGrabTime = Time.time + grabCooldown;
                                    objectGrabTime = Time.time;
                                    currentBehavior = AIBehavior.Grab;
                                }
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
                return;
            }

            Vector3 dir = toTarget.normalized;
            MoveInput = new Vector2(dir.x, dir.z).normalized;
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
        private GrabbableObject FindRetreatGrabbable(Transform threat)
        {
            if (coreRb == null) return null;

            Collider[] hits = Physics.OverlapSphere(coreRb.position + Vector3.up * 1f, retreatGrabRange, LayerMask.GetMask("Grabbable"));

            // Collect all valid objects (not held, not closer to threat than to AI)
            List<GrabbableObject> valid = new List<GrabbableObject>();
            foreach (var hit in hits)
            {
                var grabbable = hit.GetComponent<GrabbableObject>();
                if (grabbable == null || grabbable.IsHeld) continue;

                if (threat != null)
                {
                    float distToThreat = Vector3.Distance(hit.transform.position, threat.position);
                    float distMeToThreat = Vector3.Distance(coreRb.position, threat.position);
                    // Only keep objects that aren't blatantly closer to the threat than the AI
                    if (distToThreat < distMeToThreat - 0.5f) continue;
                }
                valid.Add(grabbable);
            }

            if (valid.Count == 0) return null;

            // Pick one at random — more diverse behavior across multiple AIs
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

            // ETA using the AI's actual sprint speed (matches HandleMovement)
            float aiSpeed = (controller != null && controller.SprintPressed) ? moveSpeed * 1.5f : moveSpeed;
            float eta = Mathf.Clamp(distance / aiSpeed, 0.1f, 5f);

            // Pure velocity extrapolation
            Vector3 predicted = targetPos + targetRb.linearVelocity * eta;
            predicted.y = 0f;

            // Bias toward cutting INSIDE the circle: aim at the midpoint between
            // the target's current position and the extrapolated point. This pulls
            // the aim inward so the AI doesn't just orbit alongside a circling player.
            Vector3 aimPoint = Vector3.Lerp(targetPos, predicted, 0.5f);

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
                if (isBaiting && Time.time < baitEndTime)
                {
                    // Phase A: close distance to baitDistance — sprint toward target
                    SprintPressed = true;
                    MoveToward(target.position);
                    // Transition to Phase B once we are close enough to bait
                    if (distToTarget <= baitDistance)
                    {
                        baitEndTime = Time.time; // force Phase B next frame
                    }
                    return;
                }
                if (isBaiting && Time.time >= baitEndTime)
                {
                    // Phase B: step away briefly after reaching bait distance
                    isBaiting = false;
                    Vector3 awayDir = (coreRb.position - target.position).normalized;
                    awayDir.y = 0;
                    // Wall-aware retreat: if backing into a wall, slide along it
                    MoveInput = GetSafeRetreatDirection(awayDir);
                    SprintPressed = false;
                    // High chance to immediately follow up with a charge
                    if (Random.value < 0.7f)
                    {
                        isCharging = true;
                        chargeEndTime = Time.time + chargeDuration;
                    }
                    return;
                }

                // 2) CHARGE: sprint in and heavy punch when in range
                if (isCharging && Time.time < chargeEndTime)
                {
                    SprintPressed = true;
                    MoveToward(target.position);
                    if (distToTarget < attackRange && Time.time >= nextAttackTime)
                    {
                        HeavyAttackPressed = true;
                        nextAttackTime = Time.time + attackCooldown;
                    }
                    return;
                }
                isCharging = false;

                // 3) BACK-UP: keep safeDistance, occasionally seek a grab/other target
                if (isBackingUp && Time.time < backUpEndTime)
                {
                    if (distToTarget < safeDistance)
                    {
                        Vector3 awayDir = (coreRb.position - target.position).normalized;
                        awayDir.y = 0;
                        // Wall-aware retreat: slide along the wall instead of pushing into it
                        MoveInput = GetSafeRetreatDirection(awayDir);
                    }
                    else
                    {
                        MoveInput = Vector2.zero;
                    }
                    SprintPressed = false;
                    return;
                }
                isBackingUp = false;

                // --- No active sub-state: pick one based on chances ----------
                float roll = Random.value;
                float cumulative = 0f;

                cumulative += baitChance;
                if (roll < cumulative)
                {
                    isBaiting = true;
                    // Phase A ends either when close enough (baitDistance) or after this max time
                    baitEndTime = Time.time + 1.5f;
                    return;
                }

                cumulative += backingUpChance;
                if (roll < cumulative)
                {
                    isBackingUp = true;
                    backUpEndTime = Time.time + Random.Range(0.8f, 1.8f);
                    // Chance to look for a nearby object or new unarmed target
                    if (Random.value < 0.5f)
                    {
                        GrabbableObject nearby = nearestGrabbableStatic;
                        if (nearby != null)
                        {
                            currentBehavior = AIBehavior.Grab;
                            return;
                        }
                    }
                    return;
                }

                cumulative += chargeChance;
                if (roll < cumulative)
                {
                    isCharging = true;
                    chargeEndTime = Time.time + chargeDuration;
                    return;
                }

                // Default fallback: back up
                isBackingUp = true;
                backUpEndTime = Time.time + Random.Range(0.8f, 1.8f);
                return;
            }

            // ===================================================================
            //  UNARMED TARGET — full combat personality.
            // ===================================================================

            // Currently dodging
            if (isDodging && Time.time < dodgeEndTime)
            {
                Vector3 awayFromTarget = (coreRb.position - target.position).normalized;
                awayFromTarget.y = 0;
                MoveInput = new Vector2(awayFromTarget.x, awayFromTarget.z).normalized;
                return;
            }
            isDodging = false;

            // Currently circling
            if (isCircling && Time.time < circleEndTime)
            {
                Vector3 toTarget = (target.position - coreRb.position).normalized;
                Vector3 perp = Vector3.Cross(toTarget, Vector3.up) * circleDirection;
                MoveInput = new Vector2(perp.x, perp.z).normalized;

                // Wall collision during circling -> reverse direction
                if (Physics.Raycast(coreRb.position, perp.normalized, 1.5f, LayerMask.GetMask("Default", "Wall", "Environment")))
                {
                    circleDirection *= -1f;
                }
                return;
            }
            isCircling = false;

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
                isDodging = true;
                dodgeEndTime = Time.time + Random.Range(0.3f, 0.6f);
                return;
            }

            // Circle?
            if (Random.value < circleChance)
            {
                SprintPressed = true;
                isCircling = true;
                circleDirection = Random.value > 0.5f ? 1f : -1f;
                circleEndTime = Time.time + Random.Range(1f, 2.5f);
                return;
            }

            // Attack (light combo / heavy / feint)
            if (Time.time >= nextAttackTime)
            {
                if (Random.value < 0.6f)
                {
                    LightAttackPressed = true;
                    nextAttackTime = Time.time + Random.Range(0.2f, 0.4f);
                }
                else
                {
                    if (Random.value < feintChance)
                    {
                        HeavyAttackPressed = true;
                        isDodging = true;
                        dodgeEndTime = Time.time + 0.2f;
                    }
                    else
                    {
                        HeavyAttackPressed = true;
                        nextAttackTime = Time.time + Random.Range(minAttackInterval, maxAttackInterval);
                    }
                }
            }
        }
    }
}