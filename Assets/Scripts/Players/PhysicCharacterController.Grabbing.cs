using System.Collections;
using System.Drawing;
using UnityEngine;

namespace RabbleHouse
{
    public partial class PhysicCharacterController
    {
        private void TryCreatePlayerContactJoint(Rigidbody handBody, ref Joint contactJoint)
        {
            if (handBody == null || contactJoint != null)
                return;

            Collider handCollider = handBody.GetComponent<Collider>();
            if (handCollider == null)
                return;

            // Use actual hand-collider contact geometry so the object only
            // attaches after the raised hand reaches it.
            Bounds bounds = handCollider.bounds;
            Collider[] overlaps = Physics.OverlapBox(bounds.center, bounds.extents,
                Quaternion.identity, LayerMask.GetMask("Grabbable"), QueryTriggerInteraction.Ignore);
            GrabbableObject closest = null;
            float closestDistance = float.MaxValue;
            foreach (Collider overlap in overlaps)
            {
                if (overlap == null || overlap.attachedRigidbody == handBody)
                    continue;

                GrabbableObject candidate = overlap.GetComponentInParent<GrabbableObject>();
                if (candidate == null || candidate.IsHeld || candidate.Rigidbody == null ||
                    candidate.transform.IsChildOf(transform))
                    continue;

                float distance = (overlap.ClosestPoint(handBody.position) - handBody.position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = candidate;
                }
            }

            if (closest == null)
                return;


            // Tool grips are positioned from the detected object, since
            // heldObject is assigned only after both contact joints are evaluated.
            ToolBehaviour candidateTool = closest.GetComponent<ToolBehaviour>();
            bool isTool = closest.grabbableType == GrabbableType.Tool && candidateTool != null;
            bool isPrimaryToolHand = handBody == rightHandRb;
            if (isTool && !isPrimaryToolHand && candidateTool.OneHanded)
                return;

            if (isTool && isPrimaryToolHand && closest.gripPoint != null)
            {
                Transform toolTransform = closest.transform;
                Transform handGripPoint = isPrimaryToolHand ? closest.gripPoint : closest.secondaryGripPoint;

                if (handGripPoint != null)
                {
                    // Figure out the spatial offset between the tool root and its own grip point
                    Quaternion gripRotationInToolSpace = Quaternion.Inverse(toolTransform.rotation) * handGripPoint.rotation;
                    Vector3 gripPositionInToolSpace = toolTransform.InverseTransformPoint(handGripPoint.position);

                    // Match the tool perfectly to the current hand orientation
                    Quaternion toolRotation = handBody.rotation * Quaternion.Inverse(gripRotationInToolSpace);
                    Vector3 toolPosition = handBody.position - toolRotation * gripPositionInToolSpace;

                    toolTransform.rotation = toolRotation;
                    toolTransform.position = toolPosition;

                    // Kill momentum completely so the joint doesn't inherit dirty physics data
                    closest.Rigidbody.linearVelocity = Vector3.zero;
                    closest.Rigidbody.angularVelocity = Vector3.zero;

                    // Forces Unity to update the physics engine matrices immediately before the joint attaches
                    Physics.SyncTransforms();
                }
            }

            // Create ConfigurableJoint on the hand after any tool alignment.
            ConfigurableJoint grabJoint = handBody.gameObject.AddComponent<ConfigurableJoint>();
            if (isTool)
            {
                grabJoint.connectedBody = closest.Rigidbody;
                grabJoint.anchor = Vector3.zero;
                Transform handGripPoint = isPrimaryToolHand
                    ? closest.gripPoint
                    : closest.secondaryGripPoint;
                if (handGripPoint != null)
                {
                    grabJoint.autoConfigureConnectedAnchor = false;
                    grabJoint.connectedAnchor = closest.Rigidbody.transform.InverseTransformPoint(handGripPoint.position);
                }
                else
                {
                    grabJoint.autoConfigureConnectedAnchor = true;
                }
                grabJoint.xMotion = ConfigurableJointMotion.Limited;
                grabJoint.yMotion = ConfigurableJointMotion.Limited;
                grabJoint.zMotion = ConfigurableJointMotion.Limited;
                grabJoint.angularXMotion = ConfigurableJointMotion.Limited;
                grabJoint.angularYMotion = ConfigurableJointMotion.Limited;
                grabJoint.angularZMotion = ConfigurableJointMotion.Limited;

                grabJoint.breakForce = 1500f;
                grabJoint.breakTorque = 1500f;
                grabJoint.enablePreprocessing = false;

                IgnoreEntireCharacter(closest.gameObject, true);
            }
            else
            {
                grabJoint.connectedBody = closest.Rigidbody;
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
            }
            contactJoint = grabJoint;
        }

