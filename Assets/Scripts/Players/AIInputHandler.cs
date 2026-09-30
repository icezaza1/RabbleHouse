using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace RabbleHouse
{
    /// <summary>
    /// AI input handler — provides the same input interface as PhysicInputHandler
    /// but driven by AI logic. Replaces PlayerInput on AI-controlled characters.
    /// </summary>
    public partial class AIInputHandler : MonoBehaviour
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
        [SerializeField] private LayerMask grabbableMask;
        [SerializeField] private float obstacleCheckDistance = 1.5f;
        [SerializeField] private float obstacleCheckRadius = 0.45f;
        [Tooltip("How strongly the AI prefers moving toward the target over avoiding an obstacle.")]
        [Range(0f, 1f)]
        [SerializeField] private float targetDirectionWeight = 0.7f;
        [Tooltip("How long the AI must make very little progress before recovering.")]
        [SerializeField] private float stuckTime = 0.8f;
        [Tooltip("Minimum distance the AI must move during the stuck check.")]
        [SerializeField] private float minimumProgress = 0.15f;
        [Tooltip("How long the AI commits to going around an obstacle.")]
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
            lastMovementCheckPosition = coreRb.position;
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
                    decisionInterval = 1f;
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

            // Don't act while ragdoll
            if (currentState == PhysicCharacterController.CharacterState.Ragdoll)
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

    }
}