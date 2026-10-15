using UnityEngine;
using System.Collections;

namespace RabbleHouse
{
    public partial class PhysicCharacterController
    {
        private float aiGrabHandReachDistance = 0.25f;
        private bool aiGrabReachPoseActive;

        /// <summary>
        /// Raises the AI's arms while it approaches a pickup target, giving its
        /// hands a chance to reach counter-height objects before a joint is
        /// created. No object transform is changed.
        /// </summary>
        public void SetAIGrabReachPose(bool active)
        {
            if (aiInputHandler == null)
                return;

            bool shouldBeActive = active && heldObject == null;
            if (aiGrabReachPoseActive == shouldBeActive)
                return;

            aiGrabReachPoseActive = shouldBeActive;
            if (!aiGrabReachPoseActive && heldObject == null)
                ResetBothArms();
        }

        /// <summary>
        /// AI pickup eligibility is based on hand-to-collider distance, not the
        /// hips' search range. This keeps the existing physics joints from
        /// pulling an object across a gap when a grab starts.
        /// </summary>
        public bool CanGrabObject(GrabbableObject candidate)
        {
            if (candidate == null || candidate.IsHeld || candidate.Rigidbody == null)
                return false;

            if (candidate.grabbableType == GrabbableType.Tool)
            {
                // The right hand is the primary grip for tools. A second hand is
                // attached only when it is already close enough.
                return IsHandWithinGrabReach(rightHandRb, candidate);
            }

            // Let either hand initiate a natural pickup. TryGrabObject attaches
            // only the hands that are already in reach, so a distant hand cannot
            // pull the object or character across the gap.
            return IsHandWithinGrabReach(leftHandRb, candidate) ||
                   IsHandWithinGrabReach(rightHandRb, candidate);
        }

        private bool IsHandWithinGrabReach(Rigidbody handBody, GrabbableObject candidate)
        {
            if (handBody == null || candidate == null)
                return false;

            Collider[] colliders = candidate.GetComponentsInChildren<Collider>();
            float maxDistanceSquared = aiGrabHandReachDistance * aiGrabHandReachDistance;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider candidateCollider = colliders[i];
                if (candidateCollider == null || candidateCollider.isTrigger)
                    continue;

                Vector3 closestPoint = candidateCollider.ClosestPoint(handBody.position);
                if ((closestPoint - handBody.position).sqrMagnitude <= maxDistanceSquared)
                    return true;
            }

            return false;
        }

