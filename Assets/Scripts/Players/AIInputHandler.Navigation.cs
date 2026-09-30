using System.Collections.Generic;
using UnityEngine;

namespace RabbleHouse
{
    public partial class AIInputHandler
    {
        //  MOVEMENT HELPERS
        // -----------------------------------------------------------------------
        private void HandleChasing(Transform target, float distToTarget)
        {
            if (distToTarget < 1.5f)
                MoveToward(target.position);
            else
                MoveToward(InterceptPosition(target));
        }
        private void MoveToward(Vector3 targetPos)
        {
            if (coreRb == null) return;
            if (!controller.IsGrounded) return;

            // Sprint Handle
            bool isSprinting = controller.SprintPressed ? true : false;

            Vector3 toTarget = targetPos - coreRb.position;
            toTarget.y = 0;
            if (toTarget.sqrMagnitude < 0.01f)
            {
                MoveInput = Vector2.zero;
                ResetObstacleAvoidance();
                return;
            }

            Vector3 desiredDirection = toTarget.normalized;

            // ---------------------------------------------------------
            // Check whether the direct path is clear.
            // ---------------------------------------------------------

            if (!IsPathBlocked(desiredDirection, null))
            {
                // No obstacle -> go directly toward target.
                ResetObstacleAvoidance();

                SetMoveDirection(desiredDirection);

                UpdateStuckDetection();
                return;
            }

            // ---------------------------------------------------------
            // Path is blocked -> find an avoidance direction.
            // ---------------------------------------------------------

            Vector3 avoidanceDirection = GetAvoidanceDirection(desiredDirection, targetPos);

            SetMoveDirection(avoidanceDirection);

            UpdateStuckDetection();
        }

        private void SetMoveDirection(Vector3 direction)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
            {
                MoveInput = Vector2.zero;
                return;
            }

            direction.Normalize();

            MoveInput = new Vector2(direction.x, direction.z).normalized;
        }
        private bool IsPathBlocked(Vector3 direction, Transform intendedTarget)
        {
            if (coreRb == null) return false;

            direction.y = 0;
            if (direction.sqrMagnitude < 0.001f) return false;

            direction.Normalize();

            Vector3 origin = coreRb.position + Vector3.up * 0.5f;

            LayerMask combinedMask = obstacleMask | grabbableMask;

            if (!Physics.SphereCast(
                origin,
                obstacleCheckRadius,
                direction,
                out RaycastHit hit,
                obstacleCheckDistance,
                combinedMask,
                QueryTriggerInteraction.Ignore)) 
                return false;

            // -------------------------------------------------
            // Static obstacle
            // -------------------------------------------------

            if (IsInLayerMask(
                hit.collider.gameObject.layer,
                obstacleMask))
                return true;

            // -------------------------------------------------
            // Grabbable object
            // -------------------------------------------------

            if (IsInLayerMask(
                hit.collider.gameObject.layer,
                grabbableMask))
            {
                // If this is the object we actually want to reach,
                // don't consider it an obstacle.
                if (IsSameTarget(hit.collider.transform, intendedTarget))
                    return false;

                // Otherwise treat it as a temporary obstacle.
                return true;
            }

            return false;
        }

        private bool IsInLayerMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        private bool IsSameTarget(Transform hitTransform, Transform intendedTarget)
        {
            if (hitTransform == null || intendedTarget == null)
                return false;

            return hitTransform == intendedTarget ||
                   hitTransform.IsChildOf(intendedTarget) ||
                   intendedTarget.IsChildOf(hitTransform);
        }

        private Vector3 GetAvoidanceDirection(Vector3 desiredDirection, Vector3 targetPos)
        {
            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude < 0.001f)
                return Vector3.zero;
            desiredDirection.Normalize();

            // Continue using the same side while committed
            // to navigating around the obstacle.
            if (Time.time < avoidanceEndTime && avoidanceSide != 0)
            {
                Vector3 committedDirection = avoidanceSide < 0
                        ? Vector3.Cross(Vector3.up, desiredDirection)
                        : Vector3.Cross(desiredDirection, Vector3.up);

                if (!IsPathBlocked(committedDirection, null))
                    return committedDirection;
            }

