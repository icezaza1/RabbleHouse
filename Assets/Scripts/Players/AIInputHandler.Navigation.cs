using System.Collections.Generic;
using UnityEngine;

namespace RabbleHouse
{
    public partial class AIInputHandler
    {
        private readonly List<AIWaypoint> waypointRoute = new List<AIWaypoint>();
        private int waypointRouteIndex;
        private Transform waypointRouteTarget;
        private Vector3 waypointRouteTargetPosition;
        private float waypointRouteRefreshTime;

        //  MOVEMENT HELPERS
        // -----------------------------------------------------------------------
        private void HandleChasing(Transform target, float distToTarget)
        {
            if (target == null) return;

            if (distToTarget < 1.5f)
                MoveToward(target.position, target);
            else
                MoveToward(InterceptPosition(target), target);
        }

        private void HandleGrabbableApproach(GrabbableObject grabbable, float distToGrabbable)
        {
            if (grabbable == null || coreRb == null)
                return;

            Vector3 direction = grabbable.transform.position - coreRb.position;
            direction.y = 0f;
            float distance = direction.magnitude;
            if (distance < 0.001f)
            {
                MoveInput = Vector2.zero;
                return;
            }

            direction /= distance;
            Vector3 rayOrigin = coreRb.position + Vector3.up * 0.25f;
            if (Physics.Raycast(rayOrigin, direction, out RaycastHit obstacleHit, distance,
                    obstacleMask, QueryTriggerInteraction.Ignore) &&
                obstacleHit.collider.bounds.max.y <= coreRb.position.y + 0.75f &&
                grabbable.transform.position.y > obstacleHit.collider.bounds.max.y - 0.5f)
            {
                // Use a reachable staging point on the AI's side of the
                // obstacle, then let the normal waypoint system route there.
                // The AI's pickup box can reach across the remaining gap.
                float standOff = obstacleCheckRadius + 0.1f;
                float approachDistance = Mathf.Max(0f, obstacleHit.distance - standOff);
                Vector3 approachPosition = coreRb.position + direction * approachDistance;
                approachPosition.y = coreRb.position.y;
                MoveToward(approachPosition, grabbable.transform);
                return;
            }

            HandleChasing(grabbable.transform, distToGrabbable);
        }

        private void MoveToward(Vector3 targetPos, Transform intendedTarget)
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

            if (!IsPathBlocked(desiredDirection, intendedTarget))
            {
                // No obstacle -> go directly toward target.
                ClearWaypointRoute();
                ResetObstacleAvoidance();

                SetMoveDirection(desiredDirection);

                UpdateStuckDetection();
                return;
            }

            // A waypoint route provides a stable destination around walls;
            // local steering remains the fallback for dynamic obstacles.
            if (TryGetWaypointDirection(targetPos, intendedTarget, out Vector3 waypointDirection))
            {
                ResetObstacleAvoidance();
                SetMoveDirection(waypointDirection);
                UpdateStuckDetection();
                return;
            }

            // ---------------------------------------------------------
            // Path is blocked -> find an avoidance direction.
            // ---------------------------------------------------------

            Vector3 avoidanceDirection = GetAvoidanceDirection(desiredDirection, targetPos, intendedTarget);

            SetMoveDirection(avoidanceDirection);

            UpdateStuckDetection();
        }

        private bool TryGetWaypointDirection(Vector3 targetPos, Transform intendedTarget, out Vector3 direction)
        {
            direction = Vector3.zero;
            bool needsRefresh = waypointRouteTarget != intendedTarget ||
                                waypointRoute.Count == 0 ||
                                waypointRouteIndex >= waypointRoute.Count ||
                                Time.time >= waypointRouteRefreshTime ||
                                (targetPos - waypointRouteTargetPosition).sqrMagnitude > 4f;
            if (needsRefresh)
            {
                waypointRoute.Clear();
                waypointRouteIndex = 0;
                waypointRouteTarget = intendedTarget;
                waypointRouteTargetPosition = targetPos;
                waypointRouteRefreshTime = Time.time + 0.5f;
                FindWaypointRoute(targetPos, intendedTarget, waypointRoute);
            }

            while (waypointRouteIndex < waypointRoute.Count && waypointRoute[waypointRouteIndex] == null)
                waypointRouteIndex++;

            while (waypointRouteIndex < waypointRoute.Count &&
                   HorizontalDistance(coreRb.position, waypointRoute[waypointRouteIndex].transform.position) < 0.8f)
                waypointRouteIndex++;

            if (waypointRouteIndex >= waypointRoute.Count)
                return false;

            Vector3 waypointPosition = waypointRoute[waypointRouteIndex].transform.position;
            direction = waypointPosition - coreRb.position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f;
        }

