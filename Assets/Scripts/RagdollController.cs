using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RabbleHouse
{
    /// <summary>
    /// Manages player ragdoll physics and enabling/disabling.
    /// Handles smooth transitions between animation and ragdoll states.
    /// </summary>
    public class RagdollController : MonoBehaviour
    {
        [Header("Ragdoll Setup")]
        [SerializeField] private Collider[] ragdollColliders;
        [SerializeField] private Rigidbody[] ragdollRigidbodies;
        [SerializeField] public Animator animator;

        // CharacterJoints on child limbs. Joint has no 'enabled' property —
        // the constraint is toggled by (re)assigning connectedBody. When
        // ragdoll is off, nulling connectedBody detaches the kinematic limb
        // from the dynamic root so the root isn't pinned in place.
        private CharacterJoint[] ragdollJoints;
        private Rigidbody[] jointConnectedBodies;

        // State tracking
        private bool isRagdollEnabled = false;
        private Vector3[] originalPositions;
        private Quaternion[] originalRotations;
        private bool[] enabledStates;

        // Exposed so controllers can poll ragdoll velocity before recovering
        public Rigidbody[] RagdollRigidbodies => ragdollRigidbodies;

        // Events
        public System.Action<bool> OnRagdollStateChanged;

        public bool IsRagdoll => isRagdollEnabled;

        private void Awake()
        {
            // Auto-discover ragdoll parts on children only — the root
            // body (the capsule/collider that PlayerController moves) must
            // be excluded, otherwise EnableRagdoll(false) freezes movement.
            if (ragdollColliders.Length == 0)
            {
                var all = GetComponentsInChildren<Collider>(true);
                var rootCol = GetComponent<Collider>();
                var list = new List<Collider>();
                foreach (var c in all)
                {
                    if (c != rootCol && c.gameObject != gameObject)
                        list.Add(c);
                }
                ragdollColliders = list.ToArray();
            }

            if (ragdollRigidbodies.Length == 0)
            {
                var all = GetComponentsInChildren<Rigidbody>(true);
                var rootRb = GetComponent<Rigidbody>();
                var list = new List<Rigidbody>();
                foreach (var r in all)
                {
                    if (r != rootRb && r.gameObject != gameObject)
                        list.Add(r);
                }
                ragdollRigidbodies = list.ToArray();
            }

            // Auto-discover CharacterJoints on child limbs.
            // Joint has no 'enabled' property — we toggle the constraint by
            // nulling/restoring connectedBody. Save the originals so they can
            // be restored when the ragdoll re-activates.
            ragdollJoints = GetComponentsInChildren<CharacterJoint>(true);
            jointConnectedBodies = new Rigidbody[ragdollJoints.Length];
            for (int i = 0; i < ragdollJoints.Length; i++)
            {
                if (ragdollJoints[i] != null)
                    jointConnectedBodies[i] = ragdollJoints[i].connectedBody;
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator != null)
            {
                animator.enabled = true;
            }

            // Initialize state tracking
            InitializeRagdollState();
        }

        private void Start()
        {
            // Save initial states (auto-discovers child ragdoll parts if arrays are empty)
            SaveInitialStates();

            // Force-disable the ragdoll at spawn. The prefab is saved with
            // limb rigidbodies active + gravity on and joints enabled, so
            // without this the character collapses into a lifeless pile
            // (joints pin the root via kinematic limbs). ForceRigidState
            // must run AFTER SaveInitialStates so the arrays are populated.
            ForceRigidState();
        }

        /// <summary>
        /// Forcefully disables the ragdoll (kinematic limbs, colliders off,
        /// gravity off, joints disabled) regardless of the prefab's saved
        /// state. Called at spawn so the character always starts upright.
        /// </summary>
        private void ForceRigidState()
        {
            for (int i = 0; i < ragdollColliders.Length; i++)
            {
                if (ragdollColliders[i] != null)
                    ragdollColliders[i].enabled = false;
            }

            for (int i = 0; i < ragdollRigidbodies.Length; i++)
            {
                if (ragdollRigidbodies[i] != null)
                {
                    ragdollRigidbodies[i].isKinematic = true;
                    ragdollRigidbodies[i].useGravity = false;
                    ragdollRigidbodies[i].linearVelocity = Vector3.zero;
                    ragdollRigidbodies[i].angularVelocity = Vector3.zero;
                }
            }

            // CRITICAL: detach all CharacterJoints so kinematic limb bodies
            // don't anchor the root rigidbody via the joint constraint.
            SetJointsActive(false);

            if (animator != null)
                animator.enabled = true;

            isRagdollEnabled = false;
        }

        /// <summary>
        /// Toggles every CharacterJoint's constraint by (re)assigning
        /// connectedBody. Joint has no 'enabled' property — a joint whose
        /// connectedBody is null anchors to world space, which on a
        /// kinematic limb has no effect, so the dynamic root is free.
        /// </summary>
        private void SetJointsActive(bool active)
        {
            for (int i = 0; i < ragdollJoints.Length; i++)
            {
                if (ragdollJoints[i] != null)
                    ragdollJoints[i].connectedBody = active ? jointConnectedBodies[i] : null;
            }
        }

        private void Update()
        {
            // Intentionally empty: ragdoll activation and recovery are
            // driven entirely by PlayerController via the public API
            // (EnableRagdoll / ForceEnableRagdoll). The old auto-detection
            // in this method raced with PlayerController's coroutines and
            // caused the character to lose input control after ragdoll.
        }

        private void InitializeRagdollState()
        {
            originalPositions = new Vector3[ragdollColliders.Length];
            originalRotations = new Quaternion[ragdollColliders.Length];
            enabledStates = new bool[ragdollColliders.Length];
        }

        private void SaveInitialStates()
        {
            for (int i = 0; i < ragdollColliders.Length; i++)
            {
                if (ragdollColliders[i] != null)
                {
                    originalPositions[i] = ragdollColliders[i].transform.localPosition;
                    originalRotations[i] = ragdollColliders[i].transform.localRotation;
                    enabledStates[i] = ragdollColliders[i].enabled;
                }
            }
        }

        /// <summary>
        /// Toggle ragdoll on/off.  When off: colliders disabled, limbs
        /// kinematic, gravity off, joints disabled.  When on: the reverse.
        /// </summary>
        public void EnableRagdoll(bool enable)
        {
            if (isRagdollEnabled == enable) return;

            isRagdollEnabled = enable;
            OnRagdollStateChanged?.Invoke(isRagdollEnabled);

            // Colliders
            for (int i = 0; i < ragdollColliders.Length; i++)
            {
                if (ragdollColliders[i] != null)
                    ragdollColliders[i].enabled = enable;
            }

            // Rigidbodies
            for (int i = 0; i < ragdollRigidbodies.Length; i++)
            {
                if (ragdollRigidbodies[i] != null)
                {
                    ragdollRigidbodies[i].isKinematic = !enable;
                    ragdollRigidbodies[i].useGravity = enable;
                }
            }

            // Joints — detach when ragdoll off (kinematic bodies would
            // anchor the root), re-attach when ragdoll on (bodies are
            // dynamic and joints let them swing naturally).
            SetJointsActive(enable);

            // Animator
            if (animator != null)
                animator.enabled = !enable;

            if (!enable)
                ResetToInitialStates();
        }

        private void ResetToInitialStates()
        {
            // Re-sync the ROOT transform to where the ragdoll actually ended up.
            // If we snap to originalPositions (spawn pose), the character
            // teleports back to its spawn point — losing all ragdoll progress.
            if (ragdollRigidbodies.Length > 0)
            {
                Vector3 avgPos = Vector3.zero;
                Quaternion avgRot = Quaternion.identity;
                int count = 0;

                for (int i = 0; i < ragdollRigidbodies.Length; i++)
                {
                    var rb = ragdollRigidbodies[i];
                    if (rb == null) continue;

                    avgPos += rb.transform.position;
                    avgRot = Quaternion.Lerp(avgRot, rb.transform.rotation, 0.5f);
                    count++;
                }

                if (count > 0)
                {
                    avgPos /= count;
                    transform.position = avgPos;
                    transform.rotation = avgRot;
                }
            }

            // Reset collider local transforms to the saved T-pose
            for (int i = 0; i < ragdollColliders.Length; i++)
            {
                if (ragdollColliders[i] != null)
                {
                    ragdollColliders[i].transform.localPosition = originalPositions[i];
                    ragdollColliders[i].transform.localRotation = originalRotations[i];
                }
            }

            // Zero ALL velocities on limbs so the chain settles immediately
            for (int i = 0; i < ragdollRigidbodies.Length; i++)
            {
                if (ragdollRigidbodies[i] != null)
                {
                    ragdollRigidbodies[i].linearVelocity = Vector3.zero;
                    ragdollRigidbodies[i].angularVelocity = Vector3.zero;
                }
            }
        }

        /// <summary>
        /// Force enable/disable ragdoll (e.g. on hit/stun).
        /// Same as EnableRagdoll but also handles joints and knockback impulse.
        /// </summary>
        public void ForceEnableRagdoll(bool enable, float forceMultiplier = 1.0f)
        {
            if (isRagdollEnabled == enable) return;

            isRagdollEnabled = enable;
            OnRagdollStateChanged?.Invoke(isRagdollEnabled);

            // Colliders
            for (int i = 0; i < ragdollColliders.Length; i++)
            {
                if (ragdollColliders[i] != null)
                    ragdollColliders[i].enabled = enable;
            }

            // Rigidbodies + knockback impulse
            for (int i = 0; i < ragdollRigidbodies.Length; i++)
            {
                if (ragdollRigidbodies[i] != null)
                {
                    ragdollRigidbodies[i].isKinematic = !enable;
                    ragdollRigidbodies[i].useGravity = enable;

                    if (enable)
                    {
                        // Coherent knockdown impulse: the character falls
                        // in the direction it was hit (NOT random).
                        Vector3 dir = (transform.forward + Vector3.down * 0.5f).normalized;
                        ragdollRigidbodies[i].AddForce(dir * (4f * forceMultiplier), ForceMode.Impulse);
                        ragdollRigidbodies[i].AddTorque(transform.right * (2f * forceMultiplier), ForceMode.Impulse);
                    }
                    else
                    {
                        ragdollRigidbodies[i].linearVelocity = Vector3.zero;
                        ragdollRigidbodies[i].angularVelocity = Vector3.zero;
                    }
                }
            }

            // Joints — same toggle as EnableRagdoll
            SetJointsActive(enable);

            // Animator
            if (animator != null)
                animator.enabled = !enable;

            // Recovery sync
            ResetToInitialStates();
        }

        private void OnDrawGizmosSelected()
        {
            if (ragdollColliders != null && ragdollColliders.Length > 0)
            {
                Gizmos.color = isRagdollEnabled ? Color.red : Color.green;
                for (int i = 0; i < ragdollColliders.Length; i++)
                {
                    if (ragdollColliders[i] != null && ragdollColliders[i].enabled != isRagdollEnabled)
                    {
                        Gizmos.DrawWireSphere(ragdollColliders[i].transform.position, 0.05f);
                    }
                }
            }
        }
    }
}