        private void ReleasePlayerContactJoint(ref Joint contactJoint)
        {
            GrabbableObject releasedObject = GetContactJointTarget(contactJoint);
            bool wasToolJoint = releasedObject != null &&
                releasedObject.grabbableType == GrabbableType.Tool &&
                releasedObject.GetComponent<ToolBehaviour>() != null;

            if (contactJoint != null)
            {
                Destroy(contactJoint);
            }
            contactJoint = null;

            if (wasToolJoint &&
                GetContactJointTarget(playerLeftContactJoint) != releasedObject &&
                GetContactJointTarget(playerRightContactJoint) != releasedObject)
            {
                IgnoreEntireCharacter(releasedObject.gameObject, false);
            }
        }

        private void UpdatePlayerContactHeldObject()
        {
            GrabbableObject leftTarget = GetContactJointTarget(playerLeftContactJoint);
            GrabbableObject rightTarget = GetContactJointTarget(playerRightContactJoint);
            ToolBehaviour rightHandTool = rightTarget != null
                ? rightTarget.GetComponent<ToolBehaviour>()
                : null;
            GrabbableObject contactTarget = rightHandTool != null && rightHandTool.OneHanded
                ? rightTarget
                : leftTarget != null && leftTarget == rightTarget
                    ? leftTarget
                    : null;

            if (playerContactHeldObject == contactTarget && heldObject == contactTarget)
                return;

            // Clear only the value owned by this experimental contact path;
            // a legacy pickup remains untouched.
            if (playerContactHeldObject != null && heldObject == playerContactHeldObject)
            {
                if (activeTool != null)
                {
                    activeTool.OnToolReleased();
                    activeTool = null;
                }
                playerContactHeldObject.ReleaseByPlayer();
                heldObject = null;
                heldGrabbableType = GrabbableType.SmallObject;
            }
            playerContactHeldObject = null;

            if (contactTarget == null || heldObject != null)
                return;

            playerContactHeldObject = contactTarget;
            heldObject = contactTarget;
            contactTarget.GrabByPlayer(this);
            activeTool = contactTarget.GetComponent<ToolBehaviour>();
            if (activeTool != null)
                activeTool.OnToolGrabbed(this);
            heldGrabbableType = contactTarget.grabbableType;
        }

        private static GrabbableObject GetContactJointTarget(Joint contactJoint)
        {
            return contactJoint != null && contactJoint.connectedBody != null
                ? contactJoint.connectedBody.GetComponentInParent<GrabbableObject>()
                : null;
        }

        // Helper method to completely decouple the weapon from colliding with the user
        private void IgnoreEntireCharacter(GameObject toolObj, bool ignore)
        {
            Collider[] playerColliders = transform.root.GetComponentsInChildren<Collider>();
            Collider[] toolColliders = toolObj.GetComponentsInChildren<Collider>();

            foreach (var pCol in playerColliders)
            {
                foreach (var tCol in toolColliders)
                {
                    if (pCol != null && tCol != null)
                    {
                        Physics.IgnoreCollision(pCol, tCol, ignore);
                    }
                }
            }
        }

        private float aiGrabHandReachDistance = 0.25f;
        private float aiGrabArmRaiseDistance = 0.5f;
        private bool aiGrabReachPoseActive;