        private void FindWaypointRoute(Vector3 targetPos, Transform intendedTarget, List<AIWaypoint> route)
        {
            AIWaypoint[] nodes = FindObjectsByType<AIWaypoint>();
            if (nodes.Length == 0) return;

            int count = nodes.Length;
            float[] distances = new float[count];
            int[] previous = new int[count];
            bool[] visited = new bool[count];
            for (int i = 0; i < count; i++)
            {
                distances[i] = float.PositiveInfinity;
                previous[i] = -1;
                if (nodes[i] != null && HasClearSegment(coreRb.position, nodes[i].transform.position, null))
                    distances[i] = HorizontalDistance(coreRb.position, nodes[i].transform.position);
            }

            int bestGoal = -1;
            float bestTotalCost = float.PositiveInfinity;
            for (int step = 0; step < count; step++)
            {
                int current = -1;
                float currentDistance = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    if (!visited[i] && distances[i] < currentDistance)
                    {
                        current = i;
                        currentDistance = distances[i];
                    }
                }

                if (current < 0) break;
                visited[current] = true;

                float finalLeg = HorizontalDistance(nodes[current].transform.position, targetPos);
                if (currentDistance + finalLeg < bestTotalCost &&
                    HasClearSegment(nodes[current].transform.position, targetPos, intendedTarget))
                {
                    bestGoal = current;
                    bestTotalCost = currentDistance + finalLeg;
                }

                AIWaypoint[] neighbors = nodes[current].neighbors;
                if (neighbors == null) continue;
                for (int n = 0; n < neighbors.Length; n++)
                {
                    int neighborIndex = System.Array.IndexOf(nodes, neighbors[n]);
                    if (neighborIndex < 0 || visited[neighborIndex] ||
                        !HasClearSegment(nodes[current].transform.position, nodes[neighborIndex].transform.position, null))
                        continue;

                    float edgeCost = HorizontalDistance(nodes[current].transform.position, nodes[neighborIndex].transform.position);
                    float candidateDistance = currentDistance + edgeCost;
                    if (candidateDistance < distances[neighborIndex])
                    {
                        distances[neighborIndex] = candidateDistance;
                        previous[neighborIndex] = current;
                    }
                }
            }

            if (bestGoal < 0) return;
            List<AIWaypoint> reversedRoute = new List<AIWaypoint>();
            for (int node = bestGoal; node >= 0; node = previous[node])
                reversedRoute.Add(nodes[node]);
            for (int i = reversedRoute.Count - 1; i >= 0; i--)
                route.Add(reversedRoute[i]);
        }

        private bool HasClearSegment(Vector3 from, Vector3 to, Transform intendedTarget)
        {
            Vector3 delta = to - from;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance <= obstacleCheckRadius) return true;

            Vector3 direction = delta / distance;
            // Check at waypoint/core height as well as above it. A single high
            // cast can pass over low counters that still block the character.
            Vector3 lowOrigin = from;
            Vector3 highOrigin = from + Vector3.up * 0.5f;
            return !IsNavigationSweepBlocked(lowOrigin, direction, distance, intendedTarget) &&
                   !IsNavigationSweepBlocked(highOrigin, direction, distance, intendedTarget);
        }

        private bool IsNavigationSweepBlocked(Vector3 origin, Vector3 direction, float distance, Transform intendedTarget)
        {
            LayerMask mask = obstacleMask | grabbableMask;
            Transform heldObject = controller != null && controller.HeldObject != null
                ? controller.HeldObject.transform
                : null;
            Collider[] overlaps = Physics.OverlapSphere(origin, obstacleCheckRadius, mask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                // The carried object moves with this AI and must not be treated
                // as an obstacle blocking the route to the opponent.
                if (IsSameTarget(overlaps[i].transform, intendedTarget) ||
                    IsSameTarget(overlaps[i].transform, heldObject))
                    continue;

                int layer = overlaps[i].gameObject.layer;
                bool isAhead = Vector3.Dot(direction, overlaps[i].bounds.center - origin) > 0f;
                if (isAhead && (IsInLayerMask(layer, obstacleMask) || IsInLayerMask(layer, grabbableMask)))
                    return true;
            }

            RaycastHit[] hits = Physics.SphereCastAll(origin, obstacleCheckRadius, direction, distance,
                mask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].collider.transform;
                if (IsSameTarget(hitTransform, intendedTarget) ||
                    IsSameTarget(hitTransform, heldObject))
                    continue;

                int layer = hits[i].collider.gameObject.layer;
                if (IsInLayerMask(layer, obstacleMask) || IsInLayerMask(layer, grabbableMask))
                    return true;
            }

            return false;
        }

        private float HorizontalDistance(Vector3 from, Vector3 to)
        {
            from.y = 0f;
            to.y = 0f;
            return Vector3.Distance(from, to);
        }

        private void ClearWaypointRoute()
        {
            waypointRoute.Clear();
            waypointRouteIndex = 0;
            waypointRouteTarget = null;
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

            // The core rigidbody is at the hips. Sweep at that height and
            // above it so shorter counter colliders still block the route.
            return IsNavigationSweepBlocked(coreRb.position, direction, obstacleCheckDistance, intendedTarget) ||
                   IsNavigationSweepBlocked(coreRb.position + Vector3.up * 0.5f, direction,
                       obstacleCheckDistance, intendedTarget);
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

        private Vector3 GetAvoidanceDirection(Vector3 desiredDirection, Vector3 targetPos, Transform intendedTarget)
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

                if (!IsPathBlocked(committedDirection, intendedTarget))
                    return committedDirection;
            }

            // Probe progressively around the obstruction. The old two-angle
            // check could repeatedly select a direction that still ran along
            // the wall, or make no progress when the target was close to it.
            Vector3 bestDirection = Vector3.zero;
            float bestScore = float.NegativeInfinity;
            for (int angle = -150; angle <= 150; angle += 30)
            {
                Vector3 candidate = Quaternion.Euler(0f, angle, 0f) * desiredDirection;
                if (IsPathBlocked(candidate, intendedTarget)) continue;

                float score = Vector3.Dot(candidate, desiredDirection);
                // Keep a committed side when scores are nearly equal to avoid
                // switching sides every frame at a wall corner.
                int candidateSide = angle < 0 ? -1 : (angle > 0 ? 1 : 0);
                if (candidateSide == avoidanceSide) score += 0.12f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDirection = candidate;
                }
            }

            if (bestDirection.sqrMagnitude > 0.001f)
            {
                avoidanceSide = Vector3.Dot(Vector3.Cross(Vector3.up, desiredDirection), bestDirection) < 0f ? 1 : -1;
                avoidanceEndTime = Time.time + avoidanceCommitTime;
                return bestDirection.normalized;
            }

            return GetRecoveryDirection(desiredDirection);
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
