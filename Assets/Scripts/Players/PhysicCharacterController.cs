using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.Animations.Rigging;

namespace RabbleHouse
{
    [System.Serializable]
    public class VFXs
    {
        public GameObject leftSwingPrefab;
        public GameObject rightSwingPrefab;
    }
    /// <summary>
    /// Main controller for Physic_Character - the active ragdoll character.
    /// Handles input, movement, combat, grab/drop, and ragdoll state transitions.
    /// </summary>
    public partial class PhysicCharacterController : MonoBehaviour
    {
        // --- PUBLIC STATE ---
        public enum CharacterState
        {
            Idle,
            Moving,
            Stunned,
            Ragdoll,
            Dead,
            Grabbing,
        }

        public CharacterState CurrentState => currentState;
        public bool IsHoldingObject => heldObject != null;
        public GrabbableObject HeldObject => heldObject;
        public bool IsGrounded => isGrounded;
        public bool HeavyPunchReady => heavyPunchCooldownTimer <= 0f && !isHeavyPunching;
        public bool SwingReady => swingCooldownTimer <= 0f;
        public bool SprintPressed => sprintPressed;
        public int PlayerIndex { get; set; } = 0;
        public Rigidbody CoreRigidbody => coreRigidbody;
        public float StunDuration => stunDuration;
        public float KnockdownDuration => knockdownDuration;

        // --- COMPONENTS ---
        private Rigidbody coreRigidbody;
        private PlayerInput playerInput;
        private PhysicInputHandler inputHandler;
        private AIInputHandler aiInputHandler;
        private ActiveRagdollMaster ragdollMaster;
        private PlayerHealth playerHealth;
        private ActiveRagdollBalancer balancer;
        private ConfigurableJoint hipJoint;

        // --- HAND REFERENCES (assigned in Inspector) ---
        // Each hand must have: Transform + Rigidbody + ConfigurableJoint + ActiveRagdollBone + Collider.
        [Header("Hand Bones")]
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        // Arm joints for punching/grabbing
        [Header("Left Arm Joints")]
        [SerializeField] private ConfigurableJoint leftUpperArm;
        [SerializeField] private ConfigurableJoint leftLowerArm;
        [Header("Right Arm Joints")]
        [SerializeField] private ConfigurableJoint rightUpperArm;
        [SerializeField] private ConfigurableJoint rightLowerArm;

        private JointDrive originalLeftUpperX, originalLeftUpperYZ, originalLeftLowerX, originalLeftLowerYZ;
        private JointDrive originalRightUpperX, originalRightUpperYZ, originalRightLowerX, originalRightLowerYZ;

        // Cached components from the hand transforms (resolved at Start).
        private ActiveRagdollBone LUpperBoneScript, LLowerBoneScript, RUpperBoneScript, RLowerBoneScript;
        private Rigidbody leftHandRb;
        private Joint leftHandJoint;
        private Rigidbody rightHandRb;
        private Joint rightHandJoint;
        private ToolBehaviour activeTool;

        // --- INPUT ---
        private Vector2 moveInput;
        private bool grabPressed;
        private bool lightAttackPressed;
        private bool heavyAttackPressed;
        private bool sprintPressed;

        // --- GRAB TYPES ---
        private GrabbableType heldGrabbableType = GrabbableType.SmallObject;

        // --- STATE ---
        private CharacterState currentState = CharacterState.Idle;
        private GrabbableObject heldObject;
        private float punchCooldownTimer;
        private float heavyPunchCooldownTimer;
        private bool isGrounded;
        private bool isSprinting;
        private Vector3 currentMoveDir;

        // --- PUNCHING ---
        [Header("Punching")]
        [SerializeField] private float lightPunchCooldown = 0.5f;
        [SerializeField] private float lightPunchWindow = 2.0f;
        [SerializeField] private float punchTravelTime = 0.18f;
        [SerializeField] private float punchHoldTime = 0.12f;
        private float lastLightPunchTime;
        private bool isInLightPunchWindow => Time.time - lastLightPunchTime <= lightPunchWindow;
        private bool lightPunchActive = false;
        private bool lightPunchDirection = false; // false = left, true = right
        private bool leftPunching = false;
        private bool rightPunching = false;
        private bool leftArmWaitingForWindow = false;
        private bool rightArmWaitingForWindow = false;

