using UnityEngine;

namespace RabbleHouse
{
    public partial class AIInputHandler
    {
        // ATTACK RANGE SYSTEM

        /// <summary> 
        /// Returns true when the currently held object should be treated as 
        /// a ranged weapon. 
        /// 
        /// This is intentionally centralized so every part of the AI uses 
        /// the same ranged/melee classification. 
        /// </summary>
        private bool IsCurrentAttackTypeRanged() 
        { 
            if (controller == null || controller.HeldObject == null) return false; 
            return controller.HeldObject.AIRangeBonus >= rangedWeaponThreshold; 
        }

        /// <summary> 
        /// Gets the physical melee reach of the currently held object 
        /// in the direction of the target. 
        /// 
        /// BoxColliders use their actual transformed corners, meaning 
        /// object rotation affects the calculated reach. 
        /// 
        /// Other collider types fall back to their world-space bounds. 
        /// </summary>
        private float GetHeldObjectPhysicalReach(Vector3 attackDirection)
        {
            if (controller == null || controller.HeldObject == null) 
                return 0f;

            GrabbableObject heldObject = controller.HeldObject;
            Collider[] colliders = heldObject.GetComponentsInChildren<Collider>(true);

            if (colliders == null || colliders.Length == 0) 
                return 0f; 
            
            attackDirection.y = 0f;

            if (attackDirection.sqrMagnitude < 0.001f) 
                attackDirection = heldObject.transform.forward;

            attackDirection.Normalize();

            float maximumReach = 0f;
            foreach (Collider collider in colliders) 
            { 
                if (collider == null || !collider.enabled) continue; 
                float reach = GetColliderReach(collider, attackDirection); 

                if (reach > maximumReach) 
                    maximumReach = reach; 
            }

            return maximumReach;
        }
        private float GetColliderReach(Collider collider, Vector3 attackDirection)
        {
            Transform objectTransform = controller.HeldObject.transform;

            // BoxCollider
            // Calculate all 8 actual world-space corners.
            BoxCollider box = collider as BoxCollider;

            if (box != null)
            {
                Vector3 halfSize = box.size * 0.5f; 
                Vector3[] localCorners = 
                { 
                    box.center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), 
                    box.center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z), 
                    box.center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z), 
                    box.center + new Vector3(-halfSize.x, halfSize.y, halfSize.z), 
                    box.center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z), 
                    box.center + new Vector3(halfSize.x, -halfSize.y, halfSize.z), 
                    box.center + new Vector3(halfSize.x, halfSize.y, -halfSize.z), 
                    box.center + new Vector3(halfSize.x, halfSize.y, halfSize.z)
                };

                float maxProjection = 0f;

                foreach (Vector3 localCorner in localCorners)
                {
                    Vector3 worldCorner = box.transform.TransformPoint(localCorner);
                    Vector3 relative = worldCorner - objectTransform.position;

                    relative.y = 0f; 
                    float projection = Vector3.Dot(relative, attackDirection); 
                    if (projection > maxProjection) maxProjection = projection;
                }
                return maxProjection;
            }

            // SphereCollider
            SphereCollider sphere = collider as SphereCollider;
            if (sphere != null)
            {
                Vector3 center = sphere.transform.TransformPoint(sphere.center);
                float scale = Mathf.Max(Mathf.Abs(sphere.transform.lossyScale.x), 
                    Mathf.Abs(sphere.transform.lossyScale.y), 
                    Mathf.Abs(sphere.transform.lossyScale.z));

                float radius = sphere.radius * scale; 
                Vector3 relative = center - objectTransform.position; 
                relative.y = 0f;
                
                return Vector3.Dot(relative, attackDirection) + radius;
            }

            // CapsuleCollider
            CapsuleCollider capsule = collider as CapsuleCollider;
            if (capsule != null)
            {
                Bounds bounds = capsule.bounds; 

                Vector3 centerRelative = bounds.center - objectTransform.position; 
                centerRelative.y = 0f; 
                Vector3 extents = bounds.extents; 
                extents.y = 0f; 
                
                return Vector3.Dot(centerRelative, attackDirection) + extents.magnitude;
            }

            // Generic fallback
            Bounds fallbackBounds = collider.bounds;
            Vector3 fallbackRelative = fallbackBounds.center - objectTransform.position; 
            fallbackRelative.y = 0f; Vector3 fallbackExtents = fallbackBounds.extents; 
            fallbackExtents.y = 0f; 
            
            return Vector3.Dot(fallbackRelative, attackDirection) + fallbackExtents.magnitude;
        }
        private float GetEffectiveAttackRange(Transform target)
        {
            if (target == null) 
                return attackRange; 
            
            if (controller == null || !controller.IsHoldingObject || controller.HeldObject == null) 
                return attackRange;

            GrabbableObject heldObject = controller.HeldObject; // Ranged weapons use their AI range bonus.
            if (IsCurrentAttackTypeRanged()) 
                return attackRange + heldObject.AIRangeBonus;

            // ------------------------------------------------------------- 
            // Melee weapon 
            // -------------------------------------------------------------
            Vector3 directionToTarget = target.position - coreRb.position;

            directionToTarget.y = 0f; 
            if (directionToTarget.sqrMagnitude < 0.001f) 
                return attackRange; directionToTarget.Normalize(); 
            
            float physicalReach = GetHeldObjectPhysicalReach(directionToTarget); 

            return attackRange + physicalReach + meleeRangePadding;
        }

        /// <summary> 
        /// Determines whether the target is physically within melee range. 
        /// 
        /// This checks: 
        /// 1. Horizontal distance. 
        /// 2. Held object's attack-facing direction.
        /// 3. Physical collider reach. 
        /// </summary>
        private bool IsTargetInMeleeRange(Transform target)
        {
            if (target == null || coreRb == null) 
                return false;

            if (controller == null || !controller.IsHoldingObject || controller.HeldObject == null) 
            { 
                return GetHorizontalDistance(coreRb.position, target.position) <= attackRange; 
            }

            if (IsCurrentAttackTypeRanged()) return false; 

            Vector3 toTarget = target.position - coreRb.position;

            toTarget.y = 0f; 
            
            float distance = toTarget.magnitude; 
            
            if (distance < 0.001f) 
                return true; 
            
            Vector3 directionToTarget = toTarget / distance;

            // ------------------------------------------------------------- 
            // Check attack direction. 
            // The held object's rotation now matters. 
            // -------------------------------------------------------------
            Vector3 objectForward = controller.HeldObject.transform.forward; 
            objectForward.y = 0f; 
            if (objectForward.sqrMagnitude > 0.001f) 
            { 
                objectForward.Normalize(); 
                float angle = Vector3.Angle(objectForward, directionToTarget); 

                if (angle > meleeAttackAngle) 
                    return false; 
            }

            // ------------------------------------------------------------- 
            // Calculate actual physical reach in this direction. 
            // -------------------------------------------------------------
            float effectiveRange = GetEffectiveAttackRange(target); 
            return distance <= effectiveRange;
        }
        private float GetHorizontalDistance(Vector3 a, Vector3 b)
        { 
            a.y = 0f; 
            b.y = 0f; 
            return Vector3.Distance(a, b);
        }

    }
}