            // Otherwise perform a fresh left/right evaluation
            Vector3 leftDirection = Quaternion.Euler(0f, -60f, 0f) * desiredDirection;

            Vector3 rightDirection = Quaternion.Euler(0f, 60f, 0f) * desiredDirection;

            bool leftBlocked = IsPathBlocked(leftDirection, null);
            bool rightBlocked = IsPathBlocked(rightDirection, null);

            // ---------------------------------------------------------
            // Both sides blocked.
            // Try moving more perpendicular to the obstacle.
            // ---------------------------------------------------------
            if (leftBlocked && rightBlocked)
            {
                Vector3 left = Quaternion.Euler(0f, -90f, 0f) * desiredDirection;

                Vector3 right = Quaternion.Euler(0f, 90f, 0f) * desiredDirection;

                bool leftSideBlocked = IsPathBlocked(left, null);
                bool rightSideBlocked = IsPathBlocked(right, null);

                if (!leftSideBlocked && !rightSideBlocked)
                {
                    return ChooseBetterSide(left, right, targetPos);
                }

                if (!leftSideBlocked) return left;
                if (!rightSideBlocked) return right;

                // Completely surrounded.
                return GetRecoveryDirection(desiredDirection);
            }

            // ---------------------------------------------------------
            // One side is blocked.
            // Take the open side.
            // ---------------------------------------------------------
            if (leftBlocked && !rightBlocked)
            {
                avoidanceSide = 1;
                avoidanceEndTime = Time.time + avoidanceCommitTime;

                return rightDirection;
            }
            if (rightBlocked && !leftBlocked)
            {
                avoidanceSide = -1;
                avoidanceEndTime = Time.time + avoidanceCommitTime;

                return leftDirection;
            }

