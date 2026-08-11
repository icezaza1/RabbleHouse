using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;

namespace RabbleHouse
{
    /// <summary>
    /// Main player controller: movement, state machine, grab/throw/punch.
    /// Designed for Party Animals-style physics combat.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public enum PlayerState
        {
            Idle,
            Moving,
            Stunned,
            Ragdoll,
            Grabbing,
            Throwing,
            Punching
        }

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 15f;
        [SerializeField] private float jumpForce = 6f;
        [SerializeField] private LayerMask groundLayer = 1 << 0;
        [SerializeField] private float groundCheckDistance = 0.1f;

        [Header("Grab / Throw")]
        [SerializeField] private Transform handTransform;
        [SerializeField] private float grabRange = 1.5f;
        [SerializeField] private float grabOffset = 0.3f;
        [SerializeField] private LayerMask grabbableLayer = ~0;
        [SerializeField] private float throwForce = 20f;
        [SerializeField] private float throwChargeTime = 1f;
        [SerializeField] private float maxThrowForce = 40f;

        [Header("Punch")]
        [SerializeField] private float punchCooldown = 0.5f;
        [SerializeField] private float punchForce = 15f;
        [SerializeField] private float punchRange = 1f;
        [SerializeField] private int punchDamage = 10;

        [Header("Stun / Recovery")]
        [SerializeField] private float stunDuration = 2f;
        [SerializeField] private float knockdownDuration = 1.2f;
        [SerializeField] private float recoveryTime = 3f;

        // Public read-only accessors (used by other systems)
        public PlayerState CurrentState => currentState;
        public bool IsHoldingObject => heldObject != null;
        public int PlayerIndex { get; set; } = 0;
        public Rigidbody Rigidbody => rb;
        public RagdollController RagdollController => ragdollController;
        public float StunDuration => stunDuration;

        // Components
        private Rigidbody rb;
        private CapsuleCollider capsule;
        private PlayerInput playerInput;
        private RagdollController ragdollController;
        private HipRagdollController hipRagdollController;
        private PlayerHealth playerHealth;
        private Animator animator;
        private AIController aiController;

        // Input
        private Vector2 moveInput;
        private Vector2 lookInput;
        private bool grabPressed;
        private bool grabReleased;
        private bool punchPressed;
        private bool jumpPressed;
        private bool throwCharging;
        private float throwChargeStartTime;
        private bool ragdollPressed;

        // State
        private PlayerState currentState = PlayerState.Idle;
        private GrabbableObject heldObject;
        private ConfigurableJoint grabJoint;
        private float punchCooldownTimer;
        private bool isGrounded;

        // Events
        public Action<int, GrabbableObject> OnObjectGrabbed;
        public Action<int, GrabbableObject> OnObjectThrown;
        public Action<int> OnPunch;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            playerInput = GetComponent<PlayerInput>();
            ragdollController = GetComponent<RagdollController>();
            playerHealth = GetComponent<PlayerHealth>();
            animator = GetComponentInChildren<Animator>();
            aiController = GetComponent<AIController>();

            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            if (capsule == null)
            {
                capsule = gameObject.AddComponent<CapsuleCollider>();
            }
            if (ragdollController == null)
            {
                ragdollController = gameObject.AddComponent<RagdollController>();
            }