        // --- HEAVY PUNCH ---
        [Header("Heavy Punching")]
        [SerializeField] private float heavyPunchCooldown = 1.0f;
        [SerializeField] private float heavyTravelTime = 0.5f;
        [SerializeField] private float heavyHoldTime = 0.5f;
        private bool isHeavyPunching = false;
        private bool hipRotationSuppressed = false;
        private bool heavyPunchLeftArm = true; // toggle for next arm
        private float swingCooldownTimer = 0f;

        [Header("Heavy Punch Profiles")]
        [SerializeField] private ArmPunchProfile leftHeavyProfile;
        [SerializeField] private ArmPunchProfile rightHeavyProfile;
        [SerializeField] private float HipHookRotation;
        [SerializeField] private float smallObjectSwingAngle = 45f;

        [System.Serializable]
        private class ArmPunchProfile
        {
            public Vector3 windUpUpper;
            public Vector3 windUpLower;
            public Vector3 punchUpper;
            public Vector3 punchLower;
        }

        [Header("Arm Punch Profiles")]
        [SerializeField] private ArmPunchProfile leftArmProfile;   // UpperLeft/LowerLeft
        [SerializeField] private ArmPunchProfile rightArmProfile;  // UpperRight/LowerRight

        // --- SETTINGS ---
        [Header("Reference")]
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private Transform grabAnchorPoint;
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private LayerMask groundLayer = 1 << 0;
        [SerializeField] private float groundCheckDistance = 0.2f;
        [SerializeField] private float hipHeight = 0.95f;
        [SerializeField] private float balancerWeightMoving = 0.3f;
        [SerializeField] private float balancerBlendSpeed = 6f;

        [Header("Combat")]
        [SerializeField] private float grabRange = 1.5f;
        [SerializeField] private float throwForce = 20f;
        [SerializeField] private float punchForce = 15f;
        [SerializeField] private float punchRange = 1f;
        [SerializeField] private int punchDamage = 10;
        [SerializeField] private int heavyPunchDamage = 20;
        [Tooltip("Type of effect a heavy punch applies (Stun, Knockdown, or None).")]
        [SerializeField] private HitType heavyPunchHitType = HitType.Stun;
        [Tooltip("Likelihood the heavy punch's effect actually triggers (0-1). 1 = always.")]
        [SerializeField] private float heavyPunchEffectChance = 0.8f;

        [Header("Stun/Recovery")]
        [SerializeField] private float stunDuration = 2f;
        [SerializeField] private float knockdownDuration = 1.2f;
        [Tooltip("Impulse applied to the core body when knocked down / hit by a swung object.")]
        [SerializeField] private float knockbackForce = 35f;

        [Header("Particles/VFXs")]
        [SerializeField] private VFXs vfxs;

        // --- LIFECYCLE ---
        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            inputHandler = GetComponent<PhysicInputHandler>();
            aiInputHandler = GetComponent<AIInputHandler>();
            ragdollMaster = GetComponent<ActiveRagdollMaster>();
            playerHealth = GetComponent<PlayerHealth>();

            coreRigidbody = FindCoreRigidbody();
            if (coreRigidbody == null)
                Debug.LogError("PhysicCharacterController: no Rigidbody found under Physic_Character.", this);
            hipJoint = coreRigidbody.GetComponent<ConfigurableJoint>();
        }

        private void Start()
        {
            if (playerInput != null)
                PlayerIndex = playerInput.playerIndex;

            if (coreRigidbody != null)
                balancer = coreRigidbody.GetComponent<ActiveRagdollBalancer>();

            // Cache hand components from the Inspector-assigned transforms.
            if (leftHand != null)
            {
                leftHandRb = leftHand.GetComponent<Rigidbody>();
                LUpperBoneScript = leftUpperArm.GetComponent<ActiveRagdollBone>();
                LLowerBoneScript = leftLowerArm.GetComponent<ActiveRagdollBone>();
            }
            if (rightHand != null)
            {
                rightHandRb = rightHand.GetComponent<Rigidbody>();
                RUpperBoneScript = rightUpperArm.GetComponent<ActiveRagdollBone>();
                RLowerBoneScript = rightLowerArm.GetComponent<ActiveRagdollBone>();
            }

            // Get the original angular drive
            originalLeftUpperX = leftUpperArm.angularXDrive; originalLeftUpperYZ = leftUpperArm.angularYZDrive;
            originalLeftLowerX = leftLowerArm.angularXDrive; originalLeftLowerYZ = leftLowerArm.angularYZDrive;
            originalRightUpperX = rightUpperArm.angularXDrive; originalRightUpperYZ = rightUpperArm.angularYZDrive;
            originalRightLowerX = rightLowerArm.angularXDrive; originalRightLowerYZ = rightLowerArm.angularYZDrive;
        }