            // ---------------------------------------------------------
            // Both sides are open.
            // Prefer the side that gets us closer to the target.
            // ---------------------------------------------------------
            return ChooseBetterSide(leftDirection, rightDirection, targetPos);
        }

        private Vector3 ChooseBetterSide(Vector3 leftDirection, Vector3 rightDirection, Vector3 targetPos)
        {
            Vector3 toTarget = targetPos - coreRb.position;

            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.001f)
                return leftDirection;

            toTarget.Normalize();

            float leftScore = Vector3.Dot(leftDirection, toTarget);

            float rightScore = Vector3.Dot(rightDirection, toTarget);

            if (leftScore > rightScore)
            {
                avoidanceSide = -1;
                avoidanceEndTime = Time.time + avoidanceCommitTime;

                return leftDirection;
            }
            else
            {
                avoidanceSide = 1;
                avoidanceEndTime = Time.time + avoidanceCommitTime;

                return rightDirection;
            }
        }

        private void UpdateStuckDetection()
        {
            if (coreRb == null) return;

            Vector3 currentPosition = coreRb.position;
            currentPosition.y = 0f;

            Vector3 previousPosition = lastMovementCheckPosition;
            previousPosition.y = 0f;

            float distanceMoved = Vector3.Distance(currentPosition, previousPosition);

            if (distanceMoved >= minimumProgress)
            {
                // AI is actually moving.
                stuckTimer = 0f;
                lastMovementCheckPosition = currentPosition;
                return;
            }

            stuckTimer += Time.deltaTime;

            if (stuckTimer >= stuckTime)
            {
                ForceAvoidanceRecovery();
            }
        }

        private void ForceAvoidanceRecovery()
        {
            stuckTimer = 0f;

            // Reverse the currently chosen side.
            if (avoidanceSide == 0)
                avoidanceSide = Random.value < 0.5f ? -1 : 1;
            else
                avoidanceSide *= -1;

            avoidanceEndTime =
                Time.time + avoidanceCommitTime;

            lastMovementCheckPosition = coreRb.position;
        }

        private Vector3 GetRecoveryDirection(Vector3 desiredDirection)
        {
            desiredDirection.y = 0f;
            desiredDirection.Normalize();

            Vector3 left = Vector3.Cross(Vector3.up, desiredDirection);

            Vector3 right = -left;

            if (avoidanceSide < 0)
            {
                if (!IsPathBlocked(left, null))
                    return left;

                if (!IsPathBlocked(right, null))
                    return right;
            }
            else
            {
                if (!IsPathBlocked(right, null))
                    return right;

                if (!IsPathBlocked(left, null))
                    return left;
            }

            // Nothing available.
            return Vector3.zero;
        }

        private void ResetObstacleAvoidance()
        {
            avoidanceSide = 0;
            stuckTimer = 0f;
        }

        /// <summary>
        /// Find nearest player within chase range
        /// </summary>
        private Transform FindNearestPlayer()
        {
            if (coreRb == null) return null;

            PhysicCharacterController[] allControllers = FindObjectsByType<PhysicCharacterController>();
            Transform nearest = null;
            float nearestDist = chaseRange;

            foreach (var ctrl in allControllers)
            {
                if (ctrl == controller) continue;

                var health = ctrl.GetComponent<PlayerHealth>();
                if (health != null && health.CurrentHealth <= 0) continue;

                Rigidbody otherRb = ctrl.CoreRigidbody;
                if (otherRb == null) continue;

                float dist = Vector3.Distance(coreRb.position, otherRb.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    // Return the Hips bone (core rigidbody) for correct position/movement targeting.
                    // IsTargetArmed uses GetComponentInParent<> so it still finds the controller on the root.
                    nearest = otherRb.transform;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find a random grabbable object within retreat range that's safe from the threat.
        /// Returns null if none are suitable. Picking randomly rather than the nearest
        /// makes multiple AIs less likely to converge on the same object.
        /// </summary>
        /// <summary>
        /// Find a random grabbable object within retreat range that's safe from the threat.
        /// Returns null if none are suitable. Picking randomly rather than the nearest
        /// makes multiple AIs less likely to converge on the same object.
        /// </summary>
        private GrabbableObject FindRetreatGrabbable(Transform threat)
        {
            if (coreRb == null) return null;

            Collider[] hits = Physics.OverlapSphere(coreRb.position + Vector3.up * 1f, retreatGrabRange, grabbableMask);

            List<GrabbableObject> valid = new List<GrabbableObject>();
            foreach (var hit in hits)
            {
                var grabbable = hit.GetComponent<GrabbableObject>();
                if (grabbable == null) continue;

                // Threat-proximity filter: discard objects that are much closer to the
                // threat than to us (the threat would grab them first).
                if (threat != null)
                {
                    float distToThreat = Vector3.Distance(hit.transform.position, threat.position);
                    float distMeToThreat = Vector3.Distance(coreRb.position, threat.position);
                    if (distToThreat < distMeToThreat - 0.5f) continue;
                }

                // Held objects: only grab if close AND the holder is armed/threatening
                if (grabbable.IsHeld)
                {
                    float distToGrabbable = Vector3.Distance(coreRb.position, grabbable.transform.position);
                    float distThreatToGrabbable = threat != null ? Vector3.Distance(threat.position, grabbable.transform.position) : 0f;
                    float distMeToThreat = threat != null ? Vector3.Distance(coreRb.position, threat.position) : 0f;
                    // Only grab a held object if we are closer than the threat
                    if (distToGrabbable < 2.5f && distThreatToGrabbable > distToGrabbable - 0.5f)
                        valid.Add(grabbable);
                }
                else
                {
                    // Unheld objects are always valid at this point
                    valid.Add(grabbable);
                }
            }

            if (valid.Count == 0) return null;

            return valid[Random.Range(0, valid.Count)];
        }

        /// <summary>
        /// Find a random grabbable object in a wider radius around the AI
        /// Uses sphere overlap for 360-degree detection (not just in front)
        /// </summary>
        private GrabbableObject FindRandomGrabbable()
        {
            if (coreRb == null) return null;

            // Use sphere around the AI for wider detection — not just in front
            Collider[] hits = Physics.OverlapSphere(coreRb.position + Vector3.up * 1f, grabSearchRadius, grabbableMask);

            List<GrabbableObject> valid = new List<GrabbableObject>();

            foreach (var hit in hits)
            {
                var grabbable = hit.GetComponent<GrabbableObject>();
                if (grabbable == null || grabbable.IsHeld) continue;

                valid.Add(grabbable);
            }

            if (valid.Count == 0) return null;

            // Pick one at random — more diverse behavior across multiple AIs
            return valid[Random.Range(0, valid.Count)];
        }

        /// <summary>
        /// Check if target is holding a grabbable object (armed)
        /// </summary>
        private bool IsTargetArmed(Transform target)
        {
            if (target == null) return false;
            // The target passed in may be a child bone (e.g. Hips). Climb to the
            // root GameObject where PhysicCharacterController actually lives.
            var targetController = target.GetComponentInParent<PhysicCharacterController>();
            if (targetController == null)
            {
                return false;
            }
            return targetController.IsHoldingObject;
        }

        /// <summary>
        /// Predict where the target will be and aim to cut them off.
        /// Instead of naively extrapolating current velocity (which makes the AI
        /// orbit alongside a circling player), we bias the aim point toward the
        /// inside of the player's path so the AI closes the gap across the circle.
        /// </summary>
        private Vector3 InterceptPosition(Transform target)
        {
            if (target == null) return coreRb.position;

            // Get the target's core rigidbody (may be on a child Hips bone)
            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            if (targetRb == null)
            {
                var pc = target.GetComponent<PhysicCharacterController>();
                if (pc != null) targetRb = pc.CoreRigidbody;
            }
            if (targetRb == null) return target.position;

            Vector3 targetPos = targetRb.position;
            Vector3 toTarget = targetPos - coreRb.position;
            toTarget.y = 0;
            float distance = toTarget.magnitude;
            if (distance < 0.001f) return targetPos;

            // Smooth target velocity to avoid jittery updates from Hips root
            float now = Time.time;
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, targetRb.linearVelocity, velocitySmoothing);
            lastTargetPos = targetPos;
            lastTargetTime = now;

            // Fixed lead time (seconds) instead of distance-based ETA for more predictable behavior
            float eta = leadTime;
            eta = Mathf.Clamp(eta, 0.1f, 5f);

            // Predict where target will be (more ahead)
            Vector3 predicted = targetPos + smoothedVelocity * eta;

            // Bias further toward cutting inside the circle (reduce Lerp weight from 0.5 to 0.3)
            // Lower Lerp value = aim point is closer to predicted (further ahead), making the intercept more aggressive.
            Vector3 aimPoint = Vector3.Lerp(targetPos, predicted, 0.3f);

            return aimPoint;
        }

        /// <summary>
        /// Given a desired retreat direction, check if it hits a wall.
        /// If clear, return it as-is. If blocked, try sliding left/right
        /// along the wall. If both sides are blocked, return zero (stop).
        /// </summary>
        private Vector2 GetSafeRetreatDirection(Vector3 desiredDir)
        {
            float checkDist = 2f;
            int wallMask = LayerMask.GetMask("Default", "Wall", "Environment");

            // Try the desired direction first
            if (!Physics.Raycast(coreRb.position + Vector3.up * 0.5f, desiredDir, checkDist, wallMask))
            {
                return new Vector2(desiredDir.x, desiredDir.z);
            }

            // Blocked — try perpendicular slide (left then right)
            Vector3 leftDir = Vector3.Cross(desiredDir, Vector3.up);  // perpendicular left
            Vector3 rightDir = -leftDir;                                // perpendicular right

            if (!Physics.Raycast(coreRb.position + Vector3.up * 0.5f, leftDir, checkDist, wallMask))
            {
                return new Vector2(leftDir.x, leftDir.z);
            }
            if (!Physics.Raycast(coreRb.position + Vector3.up * 0.5f, rightDir, checkDist, wallMask))
            {
                return new Vector2(rightDir.x, rightDir.z);
            }

            // Completely boxed in — stop
            return Vector2.zero;
        }

        /// <summary>
        /// Combat logic executed when the AI is unarmed.
        /// ARMED TARGET  -> only Bait / Back-Up / Charge can run (never punch/dodge/circle).
        /// UNARMED TARGET -> full personality (dodge, circle, retreat, punch).
        /// </summary>
    }
}
