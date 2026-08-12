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
        private Rigidbody coreRigidbody;          // mixamorig:Hips' Rigidbody (the physics core)
        private PlayerInput playerInput;
        private PhysicInputHandler inputHandler;
        private ActiveRagdollMaster ragdollMaster;
        private PlayerHealth playerHealth;
        private Animator animatedRig;              // lives on the separate Animated_Character rig
        private ActiveRagdollBalancer balancer;    // on Hips — drives tilt balance; weight blended while turning
        private ConfigurableJoint hipJoint;        // Hips' joint — used for facing rotation

        // --- RAGDOLL BONES (for grabbing arm animation) ---
        // Hand bones whose ConfigurableJoint targetRotation we override while grabbing.
        // Specify their names in the Inspector (e.g. "RightHand", "LeftHand").
        [Header("Ragdoll Arm Bones")]
        [SerializeField] private string[] armBoneNames = new[] { "RightHand", "LeftHand" };
        private ActiveRagdollBone[] armBoneScripts;
        private float[] savedMuscleStrength;

        // --- INPUT ---
        private Vector2 moveInput;
        private bool grabPressed;
        private bool grabReleased;
        private bool punchPressed;
        private bool jumpPressed;
        private bool throwHeld;

        // --- STATE ---
        private CharacterState currentState = CharacterState.Idle;
        private GrabbableObject heldObject;
        private ConfigurableJoint grabJoint;
        private float punchCooldownTimer;
        private bool isGrounded;
        private Vector3 currentMoveDir;

        // Target arm pose while grabbing (raised up) — expressed as a local rotation
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

        [Header("Grab")]
        [SerializeField] private float grabSpring = 1000f;
        [SerializeField] private float grabDamper = 1000f;
        [SerializeField] private float grabMaxForce = 10000f;

        // --- LIFECYCLE ---
        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            inputHandler = GetComponent<PhysicInputHandler>();
            ragdollMaster = GetComponent<ActiveRagdollMaster>();
            playerHealth = GetComponent<PlayerHealth>();

            coreRigidbody = FindCoreRigidbody();
            if (coreRigidbody == null)
                Debug.LogError("PhysicCharacterController: no Rigidbody found under Physic_Character. Add one to mixamorig:Hips.", this);
            hipJoint = coreRigidbody.GetComponent<ConfigurableJoint>();

            if (ragdollMaster != null && ragdollMaster.AnimatedRig != null)
                animatedRig = ragdollMaster.AnimatedRig.GetComponent<Animator>();
        }

        private void Start()
        {
            if (playerInput != null)
                PlayerIndex = playerInput.playerIndex;

            if (coreRigidbody != null)
            {
                balancer = coreRigidbody.GetComponent<ActiveRagdollBalancer>();
            }

            CacheArmBones();
        }

        /// <summary>Finds the ragdoll core body: prefers a Rigidbody named "Hips", falls back to the first child Rigidbody.</summary>
        private Rigidbody FindCoreRigidbody()
        {
            Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in bodies)
                if (rb.transform.name.Contains("Hips"))
                    return rb;
            return bodies.Length > 0 ? bodies[0] : null;
        }

        private void CacheArmBones()
        {
            if (armBoneNames == null || armBoneNames.Length == 0)
                return;

            armBoneScripts = new ActiveRagdollBone[armBoneNames.Length];
            savedMuscleStrength = new float[armBoneNames.Length];

            var allBones = GetComponentsInChildren<ActiveRagdollBone>(true);

            for (int i = 0; i < armBoneNames.Length; i++)
            {
                foreach (var b in allBones)
                {
                    if (b.gameObject.name.Contains(armBoneNames[i]))
                    {
                        armBoneScripts[i] = b;
                        break;
                    }
                }
            }
        }

        private void LateUpdate()
        {
            // While holding an object, override the arm bones' targetRotation so the
            // hands are raised. We do this in LateUpdate (after FixedUpdate) so our
            // value wins over ActiveRagdollBone's per-frame targetRotation write.
            if (heldObject != null && armBoneScripts != null)
            {
                for (int i = 0; i < armBoneScripts.Length; i++)
                {
                    if (armBoneScripts[i] == null) continue;

                    var joint = armBoneScripts[i].GetComponent<ConfigurableJoint>();
                    if (joint == null) continue;

                    // Per the canonical rotation fix: set joint.targetRotation, not transform.
                    joint.targetRotation = Quaternion.Slerp(
                        joint.targetRotation,
                        armRaiseLocalRot,
                        Time.deltaTime * armTargetUpdateSpeed
                    );
                }
            }
        }

        private void Update()
        {
            ReadInput();
            CheckGrounded();
            UpdateState();
            HandlePunchCooldown();

            // Ease the balancer weight: near-zero while turning so the physics body
            // can rotate freely, full when idle so the character stands upright.
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
                    // While grabbing the character can still move and turn.
                    targetAnimator.SetBool("IsWalking", moveInput.magnitude > 0.01f);
                    // Release on grab-release input
                    if (grabReleased && heldObject != null)
                        ReleaseObject();

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
                moveInput     = inputHandler.MoveInput;
                grabPressed   = inputHandler.GrabPressed;
                grabReleased  = inputHandler.GrabReleased;
                punchPressed  = inputHandler.PunchPressed;
                jumpPressed   = inputHandler.JumpPressed;
                throwHeld     = inputHandler.ThrowHeld;
            }

            if (grabPressed && heldObject == null)
                TryGrabObject();
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

            Vector3 forward = Camera.main ? Camera.main.transform.forward : Vector3.forward;
            Vector3 right = Camera.main ? Camera.main.transform.right : Vector3.right;
            forward.y = right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;
            Vector3 targetVel = moveDir * moveSpeed;
            targetVel.y = coreRigidbody.linearVelocity.y;

            coreRigidbody.linearVelocity = Vector3.Lerp(coreRigidbody.linearVelocity, targetVel, Time.fixedDeltaTime * 10f);
            currentMoveDir = moveDir;

            float forwardSpeed = Mathf.Abs(Vector3.Dot(coreRigidbody.linearVelocity, transform.forward));
            float rightSpeed = Mathf.Abs(Vector3.Dot(coreRigidbody.linearVelocity, transform.right));
            float highestSpeed = forwardSpeed > rightSpeed ? forwardSpeed : rightSpeed;
            if (highestSpeed > 0.1f)
            {
                float upwardLift = highestSpeed * 5f;
                coreRigidbody.AddForce(Vector3.up * upwardLift, ForceMode.Impulse);
            }

            if (jumpPressed && isGrounded)
                coreRigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        private void HandleRotation()
        {
            if (coreRigidbody == null) return;
            if (currentMoveDir == Vector3.zero) return;

            // Canonical rotation fix: set targetRotation on the ConfigurableJoint,
            // NOT transform.rotation. The joint solver applies it cleanly.
            Quaternion targetRot = Quaternion.LookRotation(currentMoveDir);
            hipJoint.targetRotation = Quaternion.Inverse(targetRot);
        }

        // --- GRAB / DROP ---
        private void TryGrabObject()
        {
            // Use the HIP body position for grab detection — the root can be far
            // from the ragdoll's actual body since the root is just an
            // orchestrator with no Rigidbody.
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

            // Attach the grab joint to the FIRST hand bone that has a Rigidbody.
            Rigidbody handBody = null;
            for (int i = 0; i < armBoneScripts.Length; i++)
            {
                if (armBoneScripts[i] != null)
                {
                    var rb = armBoneScripts[i].GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        handBody = rb;
                        break;
                    }
                }
            }

            if (handBody == null)
            {
                Debug.LogError("Grab failed: no arm bone with Rigidbody found. Check Arm Bone Names in Inspector.", this);
                heldObject = null;
                return;
            }

            // Create the grab joint on the hand bone's body.
            grabJoint = handBody.gameObject.AddComponent<ConfigurableJoint>();
            grabJoint.connectedBody = heldObject.Rigidbody;
            grabJoint.autoConfigureConnectedAnchor = true;

            // Anchor at the hand bone's local origin.
            grabJoint.anchor = Vector3.zero;
            grabJoint.connectedAnchor = Vector3.zero;

            // Lock all linear motion — object sticks to hand.
            grabJoint.xMotion = ConfigurableJointMotion.Locked;
            grabJoint.yMotion = ConfigurableJointMotion.Locked;
            grabJoint.zMotion = ConfigurableJointMotion.Locked;

            // Free angular — let the object dangle naturally from the hand.
            grabJoint.angularXMotion = ConfigurableJointMotion.Free;
            grabJoint.angularYMotion = ConfigurableJointMotion.Free;
            grabJoint.angularZMotion = ConfigurableJointMotion.Free;

            // Position spring-damper to hold the object against gravity.
            // Without this drive, gravity pulls the object away from the hand.
            JointDrive drive = new JointDrive
            {
                positionSpring = grabSpring,
                positionDamper = grabDamper,
                maximumForce = grabMaxForce
            };
            grabJoint.xDrive = drive;
            grabJoint.yDrive = drive;
            grabJoint.zDrive = drive;

            grabJoint.breakForce = 1500f;
            grabJoint.breakTorque = 1500f;
            grabJoint.enablePreprocessing = false;

            // Ignore collisions to prevent clipping stutter
            Physics.IgnoreCollision(coreRigidbody.GetComponent<Collider>(), heldObject.GetComponent<Collider>(), true);

            // Weaken arm bones' tracking so our LateUpdate targetRotation override
            // isn't immediately overwritten by ActiveRagdollBone's FixedUpdate.
            for (int i = 0; i < armBoneScripts.Length; i++)
            {
                if (armBoneScripts[i] != null)
                {
                    savedMuscleStrength[i] = 1f;
                    armBoneScripts[i].SetMuscleStrength(0.1f);
                }
            }

            SetState(CharacterState.Grabbing);

            // Suppress the ragdoll balancer while arms are raised
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

            // Restore arm bone muscle strength
            for (int i = 0; i < armBoneScripts.Length; i++)
            {
                if (armBoneScripts[i] != null)
                    armBoneScripts[i].SetMuscleStrength(savedMuscleStrength[i]);
            }

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

            for (int i = 0; i < armBoneScripts.Length; i++)
            {
                if (armBoneScripts[i] != null)
                    armBoneScripts[i].SetMuscleStrength(savedMuscleStrength[i]);
            }

            if (balancer != null)
                balancer.weight = 1f;
        }

        // --- COMBAT ---
        private void HandlePunchCooldown()
        {
            if (punchCooldownTimer > 0f)
                punchCooldownTimer -= Time.deltaTime;

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
                {
                    grabbable.ApplyForce(dir * punchForce);
                }
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