            // NEW: Get the HipRagdollController component
            hipRagdollController = GetComponentInChildren<HipRagdollController>();
            // If missing, try fallback to legacy RagdollController component
            if (hipRagdollController == null)
            {
                Debug.LogWarning("HipRagdollController not found – ensure Hip GameObject has the component.");
                // optionally add it automatically if you want:
                // hipRagdollController = gameObject.AddComponent<HipRagdollController>();
            }
            if (playerHealth == null)
            {
                playerHealth = gameObject.AddComponent<PlayerHealth>();
            }

            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.mass = 3f;
        }

        private void Start()
        {
            // AI opponents keep the index assigned by GameManager at spawn time.
            if (playerInput != null && aiController == null
                && playerInput.actions != null && playerInput.actions.FindAction("Move") != null)
            {
                PlayerIndex = playerInput.playerIndex;
            }
        }

        private void Update()
        {
            ReadInput();
            CheckGrounded();
            UpdateState();
            HandlePunchCooldown();
        }

        private void FixedUpdate()
        {
            switch (currentState)
            {
                case PlayerState.Idle:
                    ragdollController.animator.SetBool("IsWalking", false);
                    ForceStun();
                    break;
                case PlayerState.Moving:
                    ragdollController.animator.SetBool("IsWalking", true);
                    HandleMovement();
                    HandleRotation();
                    break;
                case PlayerState.Grabbing:
                    HandleGrab();
                    break;
            }
        }

        private void ReadInput()
        {
            // AI-controlled characters never read from PlayerInput, so they
            // cannot steal keyboards/gamepads from the human player.
            if (aiController != null && aiController.enabled)
            {
                ReadAIInput();
                return;
            }

            if (playerInput == null || playerInput.actions == null)
            {
                // No PlayerInput/actions assigned. NOTE: the legacy fallback
                // (ReadLegacyInput) is intentionally disabled — this project
                // runs Input System ONLY (activeInputHandler: 1), so
                // UnityEngine.Input APIs throw. The PlayerBean prefab carries
                // a PlayerInput component wired to InputSystem_Actions, so this
                // branch should not be reached in normal play.
                return;
            }

            moveInput = playerInput.actions["Move"].ReadValue<Vector2>();
            lookInput = playerInput.actions["Look"].ReadValue<Vector2>();
            grabPressed = playerInput.actions["Grab"].WasPressedThisFrame();
            grabReleased = playerInput.actions["Grab"].WasReleasedThisFrame();
            punchPressed = playerInput.actions["Punch"].WasPressedThisFrame();
            jumpPressed = playerInput.actions["Jump"].WasPressedThisFrame();
            ragdollPressed = playerInput.actions["ForcedRagdoll"].WasPressedThisFrame();

            if (grabPressed && heldObject == null) TryGrabObject();

            if (playerInput.actions.FindAction("Throw") != null)
            {
                if (playerInput.actions["Throw"].IsPressed() && heldObject != null)
                {
                    if (!throwCharging)
                    {
                        throwCharging = true;
                        throwChargeStartTime = Time.time;
                        SetState(PlayerState.Throwing);
                    }
                }
                else if (throwCharging)
                {
                    throwCharging = false;
                    if (heldObject != null)
                    {
                        ThrowObject();
                    }
                }
            }
        }

        private void ReadAIInput()
        {
            moveInput = aiController.MoveInput;
            lookInput = aiController.MoveInput;
            grabPressed = aiController.GrabPressed;
            grabReleased = aiController.GrabReleased;
            punchPressed = aiController.PunchPressed;
            jumpPressed = aiController.JumpPressed;

            if (aiController.ThrowHeld && heldObject != null)
            {
                if (!throwCharging)
                {
                    throwCharging = true;
                    throwChargeStartTime = Time.time;
                    SetState(PlayerState.Throwing);
                }
            }
            else if (throwCharging)
            {
                throwCharging = false;
                if (heldObject != null)
                {
                    ThrowObject();
                }
            }
        }

        private void CheckGrounded()
        {
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            isGrounded = Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundLayer);
        }

        private void UpdateState()
        {
            if (currentState == PlayerState.Stunned || currentState == PlayerState.Ragdoll)
                return;

            if (currentState == PlayerState.Punching)
            {
                if (punchCooldownTimer <= 0)
                    SetState(PlayerState.Idle);
                return;
            }

            SetState(moveInput.magnitude > 0.01f ? PlayerState.Moving : PlayerState.Idle);
        }

        private void HandleMovement()
        {
            // CRITICAL: never overwrite the root body's velocity while the
            // ragdoll is still simulating (Stunned/Ragdoll) — it fights the
            // joint chain and causes the violent shaking.
            if (currentState == PlayerState.Stunned || currentState == PlayerState.Ragdoll)
                return;

            if (!isGrounded) return;

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (Camera.main != null)
            {
                forward = Camera.main.transform.forward;
                right = Camera.main.transform.right;
                forward.y = 0;
                right.y = 0;
                forward.Normalize();
                right.Normalize();
            }

            Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;
            Vector3 targetVelocity = moveDirection * moveSpeed;
            targetVelocity.y = rb.linearVelocity.y;

            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, Time.fixedDeltaTime * 10f);

            if (jumpPressed && isGrounded)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
        }

        private void HandleRotation()
        {
            if (lookInput.magnitude < 0.1f) return;

            Vector3 lookDirection = new Vector3(lookInput.x, 0, lookInput.y).normalized;
            if (lookDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
            }
        }

        private void HandleGrab()
        {
            if (grabReleased && heldObject != null)
            {
                ReleaseObject();
            }
        }

        private void TryGrabObject()
        {
            Vector3 searchOrigin = handTransform != null ? handTransform.position : transform.position + Vector3.up * 1f;
            Collider[] hits = Physics.OverlapSphere(searchOrigin, grabRange, grabbableLayer);

            GrabbableObject closest = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var grabbable = hit.GetComponent<GrabbableObject>();
                if (grabbable != null && !grabbable.IsHeld)
                {
                    float dist = Vector3.Distance(searchOrigin, grabbable.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = grabbable;
                    }
                }
            }

            if (closest != null)
            {
                GrabObject(closest);
            }
        }

        private void GrabObject(GrabbableObject obj)
        {
            heldObject = obj;
            obj.GrabByPlayer(this);

            // Joint connects this player's root to the furniture
            grabJoint = gameObject.AddComponent<ConfigurableJoint>();
            grabJoint.connectedBody = obj.Rigidbody;
            grabJoint.autoConfigureConnectedAnchor = false;
            grabJoint.connectedAnchor = Vector3.zero;
            grabJoint.anchor = handTransform != null ? transform.InverseTransformPoint(handTransform.position) : Vector3.up;
            grabJoint.xMotion = ConfigurableJointMotion.Locked;
            grabJoint.yMotion = ConfigurableJointMotion.Locked;
            grabJoint.zMotion = ConfigurableJointMotion.Locked;
            grabJoint.angularXMotion = ConfigurableJointMotion.Locked;
            grabJoint.angularYMotion = ConfigurableJointMotion.Locked;
            grabJoint.angularZMotion = ConfigurableJointMotion.Locked;

            OnObjectGrabbed?.Invoke(PlayerIndex, obj);
            SetState(PlayerState.Grabbing);
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

            SetState(PlayerState.Idle);
        }

        private void ThrowObject()
        {
            if (heldObject == null) return;

            float chargePercent = Mathf.Clamp01((Time.time - throwChargeStartTime) / throwChargeTime);
            float force = Mathf.Lerp(throwForce, maxThrowForce, chargePercent);

            Vector3 throwDirection = handTransform != null ? handTransform.forward : transform.forward;
            if (lookInput.magnitude > 0.1f)
            {
                throwDirection = new Vector3(lookInput.x, 0, lookInput.y).normalized;
            }

            if (grabJoint != null)
            {
                Destroy(grabJoint);
                grabJoint = null;
            }

            GrabbableObject thrown = heldObject;
            thrown.ThrowByDirection(throwDirection * force);
            heldObject = null;

            OnObjectThrown?.Invoke(PlayerIndex, thrown);
            SetState(PlayerState.Idle);
        }

        private void HandlePunchCooldown()
        {
            if (punchCooldownTimer > 0)
            {
                punchCooldownTimer -= Time.deltaTime;
            }

            if (punchPressed && punchCooldownTimer <= 0 && currentState != PlayerState.Punching)
            {
                Punch();
            }
        }

        private void Punch()
        {
            SetState(PlayerState.Punching);
            punchCooldownTimer = punchCooldown;

            Vector3 punchOrigin = handTransform != null ? handTransform.position : transform.position + Vector3.up * 1f;
            Vector3 punchDir = handTransform != null ? handTransform.forward : transform.forward;
            if (lookInput.magnitude > 0.1f)
            {
                punchDir = new Vector3(lookInput.x, 0, lookInput.y).normalized;
            }

            if (Physics.Raycast(punchOrigin, punchDir, out RaycastHit hit, punchRange))
            {
                var otherHealth = hit.collider.GetComponentInParent<PlayerHealth>();
                if (otherHealth != null && otherHealth != playerHealth)
                {
                    otherHealth.TakeDamage(punchDamage, punchDir * punchForce, PlayerIndex);
                }

                var grabbable = hit.collider.GetComponentInParent<GrabbableObject>();
                if (grabbable != null && !grabbable.IsHeld)
                {
                    grabbable.ApplyForce(punchDir * punchForce);
                }
            }

            OnPunch?.Invoke(PlayerIndex);

            if (animator != null)
            {
                animator.SetTrigger("Punch");
            }
        }

        public void TryGrab()
        {
            if (heldObject == null)
            {
                TryGrabObject();
            }
        }

        public void SetState(PlayerState newState)
        {
            if (currentState == newState) return;
            currentState = newState;

            if (animator != null)
            {
                animator.SetInteger("State", (int)newState);
            }
        }

        public void OnStunned(float duration)
        {
            SetState(PlayerState.Stunned);
            StartCoroutine(StunRoutine(duration));
        }

        private IEnumerator StunRoutine(float duration)
        {
            ForceDropObject();
            hipRagdollController?.SetRagdollMode(true);
            yield return new WaitForSeconds(duration);
            hipRagdollController?.SetRagdollMode(false);
            if (hipRagdollController == null)
                ragdollController.EnableRagdoll(false);
            SetState(PlayerState.Idle);
        }

        public void OnKnockdown(float duration)
        {
            SetState(PlayerState.Ragdoll);
            ForceDropObject();
            hipRagdollController?.SetRagdollMode(true);
            if (hipRagdollController == null)
                ragdollController.ForceEnableRagdoll(true, 1.5f);

            // Wait until the ragdoll actually settles (velocity ~0) before recovering,
            // then sync root and hand control back.
            StartCoroutine(KnockdownRoutine(duration));
        }

        private IEnumerator KnockdownRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);

            // Give the ragdoll time to settle (velocity ~0) so the root sync
            // in EnableRagdoll(false) captures a stable pose, not a mid-tumble one.
            float elapsed = 0f;
            Rigidbody[] limbRbs = hipRagdollController != null ? null : ragdollController.RagdollRigidbodies;
            if (hipRagdollController == null)
            {
                if (ragdollController.RagdollRigidbodies != null)
                {
                    limbRbs = ragdollController.RagdollRigidbodies;
                }
            }
            else
            {
                Debug.LogWarning("HipRagdollController does not expose limb rigidbodies for settling check.");
            }

            while (elapsed < 1.5f)
            {
                if (limbRbs != null)
                {
                    float totalV = 0f;
                    foreach (var r in limbRbs)
                    {
                        if (r != null) totalV += r.linearVelocity.magnitude + r.angularVelocity.magnitude;
                    }
                    if (totalV < 1f) break; // settled
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            hipRagdollController?.SetRagdollMode(false);
            if (hipRagdollController == null)
                ragdollController.EnableRagdoll(false);
            SetState(PlayerState.Idle);
            // Ensure the root body has no leftover velocity so movement resumes cleanly
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        public void ForceDropObject()
        {
            if (heldObject != null)
            {
                if (grabJoint != null)
                {
                    Destroy(grabJoint);
                    grabJoint = null;
                }
                heldObject.ReleaseByPlayer();
                heldObject = null;
            }
        }

        /// <summary>
        /// Called by GrabbableObject when it releases itself (e.g. after being thrown).
        /// </summary>
        public void ReleaseObject(GrabbableObject obj)
        {
            if (heldObject == obj)
            {
                if (grabJoint != null)
                {
                    Destroy(grabJoint);
                    grabJoint = null;
                }
                heldObject = null;
            }
        }

        public void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 origin = handTransform != null ? handTransform.position : transform.position + Vector3.up * 1f;
            Gizmos.DrawWireSphere(origin, grabRange);
        }

        private void ForceStun()
        {
            if (ragdollPressed)
                playerHealth.ApplyStun();
        }
    }
}