        private Rigidbody FindCoreRigidbody()
        {
            Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in bodies)
                if (rb.transform.name.Contains("Hips"))
                    return rb;
            return bodies.Length > 0 ? bodies[0] : null;
        }

        private void LateUpdate()
        {
            // Dead characters do nothing — no movement, no attacks
            if (currentState == CharacterState.Dead)
                return;

            // While holding an object, override both hand joints' targetRotation
            // so the arms raise.  LateUpdate runs after FixedUpdate, so our
            // value wins over ActiveRagdollBone's per-frame write.
            if (heldObject != null && !isHeavyPunching)
            {
                RaiseArmsForHeld();
            }

            // Handle light attack (punch or swing depending on held object)
            HandleLightAttack();

            // Handle heavy attack (heavy punch or throw depending on held object)
            if (heavyAttackPressed)
            {
                HandleHeavyAttack();
            }
        }

        private void Update()
        {
            if (currentState == CharacterState.Dead)
                return;

            ReadInput();
            CheckGrounded();
            UpdateState();
            HandlePunchCooldown();
            CheckLightPunchWindowExpiry();
        }

        private void FixedUpdate()
        {
            switch (currentState)
            {
                case CharacterState.Idle:
                    targetAnimator.SetBool("IsWalking", false);
                    break;
                case CharacterState.Moving:
                    targetAnimator.SetBool("IsWalking", true);
                    HandleMovement();
                    HandleRotation();
                    break;
                case CharacterState.Grabbing:
                    targetAnimator.SetBool("IsWalking", moveInput.magnitude > 0.01f);
                    HandleMovement();
                    HandleRotation();
                    break;
            }

            // Handle balancer weight
            if (balancer != null)
            {
                float target = (currentState == CharacterState.Moving) ? balancerWeightMoving : 1f;
                balancer.weight = Mathf.Lerp(balancer.weight, target, Time.fixedDeltaTime * balancerBlendSpeed);
            }
        }

        private void SetState(CharacterState newState)
        {
            if (currentState == newState) return;
            currentState = newState;
        }

        // --- INPUT HANDLING ---
        private void ReadInput()
        {
            // Priority: AIInputHandler (if present) > PhysicInputHandler
            if (aiInputHandler != null)
            {
                moveInput = aiInputHandler.MoveInput;
                grabPressed = aiInputHandler.GrabPressed;
                lightAttackPressed = aiInputHandler.LightAttackPressed;
                heavyAttackPressed = aiInputHandler.HeavyAttackPressed;
                sprintPressed = aiInputHandler.SprintPressed;
            }
            else if (inputHandler != null)
            {
                moveInput = inputHandler.MoveInput;
                grabPressed = inputHandler.GrabPressed;
                lightAttackPressed = inputHandler.LightAttackPressed;
                heavyAttackPressed = inputHandler.HeavyAttackPressed;
                sprintPressed = inputHandler.SprintPressed;
            }

            // Toggle grab: press to grab, press again to drop.
            if (grabPressed)
            {
                if (heldObject == null)
                    TryGrabObject();
                else
                    ReleaseObject();
            }
        }

        // --- STATE MACHINE ---
        private void UpdateState()
        {
            if (currentState == CharacterState.Stunned || currentState == CharacterState.Ragdoll)
                return;

            if (currentState == CharacterState.Dead)
                return;

            if (heldObject != null)
            {
                SetState(CharacterState.Grabbing);
                return;
            }

            SetState(moveInput.magnitude > 0.1f ? CharacterState.Moving : CharacterState.Idle);
        }

        // --- CHECK GROUNDED ---
        private void CheckGrounded()
        {
            if (coreRigidbody == null) return;

            Vector3 feet = coreRigidbody.position - Vector3.up * hipHeight;
            isGrounded = Physics.Raycast(feet, Vector3.down, groundCheckDistance, groundLayer);
        }

        // --- MOVEMENT ---
        private void HandleMovement()
        {
            if (coreRigidbody == null) return;
            if (!isGrounded) return;

            // Sprint Handle
            isSprinting = (heldObject == null || heldGrabbableType == GrabbableType.Tool) && (sprintPressed ? true : false);
            targetAnimator.SetFloat("AnimationSpeed", isSprinting ? 1 : 2);

            Vector3 forward = Camera.main ? Camera.main.transform.forward : Vector3.forward;
            Vector3 right = Camera.main ? Camera.main.transform.right : Vector3.right;
            forward.y = right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;
            Vector3 targetVel = moveDir * (isSprinting ? moveSpeed * 2f : moveSpeed);
            targetVel.y = coreRigidbody.linearVelocity.y;

            // Move Character
            coreRigidbody.linearVelocity = Vector3.Lerp(coreRigidbody.linearVelocity, targetVel, Time.fixedDeltaTime * 10f);
            // Set move direction for character rotation
            currentMoveDir = moveDir;

            // Lift Body Upward
            float forwardSpeed = Mathf.Abs(Vector3.Dot(coreRigidbody.linearVelocity, transform.forward));
            float rightSpeed = Mathf.Abs(Vector3.Dot(coreRigidbody.linearVelocity, transform.right));
            float highestSpeed = forwardSpeed > rightSpeed ? forwardSpeed : rightSpeed;
            if (highestSpeed > 0.1f)
            {
                coreRigidbody.AddForce(Vector3.up * highestSpeed * (isSprinting ? 2.5f : 4.5f), ForceMode.Impulse);
            }
        }

        private void HandleRotation()
        {
            if (coreRigidbody == null) return;
            if (currentMoveDir == Vector3.zero) return;
            if (hipRotationSuppressed) return;  // don't fight the hip hook

            Quaternion targetRot = Quaternion.LookRotation(currentMoveDir);

            float rotationStrength = 1f;
            // Small & LargeObject: heavy object dragging behind makes hip rotation sluggish.
            // Uses the object's hipRotationResistance (0 = instant turn, higher = slower).
            if (heldObject != null)
            {
                float resistance = heldObject.HipRotationResistance;

                rotationStrength = Mathf.Clamp01(1f / (1f + resistance * 0.05f));
            }

            hipJoint.targetRotation = Quaternion.Slerp(hipJoint.targetRotation, 
                Quaternion.Inverse(targetRot),
                rotationStrength * Time.fixedDeltaTime * balancerBlendSpeed
            );
        }

        // --- GIZMOS ---
        private void OnDrawGizmosSelected()
        {
            if (isGrounded)
            {
                Gizmos.color = Color.green;
                Vector3 feet = (coreRigidbody != null ? coreRigidbody.position : transform.position) - Vector3.up * hipHeight;
                Debug.DrawRay(feet, Vector3.down * groundCheckDistance, Color.green);
            }

            if (coreRigidbody == null) return;
            Matrix4x4 oldMatrix = Gizmos.matrix;

            // Grab box gizmo (existing)
            Gizmos.color = Color.green;
            Vector3 origin = coreRigidbody.position + coreRigidbody.transform.forward * (grabRange * 0.5f);
            Vector3 halfExtents = new Vector3(0.1f, 0.5f, grabRange * 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(origin, coreRigidbody.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
            Gizmos.matrix = oldMatrix;

            // --- Hit-cone gizmo ---
            // Matches CheckHit: OverlapSphere at core body + 0.5 up, radius = punchRange,
            // cone gate = dot(toTarget, forward) >= 0.2  (≈ 78.5° half-angle)
            Vector3 hitCenter = coreRigidbody.position + Vector3.up * 0.5f;
            Vector3 fwd = coreRigidbody.transform.forward;
        }
    }
}