        // --- GRAB / DROP ---
        private void TryGrabObject()
        {
            Vector3 origin = coreRigidbody.position;
            Collider[] hits;
            if (aiInputHandler != null)
            {
                // For AI, search around the hands and require actual hand reach
                // below. The wider candidate search does not move or snap objects.
                int grabbableLayer = LayerMask.GetMask("Grabbable");
                var handHits = new System.Collections.Generic.List<Collider>();
                float searchRadius = aiGrabHandReachDistance;
                if (leftHandRb != null)
                    handHits.AddRange(Physics.OverlapSphere(leftHandRb.position, searchRadius, grabbableLayer));
                if (rightHandRb != null)
                    handHits.AddRange(Physics.OverlapSphere(rightHandRb.position, searchRadius, grabbableLayer));
                hits = handHits.ToArray();
            }
            else
            {
                // Preserve the player's existing forward-facing pickup check.
                origin += coreRigidbody.transform.forward * (grabRange * 0.5f);
                hits = Physics.OverlapBox(origin, new Vector3(0.1f, 0.6f, grabRange * 0.5f),
                    coreRigidbody.rotation, LayerMask.GetMask("Grabbable"));
            }

            GrabbableObject closest = null;
            float bestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var grabbable = hit.GetComponent<GrabbableObject>();
                if (grabbable != null && !grabbable.IsHeld &&
                    (aiInputHandler == null || CanGrabObject(grabbable)))
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
                heldGrabbableType = closest.grabbableType;
                closest.GrabByPlayer(this);

                if (heldGrabbableType == GrabbableType.SmallObject)
                {
                    // The AI attaches only hands already in reach. The player
                    // keeps the existing two-handed pickup behavior.
                    if (aiInputHandler != null)
                    {
                        if (IsHandWithinGrabReach(leftHandRb, closest))
                            leftHandJoint = SetupGrabJoint(leftHandRb, closest, true);
                        if (IsHandWithinGrabReach(rightHandRb, closest))
                            rightHandJoint = SetupGrabJoint(rightHandRb, closest, false);
                    }
                    else
                    {
                        leftHandJoint = SetupGrabJoint(leftHandRb, closest, true);
                        rightHandJoint = SetupGrabJoint(rightHandRb, closest, false);
                    }
                }
                else if (heldGrabbableType == GrabbableType.Tool)
                {
                    // Cache tool behavior
                    activeTool = closest.GetComponent<ToolBehaviour>();

                    // Right hand is always the primary hand.
                    rightHandJoint = SetupGrabJoint(rightHandRb, closest, false);

                    // Two-handed tools also attach the left hand.
                    if (activeTool != null && !activeTool.OneHanded &&
                        (aiInputHandler == null || IsHandWithinGrabReach(leftHandRb, closest)))
                    {
                        leftHandJoint = SetupGrabJoint(leftHandRb, closest, true);
                    }
                    else
                    {
                        leftHandJoint = null;
                    }

                    if (activeTool != null)
                        activeTool.OnToolGrabbed(this);
                }
                else
                {
                    // Two-handed grab
                    if (aiInputHandler != null)
                    {
                        if (IsHandWithinGrabReach(leftHandRb, closest))
                            leftHandJoint = SetupGrabJoint(leftHandRb, closest, true);
                        if (IsHandWithinGrabReach(rightHandRb, closest))
                            rightHandJoint = SetupGrabJoint(rightHandRb, closest, false);
                    }
                    else
                    {
                        leftHandJoint = SetupGrabJoint(leftHandRb, closest, true);
                        rightHandJoint = SetupGrabJoint(rightHandRb, closest, false);
                    }
                }
                SetState(CharacterState.Grabbing);
            }
        }

        private Joint SetupGrabJoint(Rigidbody handBody, GrabbableObject obj, bool isLeftHand)
        {
            heldObject = obj;
            obj.GrabByPlayer(this);

            // For SmallObject: use ConfigurableJoint with very high position spring to hold object at front
            if (heldGrabbableType == GrabbableType.SmallObject)
            {
                // Create ConfigurableJoint on the hand
                ConfigurableJoint grabJoint = handBody.gameObject.AddComponent<ConfigurableJoint>();
                grabJoint.connectedBody = heldObject.Rigidbody;
                grabJoint.autoConfigureConnectedAnchor = true;
                grabJoint.anchor = Vector3.zero;

                // Lock angular so object doesn't rotate independently
                grabJoint.angularXMotion = ConfigurableJointMotion.Locked;
                grabJoint.angularYMotion = ConfigurableJointMotion.Locked;
                grabJoint.angularZMotion = ConfigurableJointMotion.Locked;

                // Lock linear to hold position (will be driven by spring below)
                grabJoint.xMotion = ConfigurableJointMotion.Locked;
                grabJoint.yMotion = ConfigurableJointMotion.Locked;
                grabJoint.zMotion = ConfigurableJointMotion.Locked;

                // Very high position spring to minimize snap-to-hand while allowing physics
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
                Physics.IgnoreCollision(handBody.GetComponent<Collider>(), heldObject.GetComponent<Collider>(), true);
                if (balancer != null)
                    balancer.weight *= 0.5f;

                return grabJoint;
            }
            else if (heldGrabbableType == GrabbableType.LargeObject)
            {
                // LargeObject: ConfigurableJoint with spring-damper
                ConfigurableJoint grabJoint = handBody.gameObject.AddComponent<ConfigurableJoint>();
                grabJoint.connectedBody = heldObject.Rigidbody;
                grabJoint.autoConfigureConnectedAnchor = true;
                grabJoint.anchor = Vector3.zero;

                grabJoint.xMotion = ConfigurableJointMotion.Locked;
                grabJoint.yMotion = ConfigurableJointMotion.Locked;
                grabJoint.zMotion = ConfigurableJointMotion.Locked;
                grabJoint.angularXMotion = ConfigurableJointMotion.Locked;
                grabJoint.angularYMotion = ConfigurableJointMotion.Locked;
                grabJoint.angularZMotion = ConfigurableJointMotion.Locked;

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
                Physics.IgnoreCollision(handBody.GetComponent<Collider>(), heldObject.GetComponent<Collider>(), true);
                if (balancer != null)
                    balancer.weight *= 0.5f;

                return grabJoint;
            }
            else if (heldGrabbableType == GrabbableType.Tool)
            {
                // Only the primary/right hand positions the tool.
                if (!isLeftHand)
                {
                    Quaternion rotationOffset =
                        handBody.rotation *
                        Quaternion.Inverse(heldObject.gripPoint.localRotation);

                    heldObject.transform.rotation = rotationOffset;

                    Vector3 positionOffset =
                        handBody.position -
                        (rotationOffset * heldObject.gripPoint.localPosition);

                    heldObject.transform.position = positionOffset;
                }

                // Tool: ConfigurableJoint with spring-damper
                FixedJoint grabJoint = handBody.gameObject.AddComponent<FixedJoint>();
                grabJoint.connectedBody = heldObject.Rigidbody;
                grabJoint.anchor = Vector3.zero;

                // Primary hand
                if (!isLeftHand)
                {
                    grabJoint.autoConfigureConnectedAnchor = false;

                    grabJoint.connectedAnchor =
                        heldObject.Rigidbody.transform.InverseTransformPoint(
                            heldObject.gripPoint.position
                        );
                }
                // Secondary hand
                else
                {
                    if (heldObject.secondaryGripPoint == null)
                    {
                        Debug.LogWarning(
                            $"[PhysicCharacterController] {heldObject.name} is configured as a two-handed tool but has no Secondary Grip Point."
                        );

                        Destroy(grabJoint);
                        return null;
                    }

                    grabJoint.autoConfigureConnectedAnchor = false;
                    grabJoint.connectedAnchor = heldObject.Rigidbody.transform.InverseTransformPoint(heldObject.secondaryGripPoint.position);
                }
                grabJoint.breakForce = 1500f;
                grabJoint.breakTorque = 1500f;
                Physics.IgnoreCollision(handBody.GetComponent<Collider>(), heldObject.GetComponent<Collider>(), true);
                if (!isLeftHand && balancer != null)
                    balancer.weight *= 0.5f;

                return grabJoint;
            }
            else return null;
        }

        public void ReleaseObject()
        {
            if (heldObject == null) return;

            // Clean up both joints
            if (leftHandJoint != null) Destroy(leftHandJoint);
            if (rightHandJoint != null) Destroy(rightHandJoint);

            // Clean up tool
            if (activeTool != null)
            {
                activeTool.OnToolReleased();
                activeTool = null;
            }

            heldObject.ReleaseByPlayer();
            heldObject = null;
            heldGrabbableType = GrabbableType.SmallObject;
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

            // Clean up tool
            if (activeTool != null)
            {
                activeTool.OnToolReleased();
                activeTool = null;
            }

            heldObject.ReleaseByPlayer();
            heldObject = null;
            heldGrabbableType = GrabbableType.SmallObject;
            leftHandJoint = null;
            rightHandJoint = null;
            ResetBothArms();
            if (balancer != null)
                balancer.weight = 1f;
        }

        // --- ARM MUSCLE HELPERS ---
        private void RaiseArmsForHeld()
        {
            var tool = heldObject != null ? heldObject.GetComponent<ToolBehaviour>() : null;
            // Tool type
            if (tool != null)
            {
                // Per-tool hold pose
                var profile = tool.ArmProfile;
                bool oneHandedTool = tool.OneHanded;

                RUpperBoneScript.enabled = false;
                RLowerBoneScript.enabled = false;

                SetXYZJointStrength(rightUpperArm, 12000f, 240f);
                SetXYZJointStrength(rightLowerArm, 12000f, 240f);

                // Set target rotations
                rightUpperArm.targetRotation = Quaternion.Euler(profile.rightUpperHold);
                rightLowerArm.targetRotation = Quaternion.Euler(profile.rightLowerHold);
                // Zero out any existing angular velocity
                rightUpperArm.targetAngularVelocity = Vector3.zero;
                

                if (!oneHandedTool)
                {
                    LUpperBoneScript.enabled = false;
                    LLowerBoneScript.enabled = false;

                    SetXYZJointStrength(leftUpperArm, 12000f, 240f);
                    SetXYZJointStrength(leftLowerArm, 12000f, 240f);

                    // Set target rotations
                    leftUpperArm.targetRotation = Quaternion.Euler(profile.leftUpperHold);
                    leftLowerArm.targetRotation = Quaternion.Euler(profile.leftLowerHold);
                    // Zero out any existing angular velocity
                    leftUpperArm.targetAngularVelocity = Vector3.zero;
                }

                return;
            }

            // Small & Large object
            RaiseBothArms();
        }
        public void RaiseBothArms()
        {
            // Disable ActiveRagdollBone scripts so they don't fight our target rotation
            RUpperBoneScript.enabled = false;
            RLowerBoneScript.enabled = false;
            LUpperBoneScript.enabled = false;
            LLowerBoneScript.enabled = false;

            // Boost X and YZ joint drives for both arms
            SetXYZJointStrength(rightUpperArm, 12000f, 120f);
            SetXYZJointStrength(rightLowerArm, 12000f, 120f);
            SetXYZJointStrength(leftUpperArm, 12000f, 120f);
            SetXYZJointStrength(leftLowerArm, 12000f, 120f);

            // Zero out any existing angular velocity
            rightUpperArm.targetAngularVelocity = Vector3.zero;
            leftUpperArm.targetAngularVelocity = Vector3.zero;

            // Set target rotations for the raised arms
            rightUpperArm.targetRotation = Quaternion.Euler(0, 90, -60);
            rightLowerArm.targetRotation = Quaternion.Euler(330, 0, 0);
            leftUpperArm.targetRotation = Quaternion.Euler(0, -90, 60);
            leftLowerArm.targetRotation = Quaternion.Euler(330, 0, 0);
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

    }
}
