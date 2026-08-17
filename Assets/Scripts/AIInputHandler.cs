using System.Collections.Generic;
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

        // --- AI STATE ---
        private enum AIBehavior { Idle, Chase, Grab, Attack, Retreat }
        private AIBehavior currentBehavior = AIBehavior.Idle;
        private float nextDecisionTime;
        private float nextGrabTime;
        private float nextAttackTime;
        private Transform targetPlayer;
        private Rigidbody targetRb;
        private PhysicCharacterController.CharacterState currentState = PhysicCharacterController.CharacterState.Idle;

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
                nextDecisionTime = Time.time + decisionInterval;
                MakeDecision();
            }

            // Execute current behavior
            ExecuteBehavior();
        }

        private void MakeDecision()
        {
            targetPlayer = FindNearestPlayer();
            GrabbableObject nearestGrabbable = FindNearestGrabbable();
            bool isHolding = controller != null && controller.IsHoldingObject;
            float distToTarget = targetPlayer != null ? Vector3.Distance(coreRb.position, targetPlayer.position) : float.MaxValue;

            if (isHolding)
            {
                // Holding an object — throw when target is FAR, swing when CLOSE
                if (targetPlayer != null)
                {
                    if (distToTarget < attackRange)
                        currentBehavior = AIBehavior.Attack; // Swing (LightAttack) at close range
                    else
                        currentBehavior = AIBehavior.Chase;  // Chase to get closer, then throw
                }
                else
                    currentBehavior = AIBehavior.Idle;
            }
            else
            {
                // Not holding — PRIORITIZE GRABBING OBJECT
                // Only fight unarmed if target is VERY close (within 1.5f)
                if (nearestGrabbable != null && distToTarget > 1.5f)
                {
                    currentBehavior = AIBehavior.Grab;
                }
                else if (targetPlayer != null && distToTarget <= 1.5f)
                {
                    currentBehavior = AIBehavior.Attack; // Unarmed fight only when REALLY close
                }
                else if (targetPlayer != null)
                {
                    currentBehavior = AIBehavior.Chase;
                }
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
                        MoveToward(InterceptPosition(targetPlayer));
                    else
                        MoveInput = Vector2.zero;
                    break;

                case AIBehavior.Grab:
                    GrabbableObject grabbable = FindNearestGrabbable();
                    if (grabbable != null)
                    {
                        MoveToward(grabbable.transform.position);
                        if (Time.time >= nextGrabTime && Vector3.Distance(coreRb.position, grabbable.transform.position) < grabRange)
                        {
                            GrabPressed = true;
                            nextGrabTime = Time.time + grabCooldown;
                            currentBehavior = AIBehavior.Chase; // After grabbing, chase nearest player
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

                        if (controller != null && controller.IsHoldingObject)
                        {
                            // Holding object: THROW when far, SWING when close
                            if (distToTarget > attackRange)
                            {
                                // Move to intercept and throw
                                MoveToward(InterceptPosition(targetPlayer));
                                if (Time.time >= nextAttackTime)
                                {
                                    HeavyAttackPressed = true; // Throw
                                    nextAttackTime = Time.time + attackCooldown;
                                }
                            }
                            else
                            {
                                MoveInput = Vector2.zero;
                                if (Time.time >= nextAttackTime)
                                {
                                    LightAttackPressed = true; // Swing
                                    nextAttackTime = Time.time + attackCooldown;
                                }
                            }
                        }
                        else
                        {
                            // Unarmed fight
                            if (distToTarget > attackRange)
                                MoveToward(InterceptPosition(targetPlayer));
                            else
                            {
                                MoveInput = Vector2.zero;
                                if (Time.time >= nextAttackTime)
                                {
                                    if (Random.value < 0.6f)
                                        LightAttackPressed = true;
                                    else
                                        HeavyAttackPressed = true;
                                    nextAttackTime = Time.time + attackCooldown;
                                }
                            }
                        }
                    }
                    else
                    {
                        currentBehavior = AIBehavior.Idle;
                    }
                    break;

                case AIBehavior.Retreat:
                    // Move away from nearest threat
                    Transform threat = FindNearestPlayer();
                    if (threat != null)
                    {
                        Vector3 awayDir = (coreRb.position - threat.position).normalized;
                        MoveInput = new Vector2(awayDir.x, awayDir.z).normalized;
                    }
                    else
                    {
                        currentBehavior = AIBehavior.Idle;
                    }
                    break;
            }
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
                return;
            }

            Vector3 dir = toTarget.normalized;
            MoveInput = new Vector2(dir.x, dir.z).normalized;

            // Lift Body Upward when grounded (mirrors HandleMovement in PhysicCharacterController)
            if (controller != null && controller.IsGrounded)
            {
                float forwardSpeed = Mathf.Abs(Vector3.Dot(coreRb.linearVelocity, transform.forward));
                float rightSpeed = Mathf.Abs(Vector3.Dot(coreRb.linearVelocity, transform.right));
                float highestSpeed = forwardSpeed > rightSpeed ? forwardSpeed : rightSpeed;
                if (highestSpeed > 0.1f)
                {
                    coreRb.AddForce(Vector3.up * highestSpeed * (controller.SprintPressed ? 0.175f : 0.2f), ForceMode.Impulse);
                }
            }
        }

        /// <summary>
        /// Predict where the target will be by the time we reach them, based on their current velocity.
        /// Simple linear intercept: targetPos + targetVelocity * (distance / aiSpeed)
        /// </summary>
        private Vector3 InterceptPosition(Transform target)
        {
            if (target == null) return coreRb.position;

            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            if (targetRb == null) return target.position;

            Vector3 toTarget = target.position - coreRb.position;
            toTarget.y = 0;
            float distance = toTarget.magnitude;

            // Predict time to reach target (based on AI speed and distance)
            float eta = distance / moveSpeed;
            eta = Mathf.Clamp(eta, 0.1f, 5f);

            // Predict target's future position based on their velocity
            Vector3 predictedPos = target.position + targetRb.linearVelocity * eta;
            return predictedPos;
        }

        private Transform FindNearestPlayer()
        {
            if (coreRb == null) return null;

            PhysicCharacterController[] allControllers = FindObjectsByType<PhysicCharacterController>(FindObjectsSortMode.None);
            Transform nearest = null;
            float nearestDist = chaseRange;

            foreach (var ctrl in allControllers)
            {
                // Skip self
                if (ctrl == controller) continue;

                // Skip dead characters
                var health = ctrl.GetComponent<PlayerHealth>();
                if (health != null && health.CurrentHealth <= 0) continue;

                Rigidbody otherRb = ctrl.CoreRigidbody;
                if (otherRb == null) continue;

                float dist = Vector3.Distance(coreRb.position, otherRb.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = otherRb.transform;
                }
            }

            return nearest;
        }

        private GrabbableObject FindNearestGrabbable()
        {
            if (coreRb == null) return null;

            Collider[] hits = Physics.OverlapBox(
                coreRb.position + coreRb.transform.forward * grabRange,
                new Vector3(0.5f, 0.5f, grabRange),
                coreRb.rotation,
                LayerMask.GetMask("Grabbable")
            );

            GrabbableObject nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var grabbable = hit.GetComponent<GrabbableObject>();
                if (grabbable != null && !grabbable.IsHeld)
                {
                    float dist = Vector3.Distance(coreRb.position, hit.transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = grabbable;
                    }
                }
            }

            return nearest;
        }
    }
}