        /// <summary>
        /// AI pickup eligibility follows hand reach; player pickup eligibility
        /// continues to use the forward-facing box in TryGrabObject.
        /// </summary>
        public bool CanGrabObject(GrabbableObject candidate)
        {
            if (candidate == null || candidate.IsHeld || candidate.Rigidbody == null)
                return false;

            if (candidate.grabbableType == GrabbableType.Tool)
            {
                // The right hand is the primary grip for tools.
                return IsHandWithinGrabReach(rightHandRb, candidate);
            }

            // Let either hand initiate a natural pickup; only reachable hands
            // will receive joints when the object is grabbed.
            return IsHandWithinGrabReach(leftHandRb, candidate) ||
                   IsHandWithinGrabReach(rightHandRb, candidate);
        }

        /// <summary>
        /// Returns true when the selected object is near enough for the AI to
        /// begin reaching, before it is close enough to create a grab joint.
        /// </summary>
        public bool IsAIObjectWithinArmRaiseDistance(GrabbableObject candidate)
        {
            if (candidate == null || candidate.IsHeld)
                return false;

            return IsHandWithinDistance(leftHandRb, candidate, aiGrabArmRaiseDistance) ||
                   IsHandWithinDistance(rightHandRb, candidate, aiGrabArmRaiseDistance);
        }

        private bool IsHandWithinGrabReach(Rigidbody handBody, GrabbableObject candidate)
        {
            return IsHandWithinDistance(handBody, candidate, aiGrabHandReachDistance);
        }

