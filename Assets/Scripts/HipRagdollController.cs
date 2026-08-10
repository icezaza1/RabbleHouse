using UnityEngine;
using System.Collections.Generic;

namespace RabbleHouse
{
    /// <summary>
    /// Central ragdoll controller that works with ConfigurableJoints.
    /// Attached to the Hip GameObject - acts as the character core.
    /// Controls the character’s transition between:
    /// 1. Controlled movement (animator active)
    /// 2. Ragdoll physics (animator off, limbs become independent)
    /// </summary>
    public class HipRagdollController : MonoBehaviour
    {
        [Header("Joint Configuration")]
        // All ConfigurableJoints on child limbs that should be controllable
        [SerializeField] private ConfigurableJoint[] ragdollJoints;

        // Store original angular drive settings for restoration when ragdoll turns off
        private JointDrive[] originalAngularXDrive;
        private JointDrive[] originalAngularYZDrive;

        [Header("Core Physics")]
        [SerializeField] private Rigidbody coreRigidbody;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 180f;

        // State tracking
        public bool IsRagdoll { get; private set; } = false;
        public System.Action<bool> OnRagdollStateChanged;

        private void Awake()
        {
            // Auto-discover ConfigurableJoints on direct children only
            // This excludes any joints that might be on the Hip itself
            ragdollJoints = GetComponentsInChildren<ConfigurableJoint>(true);

            // Save original angular drive settings
            originalAngularXDrive = new JointDrive[ragdollJoints.Length];
            originalAngularYZDrive = new JointDrive[ragdollJoints.Length];

            for (int i = 0; i < ragdollJoints.Length; i++)
            {
                ConfigurableJoint joint = ragdollJoints[i];
                if (joint != null)
                {
                    originalAngularXDrive[i] = joint.angularXDrive;
                    originalAngularYZDrive[i] = joint.angularYZDrive;
                }
            }
        }

        public void SetRagdollMode(bool enable)
        {
            if (IsRagdoll == enable) return;
            IsRagdoll = enable;

            OnRagdollStateChanged?.Invoke(IsRagdoll);

            if (IsRagdoll)
            {
                // Turn ragdoll ON: make limbs independent, restore angular drives
                EnableRagdollMode(true);
            }
            else
            {
                // Turn ragdoll OFF: reattach limbs, restore original drive settings
                EnableRagdollMode(false);
            }
        }

        private void EnableRagdollMode(bool enableRagdoll)
        {
            foreach (ConfigurableJoint joint in ragdollJoints)
            {
                if (joint == null) continue;

                if (enableRagdoll)
                {
                    SoftJointLimit newLimit = new SoftJointLimit();
                    newLimit.limit = -1000f;
                    // Disable connectedBody - limb becomes independent
                    joint.connectedBody = null;
                    joint.xMotion = ConfigurableJointMotion.Free;
                    joint.yMotion = ConfigurableJointMotion.Free;
                    joint.zMotion = ConfigurableJointMotion.Free;
                    joint.angularXMotion = ConfigurableJointMotion.Free;
                    joint.angularYMotion = ConfigurableJointMotion.Free;
                    joint.angularZMotion = ConfigurableJointMotion.Free;
                    joint.lowAngularXLimit = joint.highAngularXLimit = newLimit;
                }
                else
                {
                    // Reattach limb to core - restore angular drive settings
                    joint.xMotion = ConfigurableJointMotion.Locked;
                    joint.yMotion = ConfigurableJointMotion.Locked;
                    joint.zMotion = ConfigurableJointMotion.Locked;

                    // Restore original angular drives
                    int index = System.Array.IndexOf(ragdollJoints, joint);
                    if (index >= 0)
                    {
                        joint.angularXDrive = originalAngularXDrive[index];
                        joint.angularYZDrive = originalAngularYZDrive[index];
                    }

                    // Reconnect to core Rigidbody
                    if (joint.name != "Hip")
                        joint.connectedBody = coreRigidbody?.GetComponent<Rigidbody>();
                }
            }

            // Update core physics
            if (coreRigidbody != null)
            {
                coreRigidbody.isKinematic = !enableRagdoll;
                coreRigidbody.useGravity = !enableRagdoll;
            }
        }

        public void ApplyCoreMovement(Vector3 velocity, float rotationDelta)
        {
            if (!IsRagdoll && coreRigidbody != null)
            {
                coreRigidbody.linearVelocity = velocity;
                if (rotationDelta != 0f)
                {
                    Quaternion current = coreRigidbody.rotation;
                    Quaternion target = Quaternion.Euler(0f, current.eulerAngles.y + rotationDelta, 0f);
                    coreRigidbody.rotation = Quaternion.Slerp(current, target, Time.deltaTime * rotationSpeed);
                }
            }
        }
    }
}