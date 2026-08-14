using UnityEngine;
using UnityEngine.InputSystem;

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
        private Animator animatedRig;
        private ActiveRagdollBalancer balancer;
        private ConfigurableJoint hipJoint;

        // --- HAND REFERENCES (assigned in Inspector) ---
        // Each hand must have: Transform + Rigidbody + ConfigurableJoint + ActiveRagdollBone + Collider.
        [Header("Hand Bones")]
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
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
        private Rigidbody rightHandRb;

        // --- INPUT ---
        private Vector2 moveInput;
        private bool grabPressed;
        private bool grabReleased;
        private bool punchPressed;
        private bool jumpPressed;
        private bool sprintPressed;
        private bool throwHeld;

        // --- STATE ---
        private CharacterState currentState = CharacterState.Idle;
        private GrabbableObject heldObject;
        private ConfigurableJoint grabJoint;
        private float punchCooldownTimer;
        private bool isGrounded;
        private bool isSprinting;
        private Vector3 currentMoveDir;

        // Target arm pose while grabbing (raised up)
        private Quaternion armRaiseLocalRot = Quaternion.Euler(90f, 0f, 0f);

        // --- SETTINGS ---
        [Header("Reference")]
        [SerializeField] private Animator targetAnimator;
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float jumpForce = 6f;
        [SerializeField] private LayerMask groundLayer = 1 << 0;
        [SerializeField] private float groundCheckDistance = 0.2f;
        [SerializeField] private float hipHeight = 0.95f;
        [SerializeField] private float balancerWeightMoving = 0.3f;
        [SerializeField] private float balancerBlendSpeed = 6f;
        [SerializeField] private float armTargetUpdateSpeed = 10f;

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
        }

        private void Update()
        {
            ReadInput();
            CheckGrounded();
            UpdateState();
            HandlePunchCooldown();

            if (balancer != null)
            {
                float target = (currentState == CharacterState.Moving) ? balancerWeightMoving : 1f;
                balancer.weight = Mathf.Lerp(balancer.weight, target, Time.deltaTime * balancerBlendSpeed);
            }
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
        }

        // --- INPUT HANDLING ---
        private void ReadInput()
        {
            if (inputHandler != null)
            {
                moveInput = inputHandler.MoveInput;
                grabPressed = inputHandler.GrabPressed;
                grabReleased = inputHandler.GrabReleased;
                punchPressed = inputHandler.PunchPressed;
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

            if (grabReleased)
                ReleaseObject();

            SetState(moveInput.magnitude > 0.1f ? CharacterState.Moving : CharacterState.Idle);
        }

        private void SetState(CharacterState newState)
        {
            if (currentState == newState) return;
            currentState = newState;
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
                coreRigidbody.AddForce(Vector3.up * highestSpeed * (isSprinting ? 3.5f : 5f), ForceMode.Impulse);
            }

            if (jumpPressed && isGrounded)
                coreRigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        private void HandleRotation()
        {
            if (coreRigidbody == null) return;
            if (currentMoveDir == Vector3.zero) return;

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
                GrabObject(closest);
        }

        private void GrabObject(GrabbableObject obj)
        {
            heldObject = obj;
            obj.GrabByPlayer(this);

            // Choose the first available hand bone that has a Rigidbody.
            Rigidbody handBody = rightHandRb ?? leftHandRb;

            if (handBody == null)
            {
                Debug.LogError("Grab failed: no hand bone with Rigidbody assigned. Assign Left/Right Hand in Inspector.", this);
                heldObject = null;
                obj.ReleaseByPlayer();
                return;
            }

            // Create the grab joint on the hand bone.
            grabJoint = handBody.gameObject.AddComponent<ConfigurableJoint>();
            grabJoint.connectedBody = heldObject.Rigidbody;

            // Enable connected target auto? set to false to manually configure.
            grabJoint.autoConfigureConnectedAnchor = true;
            //grabJoint.connectedAnchor = new Vector3(0f, 1f, 0f);

            // Anchor at the hand bone's local origin (hand center).
            grabJoint.anchor = Vector3.zero;
            grabJoint.connectedAnchor = Vector3.zero;

            // Lock linear motion — object sticks to hand.
            grabJoint.xMotion = ConfigurableJointMotion.Locked;
            grabJoint.yMotion = ConfigurableJointMotion.Locked;
            grabJoint.zMotion = ConfigurableJointMotion.Locked;

            // Free angular — let the object dangle naturally.
            grabJoint.angularXMotion = ConfigurableJointMotion.Free;
            grabJoint.angularYMotion = ConfigurableJointMotion.Free;
            grabJoint.angularZMotion = ConfigurableJointMotion.Free;

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

            // Ignore collisions
            Physics.IgnoreCollision(handBody.GetComponent<Collider>(), heldObject.GetComponent<Collider>(), true);

            SetState(CharacterState.Grabbing);
            if (balancer != null)
                balancer.weight *= 0.5f;
        }

        private void ReleaseObject()
        {
            if (heldObject == null) return;

            if (grabJoint != null)
            {
                Destroy(grabJoint);
                grabJoint = null;
            }

            heldObject.ReleaseByPlayer();
            heldObject = null;
            ResetBothArms();
            if (balancer != null)
                balancer.weight = 1f;

            SetState(CharacterState.Idle);
        }

        private void ForceDropObject()
        {
            if (heldObject == null) return;

            if (grabJoint != null)
            {
                Destroy(grabJoint);
                grabJoint = null;
            }

            heldObject.ReleaseByPlayer();
            heldObject = null;
            ResetBothArms();
            if (balancer != null)
                balancer.weight = 1f;
        }

        // --- ARM MUSCLE HELPERS ---
        public void RaiseBothArms()
        {
            float grabSpringStrength = 12000f;
            float grabDamper = 120f;

            // Disable ActiveRagdollBone Script so the animation from animated rig doesn't override target rotation
            LUpperBoneScript.enabled = false;
            LLowerBoneScript.enabled = false;
            RUpperBoneScript.enabled = false;
            RLowerBoneScript.enabled = false;

            // Boost X and YZ joint drives for both arms
            SetXYZJointStrength(leftUpperArm, grabSpringStrength, grabDamper);
            SetXYZJointStrength(leftLowerArm, grabSpringStrength, grabDamper);
            SetXYZJointStrength(rightUpperArm, grabSpringStrength, grabDamper);
            SetXYZJointStrength(rightLowerArm, grabSpringStrength, grabDamper);

            // Force target velocities to zero to stop current physics momentum
            leftUpperArm.targetAngularVelocity = Vector3.zero;
            rightUpperArm.targetAngularVelocity = Vector3.zero;

            // Assign target rotations
            leftUpperArm.targetRotation = Quaternion.Euler(0, -90, 60);
            leftLowerArm.targetRotation = Quaternion.Euler(330, 0, 0);

            rightUpperArm.targetRotation = Quaternion.Euler(0, 90, -60);
            rightLowerArm.targetRotation = Quaternion.Euler(330, 0, 0);
        }

        public void ResetBothArms()
        {
            // Enable ActiveRagdollBone Script
            LUpperBoneScript.enabled = true;
            LLowerBoneScript.enabled = true;
            RUpperBoneScript.enabled = true;
            RLowerBoneScript.enabled = true;
            // Revert Left Arm
            leftUpperArm.angularXDrive = originalRightUpperX; leftUpperArm.angularYZDrive = originalLeftUpperYZ;
            leftLowerArm.angularXDrive = originalLeftLowerX; leftLowerArm.angularYZDrive = originalLeftLowerYZ;

            // Revert Right Arm
            rightUpperArm.angularXDrive = originalRightUpperX; rightUpperArm.angularYZDrive = originalRightUpperYZ;
            rightLowerArm.angularXDrive = originalRightLowerX; rightLowerArm.angularYZDrive = originalRightLowerYZ;

            // Reset target rotations
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

        // --- COMBAT ---
        private void HandlePunchCooldown()
        {
            if (punchCooldownTimer > 0f) punchCooldownTimer -= Time.deltaTime;

            if (punchPressed && punchCooldownTimer <= 0f && currentState != CharacterState.Punching)
                Punch();
        }

        private void Punch()
        {
            SetState(CharacterState.Punching);
            punchCooldownTimer = 0.5f;

            Vector3 origin = coreRigidbody.position + Vector3.up * 1f;
            Vector3 dir = transform.forward;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, punchRange))
            {
                var otherHealth = hit.collider.GetComponentInParent<PlayerHealth>();
                if (otherHealth != null && otherHealth != playerHealth)
                {
                    otherHealth.TakeDamage(punchDamage, dir * punchForce, PlayerIndex);
                }

                var grabbable = hit.collider.GetComponentInParent<GrabbableObject>();
                if (grabbable != null && !grabbable.IsHeld)
                    grabbable.ApplyForce(dir * punchForce);
            }

            if (animatedRig != null)
                animatedRig.SetTrigger("Punch");
        }

        // --- STUN / RAGDOLL ---
        public void OnStunned(float duration)
        {
            SetState(CharacterState.Stunned);
            StartCoroutine(StunRoutine(duration));
        }

        private System.Collections.IEnumerator StunRoutine(float duration)
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

        private System.Collections.IEnumerator KnockdownRoutine(float duration)
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