        private bool IsHandWithinDistance(Rigidbody handBody, GrabbableObject candidate, float maxDistance)
        {
            if (handBody == null || candidate == null)
                return false;

            Collider[] colliders = candidate.GetComponentsInChildren<Collider>();
            float maxDistanceSquared = maxDistance * maxDistance;
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

        /// <summary>
        /// Let the AI raise its arms while reaching without changing the
        /// player's pickup behavior or moving the target before joint setup.
        /// </summary>
        public void SetAIGrabReachPose(bool active)
        {
            if (heldObject != null || aiGrabReachPoseActive == active || 
                (currentState == CharacterState.Ragdoll || currentState == CharacterState.Dead))
                return;

            aiGrabReachPoseActive = active;
            if (active)
                RaiseBothArms();
            else
                ResetBothArms();
        }

        // --- GRAB / DROP ---
        private void TryGrabObject()
        {
            Vector3 origin = coreRigidbody.position;
            Collider[] hits;
            if (aiInputHandler != null)
            {
                // AI searches from its hands so its reach pose determines pickup
                // eligibility. This does not move or snap the object.
                int grabbableLayer = LayerMask.GetMask("Grabbable");
                var handHits = new System.Collections.Generic.List<Collider>();
                if (leftHandRb != null)
                    handHits.AddRange(Physics.OverlapSphere(leftHandRb.position, aiGrabHandReachDistance, grabbableLayer));
                if (rightHandRb != null)
                    handHits.AddRange(Physics.OverlapSphere(rightHandRb.position, aiGrabHandReachDistance, grabbableLayer));
                hits = handHits.ToArray();
            }
            else
            {
                // Preserve the player's existing forward-facing pickup volume.
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

            // Contact-held objects use separate joints from the legacy grab
            // path, so release both hand connections before applying throw velocity.
            if (playerContactHeldObject == heldObject)
            {
                ReleasePlayerContactJoint(ref playerLeftContactJoint);
                ReleasePlayerContactJoint(ref playerRightContactJoint);
                playerContactHeldObject = null;
                leftArmToggle = false;
                rightArmToggle = false;
            }

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

            // Contact-held objects use separate joints from the legacy grab
            // path, so release both hand connections before applying throw velocity.
            if (playerContactHeldObject == heldObject)
            {
                ReleasePlayerContactJoint(ref playerLeftContactJoint);
                ReleasePlayerContactJoint(ref playerRightContactJoint);
                playerContactHeldObject = null;
                leftArmToggle = false;
                rightArmToggle = false;
            }

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
        private void AIRaiseArmsForHeld()
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

        public void RaiseArm(bool leftArm)
        {
            if (leftArm)
            {
                // Disable ActiveRagdollBone scripts so they don't fight our target rotation
                LUpperBoneScript.enabled = false;
                LLowerBoneScript.enabled = false;
                // Boost X and YZ joint drives for both arms
                SetXYZJointStrength(leftUpperArm, 12000f, 120f);
                SetXYZJointStrength(leftLowerArm, 12000f, 120f);
                // Set target rotations for the raised arms
                leftUpperArm.targetRotation = Quaternion.Euler(0, -90, 60);
                leftLowerArm.targetRotation = Quaternion.Euler(330, 0, 0);
            }
            else
            {
                // Disable ActiveRagdollBone scripts so they don't fight our target rotation
                RUpperBoneScript.enabled = false;
                RLowerBoneScript.enabled = false;
                // Boost X and YZ joint drives for both arms
                SetXYZJointStrength(rightUpperArm, 12000f, 120f);
                SetXYZJointStrength(rightLowerArm, 12000f, 120f);
                // Set target rotations for the raised arms
                rightUpperArm.targetRotation = Quaternion.Euler(0, 90, -60);
                rightLowerArm.targetRotation = Quaternion.Euler(330, 0, 0);
            }
        }

        private void ApplyToolHoldPose()
        {
            ToolBehaviour tool = activeTool != null
                ? activeTool
                : heldObject != null ? heldObject.GetComponent<ToolBehaviour>() : null;
            ToolArmProfile profile = tool != null ? tool.ArmProfile : null;
            if (profile == null)
                return;

            bool contactHeld = playerContactHeldObject != null && playerContactHeldObject == heldObject;
            bool applyRightPose = !contactHeld ||
                (rightArmToggle && GetContactJointTarget(playerRightContactJoint) == heldObject);
            bool applyLeftPose = !contactHeld ||
                (leftArmToggle && GetContactJointTarget(playerLeftContactJoint) == heldObject);

            if (applyRightPose)
            {
                RUpperBoneScript.enabled = false;
                RLowerBoneScript.enabled = false;
                SetXYZJointStrength(rightUpperArm, 12000f, 240f);
                SetXYZJointStrength(rightLowerArm, 12000f, 240f);
                rightUpperArm.targetRotation = Quaternion.Euler(profile.rightUpperHold);
                rightLowerArm.targetRotation = Quaternion.Euler(profile.rightLowerHold);
                rightUpperArm.targetAngularVelocity = Vector3.zero;
            }

            if (tool.OneHanded || !applyLeftPose)
                return;

            LUpperBoneScript.enabled = false;
            LLowerBoneScript.enabled = false;
            SetXYZJointStrength(leftUpperArm, 12000f, 240f);
            SetXYZJointStrength(leftLowerArm, 12000f, 240f);
            leftUpperArm.targetRotation = Quaternion.Euler(profile.leftUpperHold);
            leftLowerArm.targetRotation = Quaternion.Euler(profile.leftLowerHold);
            leftUpperArm.targetAngularVelocity = Vector3.zero;
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

        public void ResetArm(bool leftArm)
        {
            if (leftArm)
            {
                // Enable ActiveRagdollBone Scripts
                LUpperBoneScript.enabled = true;
                LLowerBoneScript.enabled = true;
                // Revert arm joint drives to their original values
                leftUpperArm.angularXDrive = originalLeftUpperX;
                leftUpperArm.angularYZDrive = originalLeftUpperYZ;
                leftLowerArm.angularXDrive = originalLeftLowerX;
                leftLowerArm.angularYZDrive = originalLeftLowerYZ;
                // Reset target rotations to neutral
                leftUpperArm.targetRotation = Quaternion.Euler(0, 0, 0);
                leftLowerArm.targetRotation = Quaternion.Euler(0, 0, 0);
            }
            else
            {
                // Enable ActiveRagdollBone Scripts
                RUpperBoneScript.enabled = true;
                RLowerBoneScript.enabled = true;
                // Revert arm joint drives to their original values
                rightUpperArm.angularXDrive = originalRightUpperX;
                rightUpperArm.angularYZDrive = originalRightUpperYZ;
                rightLowerArm.angularXDrive = originalRightLowerX;
                rightLowerArm.angularYZDrive = originalRightLowerYZ;
                // Reset target rotations to neutral
                rightUpperArm.targetRotation = Quaternion.Euler(0, 0, 0);
                rightLowerArm.targetRotation = Quaternion.Euler(0, 0, 0);
            }
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
