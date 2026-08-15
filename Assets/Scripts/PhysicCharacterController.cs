using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace RabbleHouse
{
    /// <summary>
    /// Main controller for Physic_Character - the active ragdoll character.
    /// Handles input, movement, combat, grab/drop, and ragdoll state transitions.
    /// </summary>
    public class PhysicCharacterController : MonoBehaviour
    {
        // --- PUBLIC STATE ---
        public enum CharacterState
        {
            Idle,
            Moving,
            Stunned,
            Ragdoll,
            Grabbing,
            Throwing,
            Punching
        }

        public CharacterState CurrentState => currentState;
        public bool IsHoldingObject => heldObject != null;
        public int PlayerIndex { get; set; } = 0;
        public Rigidbody CoreRigidbody => coreRigidbody;

        // --- COMPONENTS ---
        private Rigidbody coreRigidbody;
        private PlayerInput playerInput;
        private PhysicInputHandler inputHandler;
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
        private ConfigurableJoint leftHandJoint;
        private Rigidbody rightHandRb;
        private ConfigurableJoint rightHandJoint;

        // --- INPUT ---
        private Vector2 moveInput;
        private bool grabPressed;
        private bool grabReleased;
        private bool lightPunchPressed;
        private bool heavyPunchPressed;
        private bool jumpPressed;
        private bool sprintPressed;
        private bool throwHeld;

        // --- STATE ---
        private CharacterState currentState = CharacterState.Idle;
        private GrabbableObject heldObject;
        private float punchCooldownTimer;
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

        [Header("Heavy Punch Profiles")]
        [SerializeField] private ArmPunchProfile leftHeavyProfile;
        [SerializeField] private ArmPunchProfile rightHeavyProfile;
        [SerializeField] private float HipHookRotation;

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
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpForce = 6f;
        [SerializeField] private LayerMask groundLayer = 1 << 0;
        [SerializeField] private float groundCheckDistance = 0.2f;
        [SerializeField] private float hipHeight = 0.95f;
        [SerializeField] private float balancerWeightMoving = 0.3f;
        [SerializeField] private float balancerBlendSpeed = 6f;

        [Header("Combat")]
        [SerializeField] private float grabRange = 1.5f;
        [SerializeField] private float throwForce = 20f;
        [SerializeField] private float maxThrowForce = 40f;
        [SerializeField] private float throwChargeTime = 1f;
        [SerializeField] private float punchForce = 15f;
        [SerializeField] private float punchRange = 1f;
        [SerializeField] private int punchDamage = 10;

        [Header("Stun/Recovery")]
        [SerializeField] private float stunDuration = 2f;
        [SerializeField] private float knockdownDuration = 1.2f;

        // --- LIFECYCLE ---
        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            inputHandler = GetComponent<PhysicInputHandler>();
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
            // While holding an object, override both hand joints' targetRotation
            // so the arms raise.  LateUpdate runs after FixedUpdate, so our
            // value wins over ActiveRagdollBone's per-frame write.
            if (heldObject != null)
            {
                RaiseBothArms();
            }

            // Handle light punch logic - run every frame to check input
            HandleLightPunch();

            // Handle heavy punch input
            if (heavyPunchPressed)
            {
                HandleHeavyPunch();
            }
        }

        private void Update()
        {
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
                case CharacterState.Punching:
                    // This will be overridden by HandleLightPunch if a light punch is in progress
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
            if (inputHandler != null)
            {
                moveInput = inputHandler.MoveInput;
                grabPressed = inputHandler.GrabPressed;
                grabReleased = inputHandler.GrabReleased;
                lightPunchPressed = inputHandler.LightPunchPressed;
                heavyPunchPressed = inputHandler.HeavyPunchPressed;
                jumpPressed = inputHandler.JumpPressed;
                sprintPressed = inputHandler.SprintPressed;
                throwHeld = inputHandler.ThrowHeld;
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
            isSprinting = sprintPressed ? true : false;
            targetAnimator.SetFloat("AnimationSpeed", sprintPressed ? 1 : 2);

            Vector3 forward = Camera.main ? Camera.main.transform.forward : Vector3.forward;
            Vector3 right = Camera.main ? Camera.main.transform.right : Vector3.right;
            forward.y = right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;
            Vector3 targetVel = moveDir * (isSprinting ? moveSpeed * 1.5f : moveSpeed);
            targetVel.y = coreRigidbody.linearVelocity.y;

            coreRigidbody.linearVelocity = Vector3.Lerp(coreRigidbody.linearVelocity, targetVel, Time.fixedDeltaTime * 10f);
            currentMoveDir = moveDir;

            // Lift Body Upward
            float forwardSpeed = Mathf.Abs(Vector3.Dot(coreRigidbody.linearVelocity, transform.forward));
            float rightSpeed = Mathf.Abs(Vector3.Dot(coreRigidbody.linearVelocity, transform.right));
            float highestSpeed = forwardSpeed > rightSpeed ? forwardSpeed : rightSpeed;
            if (highestSpeed > 0.1f)
            {
                coreRigidbody.AddForce(Vector3.up * highestSpeed * (isSprinting ? 3.5f : 4f), ForceMode.Impulse);
            }

            if (jumpPressed && isGrounded)
                coreRigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        private void HandleRotation()
        {
            if (coreRigidbody == null) return;
            if (currentMoveDir == Vector3.zero) return;
            if (hipRotationSuppressed) return;  // don't fight the hip hook

            Quaternion targetRot = Quaternion.LookRotation(currentMoveDir);
            hipJoint.targetRotation = Quaternion.Inverse(targetRot);
        }

        // --- GRAB / DROP ---
        private void TryGrabObject()
        {
            // Use HIP body position for detection (stable reference point).
            Vector3 origin = coreRigidbody.position + Vector3.up * 1f;
            Collider[] hits = Physics.OverlapSphere(origin, grabRange, LayerMask.GetMask("Grabbable"));

            GrabbableObject closest = null;
            float bestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var grabbable = hit.GetComponent<GrabbableObject>();
                if (grabbable != null && !grabbable.IsHeld)
                {
                    float d = Vector3.Distance(origin, hit.transform.position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        closest = grabbable;
                    }
                }
            }

            if (closest != null)
            {
                heldObject = closest;
                closest.GrabByPlayer(this);

                leftHandJoint = SetupGrabJoint(leftHandRb, closest);
                rightHandJoint = SetupGrabJoint(rightHandRb, closest);
                SetState(CharacterState.Grabbing);
            }
        }

        private ConfigurableJoint SetupGrabJoint(Rigidbody handBody, GrabbableObject obj)
        {
            heldObject = obj;
            obj.GrabByPlayer(this);

            // Create the grab joint on the hand bone.
            ConfigurableJoint grabJoint = handBody.gameObject.AddComponent<ConfigurableJoint>();
            grabJoint.connectedBody = heldObject.Rigidbody;

            // autoConfigureConnectedAnchor = true lets Unity auto-calculate the
            // object anchor so it sticks to the hand naturally.
            grabJoint.autoConfigureConnectedAnchor = true;

            grabJoint.anchor = Vector3.zero;

            // Lock linear motion — object sticks to hand.
            grabJoint.xMotion = ConfigurableJointMotion.Locked;
            grabJoint.yMotion = ConfigurableJointMotion.Locked;
            grabJoint.zMotion = ConfigurableJointMotion.Locked;

            // Lock angular — so object doesn't randomly rotate while getting grabbed.
            grabJoint.angularXMotion = ConfigurableJointMotion.Locked;
            grabJoint.angularYMotion = ConfigurableJointMotion.Locked;
            grabJoint.angularZMotion = ConfigurableJointMotion.Locked;

            // Position spring-damper to hold object against gravity.
            JointDrive drive = new JointDrive
            {
                positionSpring = 15000f,
                positionDamper = 15000f,
                maximumForce = 15000f
            };
            grabJoint.xDrive = drive;
            grabJoint.yDrive = drive;
            grabJoint.zDrive = drive;

            grabJoint.breakForce = 1500f;
            grabJoint.breakTorque = 1500f;
            grabJoint.enablePreprocessing = false;

            // Ignore collisions between the hand and the held object.
            Physics.IgnoreCollision(handBody.GetComponent<Collider>(), heldObject.GetComponent<Collider>(), true);

            if (balancer != null)
                balancer.weight *= 0.5f;

            return grabJoint;
        }

        private void ReleaseObject()
        {
            if (heldObject == null) return;

            // Clean up both joints
            if (leftHandJoint != null) Destroy(leftHandJoint);
            if (rightHandJoint != null) Destroy(rightHandJoint);

            heldObject.ReleaseByPlayer();
            heldObject = null;
            leftHandJoint = null;
            rightHandJoint = null;
            ResetBothArms();
            if (balancer != null)
                balancer.weight = 1f;

            SetState(CharacterState.Idle);
        }

        private void ForceDropObject()
        {
            if (heldObject == null) return;

            // Clean up both joints
            if (leftHandJoint != null) Destroy(leftHandJoint);
            if (rightHandJoint != null) Destroy(rightHandJoint);

            heldObject.ReleaseByPlayer();
            heldObject = null;
            leftHandJoint = null;
            rightHandJoint = null;
            ResetBothArms();
            if (balancer != null)
                balancer.weight = 1f;
        }

        // --- ARM MUSCLE HELPERS ---
        public void RaiseBothArms()
        {
            // Disable ActiveRagdollBone scripts so they don't fight our target rotation
            LUpperBoneScript.enabled = false;
            LLowerBoneScript.enabled = false;
            RUpperBoneScript.enabled = false;
            RLowerBoneScript.enabled = false;

            // Boost X and YZ joint drives for both arms
            SetXYZJointStrength(leftUpperArm, 12000f, 120f);
            SetXYZJointStrength(leftLowerArm, 12000f, 120f);
            SetXYZJointStrength(rightUpperArm, 12000f, 120f);
            SetXYZJointStrength(rightLowerArm, 12000f, 120f);

            // Zero out any existing angular velocity
            leftUpperArm.targetAngularVelocity = Vector3.zero;
            rightUpperArm.targetAngularVelocity = Vector3.zero;

            // Set target rotations for the raised arms
            leftUpperArm.targetRotation = Quaternion.Euler(0, -90, 60);
            leftLowerArm.targetRotation = Quaternion.Euler(330, 0, 0);
            rightUpperArm.targetRotation = Quaternion.Euler(0, 90, -60);
            rightLowerArm.targetRotation = Quaternion.Euler(330, 0, 0);
        }

        public void ResetBothArms()
        {
            // Enable ActiveRagdollBone Scripts
            LUpperBoneScript.enabled = true;
            LLowerBoneScript.enabled = true;
            RUpperBoneScript.enabled = true;
            RLowerBoneScript.enabled = true;

            // Revert arm joint drives to their original values
            leftUpperArm.angularXDrive = originalLeftUpperX;
            leftUpperArm.angularYZDrive = originalLeftUpperYZ;
            leftLowerArm.angularXDrive = originalLeftLowerX;
            leftLowerArm.angularYZDrive = originalLeftLowerYZ;

            rightUpperArm.angularXDrive = originalRightUpperX;
            rightUpperArm.angularYZDrive = originalRightUpperYZ;
            rightLowerArm.angularXDrive = originalRightLowerX;
            rightLowerArm.angularYZDrive = originalRightLowerYZ;

            // Reset target rotations to neutral
            leftUpperArm.targetRotation = Quaternion.Euler(0, 0, 0);
            leftLowerArm.targetRotation = Quaternion.Euler(0, 0, 0);
            rightUpperArm.targetRotation = Quaternion.Euler(0, 0, 0);
            rightLowerArm.targetRotation = Quaternion.Euler(0, 0, 0);
        }

        private void SetXYZJointStrength(ConfigurableJoint joint, float spring, float damper)
        {
            // Must update BOTH drives individually for X/YZ mode to react correctly
            JointDrive xDrive = joint.angularXDrive;
            xDrive.positionSpring = spring;
            xDrive.positionDamper = damper;
            joint.angularXDrive = xDrive;

            JointDrive yzDrive = joint.angularYZDrive;
            yzDrive.positionSpring = spring;
            yzDrive.positionDamper = damper;
            joint.angularYZDrive = yzDrive;
        }

        // --- LIGHT / HEAVY PUNCH ---
        private void HandlePunchCooldown()
        {
            if (punchCooldownTimer > 0f)
                punchCooldownTimer -= Time.deltaTime;
        }

        private void CheckLightPunchWindowExpiry()
        {
            if (leftArmWaitingForWindow || rightArmWaitingForWindow)
            {
                if (!isInLightPunchWindow)
                {
                    LUpperBoneScript.enabled = true;
                    LLowerBoneScript.enabled = true;
                    RUpperBoneScript.enabled = true;
                    RLowerBoneScript.enabled = true;
                    leftArmWaitingForWindow = false;
                    rightArmWaitingForWindow = false;
                    leftPunching = false;
                    rightPunching = false;
                    lightPunchActive = false;
                }
            }
        }

        private void HandleLightPunch()
        {
            if (currentState == CharacterState.Grabbing) return;

            // Determine which arm is next based on toggle direction
            bool nextIsLeft = !lightPunchDirection;

            // Check if that arm is free (not currently punching)
            bool armFree = nextIsLeft ? !leftPunching : !rightPunching;

            // Start a new punch if the button is pressed, the target arm is free, and cooldown is ready
            if (lightPunchPressed && armFree && punchCooldownTimer <= 0f)
            {
                StartLightPunch();
            }
        }

        private void StartLightPunch()
        {
            // Determine which arm is about to punch based on toggle direction
            bool isLeft = !lightPunchDirection;
            // Mark that arm as punching
            if (isLeft) leftPunching = true;
            else rightPunching = true;

            lastLightPunchTime = Time.time;
            punchCooldownTimer = lightPunchCooldown;

            // Disable bone scripts so we can manually set target rotations
            LUpperBoneScript.enabled = false;
            LLowerBoneScript.enabled = false;
            RUpperBoneScript.enabled = false;
            RLowerBoneScript.enabled = false;

            // Call PerformPunch with the correct arm
            PerformPunch(isLeft);

            // Flip the direction for the next press
            lightPunchDirection = !lightPunchDirection;

            // Mark the punch as active (used elsewhere)
            lightPunchActive = true;
        }

        private void PerformPunch(bool isLeft)
        {
            // Grab the appropriate profile for the arm
            ArmPunchProfile profile = isLeft ? leftArmProfile : rightArmProfile;

            // Capture the current target rotations (the wind-up pose)
            Quaternion startUpper = Quaternion.Euler(profile.windUpUpper.x, profile.windUpUpper.y, profile.windUpUpper.z);
            Quaternion startLower = Quaternion.Euler(profile.windUpLower.x, profile.windUpLower.y, profile.windUpLower.z);

            // Compute the target punch rotations using the stored profile values.
            Quaternion targetUpper = Quaternion.Euler(profile.punchUpper.x, profile.punchUpper.y, profile.punchUpper.z);
            Quaternion targetLower = Quaternion.Euler(profile.punchLower.x, profile.punchLower.y, profile.punchLower.z);
                                                  

            // Start the lerp coroutine
            StartCoroutine(PunchLerpRoutine(isLeft, startUpper, targetUpper, startLower, targetLower));
        }

        private IEnumerator PunchLerpRoutine(bool isLeft, 
            Quaternion startUpper,
            Quaternion targetUpper,
            Quaternion startLower,
            Quaternion targetLower)
        {
            // Get the joint references for the arm being punched
            ConfigurableJoint upperJoint = (isLeft) ? leftUpperArm : rightUpperArm;
            ConfigurableJoint lowerJoint = (isLeft) ? leftLowerArm : rightLowerArm;

            // --- Move from wind-up to punch rotation ---
            float elapsed = 0f;
            float total = punchTravelTime;
            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                upperJoint.targetRotation = Quaternion.Slerp(startUpper, targetUpper, t);
                lowerJoint.targetRotation = Quaternion.Slerp(startLower, targetLower, t);
                yield return null;
            }

            // --- Hold the apex briefly ---
            elapsed = 0f;
            while (elapsed < punchHoldTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // --- Move back from punch to wind-up ---
            elapsed = 0f;
            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                upperJoint.targetRotation = Quaternion.Slerp(targetUpper, startUpper, t);
                lowerJoint.targetRotation = Quaternion.Slerp(targetLower, startLower, t);
                yield return null;
            }

            // Clean-up: mark arm as finished
            if (isLeft)
            {
                leftPunching = false;
                leftArmWaitingForWindow = true;
            }
            else
            {
                rightPunching = false;
                rightArmWaitingForWindow = true;
            }
        }

        // --- STUN / RAGDOLL ---
        public void HandleHeavyPunch()
        {
            if (isHeavyPunching) return;
            if (lightPunchActive) return;            // block if light punch is in progress
            if (punchCooldownTimer > 0f) return;      // respect light punch cooldown

            heavyPunchLeftArm = !heavyPunchLeftArm;   // alternate arms
            isHeavyPunching = true;
            StartCoroutine(HeavyPunchRoutine(heavyPunchLeftArm));
        }

        private IEnumerator HeavyPunchRoutine(bool isLeft)
        {
            ArmPunchProfile profile = isLeft ? leftHeavyProfile : rightHeavyProfile;

            // Capture wind-up hip rotation (rotate hips toward the punching arm)
            Quaternion hipStart = hipJoint.targetRotation;
            // Compute relative Y-rotation deltas from the current hip target
            float windupYaw = isLeft ? HipHookRotation : -HipHookRotation;
            float hookYaw   = isLeft ? -HipHookRotation : HipHookRotation;
            Quaternion relativeWindupYaw = Quaternion.Euler(0, windupYaw, 0);
            Quaternion relativeHookYaw   = Quaternion.Euler(0, hookYaw, 0);
            Quaternion hipWindupRot = hipStart * relativeWindupYaw;
            Quaternion hipHookRot   = hipStart * relativeHookYaw;

            // Suppress movement-based hip rotation during heavy punch
            hipRotationSuppressed = true;

            // Disable bone scripts for the punching arm + raise the OTHER arm (wind-up pose)
            if (isLeft)
            {
                LUpperBoneScript.enabled = false;
                LLowerBoneScript.enabled = false;
            }
            else
            {
                RUpperBoneScript.enabled = false;
                RLowerBoneScript.enabled = false;
            }

            // Phase 1: Hip rotates to wind-up, arm goes to wind-up pose
            float elapsed = 0f;
            float total = heavyTravelTime;
            Quaternion startUpper = Quaternion.Euler(profile.windUpUpper);
            Quaternion targetUpper = Quaternion.Euler(profile.punchUpper);
            Quaternion startLower = Quaternion.Euler(profile.windUpLower);
            Quaternion targetLower = Quaternion.Euler(profile.punchLower);

            ConfigurableJoint upperJoint = isLeft ? leftUpperArm : rightUpperArm;
            ConfigurableJoint lowerJoint = isLeft ? leftLowerArm : rightLowerArm;

            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                hipJoint.targetRotation = Quaternion.Slerp(hipStart, hipWindupRot, t);
                yield return null;
            }

            // Phase 2: Hip hooks to opposite side, arm stays extended (hold)
            elapsed = 0f;
            while (elapsed < heavyHoldTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / heavyHoldTime);
                hipJoint.targetRotation = Quaternion.Lerp(hipWindupRot, hipHookRot, t);
                upperJoint.targetRotation = Quaternion.Lerp(startUpper, targetUpper, t);
                lowerJoint.targetRotation = Quaternion.Lerp(startLower, targetLower, t);
                yield return null;
            }

            // Phase 3: Return hip to neutral and arm back to wind-up
            elapsed = 0f;
            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / total);
                hipJoint.targetRotation = Quaternion.Slerp(hipHookRot, hipStart, t);
                upperJoint.targetRotation = Quaternion.Slerp(targetUpper, startUpper, t);
                lowerJoint.targetRotation = Quaternion.Slerp(targetLower, startLower, t);
                yield return null;
            }

            // Re-enable bone scripts
            LUpperBoneScript.enabled = true;
            LLowerBoneScript.enabled = true;
            RUpperBoneScript.enabled = true;
            RLowerBoneScript.enabled = true;

            // Restore hip rotation to current facing direction
            hipRotationSuppressed = false;
            if (currentMoveDir != Vector3.zero)
            {
                hipJoint.targetRotation = Quaternion.Inverse(Quaternion.LookRotation(currentMoveDir));
            }

            isHeavyPunching = false;
        }

        public void OnStunned(float duration)
        {
            SetState(CharacterState.Stunned);
            StartCoroutine(StunRoutine(duration));
        }

        private IEnumerator StunRoutine(float duration)
        {
            ForceDropObject();
            ragdollMaster.EnableFullRagdoll();
            yield return new WaitForSeconds(duration);
            ragdollMaster.EnableActiveRagdoll();
            SetState(CharacterState.Idle);
        }

        public void OnKnockdown(float duration)
        {
            SetState(CharacterState.Ragdoll);
            ForceDropObject();
            ragdollMaster.EnableFullRagdoll();
            StartCoroutine(KnockdownRoutine(duration));
        }

        private IEnumerator KnockdownRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);

            float elapsed = 0f;
            while (elapsed < knockdownDuration && elapsed < 2f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            ragdollMaster.EnableActiveRagdoll();
            coreRigidbody.linearVelocity = Vector3.zero;
            coreRigidbody.angularVelocity = Vector3.zero;
            SetState(CharacterState.Idle);
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
        }
    }
}