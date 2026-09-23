using UnityEngine;
using System.Collections;

namespace RabbleHouse
{
    public partial class PhysicCharacterController
    {
        public void OnStunned(float duration)
        {
            SetState(CharacterState.Stunned);
            StartCoroutine(StunRoutine(duration));
        }

        private IEnumerator StunRoutine(float duration)
        {
            ForceDropObject();
            ragdollMaster.EnableFullRagdoll();
            yield return new WaitForSeconds(duration);

            // Don't revive if character died while stunned
            if (playerHealth != null && playerHealth.IsDead) yield break;

            ragdollMaster.EnableActiveRagdoll();
            SetState(CharacterState.Idle);
            if (playerHealth != null) playerHealth.NotifyRecovered();
        }

        public void OnKnockdown(float duration)
        {
            SetState(CharacterState.Ragdoll);
            ForceDropObject();
            ragdollMaster.EnableFullRagdoll();
            StartCoroutine(KnockdownRoutine(duration));
        }

        private IEnumerator KnockdownRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);

            float elapsed = 0f;
            while (elapsed < knockdownDuration && elapsed < 2f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Don't revive if character died while knocked down
            if (playerHealth != null && playerHealth.IsDead) yield break;

            ragdollMaster.EnableActiveRagdoll();
            coreRigidbody.linearVelocity = Vector3.zero;
            coreRigidbody.angularVelocity = Vector3.zero;
            SetState(CharacterState.Idle);
            if (playerHealth != null) playerHealth.NotifyRecovered();
        }

        /// <summary>Called by PlayerHealth when health reaches 0 — character is "dead".</summary>
        public void OnDead()
        {
            SetState(CharacterState.Dead);
            ForceDropObject();
            ragdollMaster.EnableFullRagdoll();

            // Change layer to grabbable (WIP)
            ActiveRagdollBone[] ragdollBones = GetComponentsInChildren<ActiveRagdollBone>();
            GrabbableObject selfBodyObject = coreRigidbody.GetComponent<GrabbableObject>();
            int targetLayer = LayerMask.NameToLayer("Grabbable");

            if (selfBodyObject == null) return;

            coreRigidbody.gameObject.layer = targetLayer;
            foreach (var bone in ragdollBones)
            {
                bone.gameObject.layer = targetLayer;

                // Each ragdoll bone has its own Rigidbody, so its collision callback
                // does not reach the root GrabbableObject. Forward limb impacts there.
                if (bone.gameObject != coreRigidbody.gameObject &&
                    bone.GetComponent<Rigidbody>() != null)
                {
                    var relay = bone.GetComponent<GrabbableCollisionRelay>();
                    if (relay == null)
                        relay = bone.gameObject.AddComponent<GrabbableCollisionRelay>();
                    relay.Initialize(selfBodyObject);
                }
            }
        }

        /// <summary>
        /// Apply an impulse to the core body (used for knockback / sent-flying).
        /// Temporarily unlocks the hip joint's linear motion so the impulse can
        /// actually move the body (joint normally constrains position in active ragdoll).
        /// Pass a custom force (e.g. from a thrown object); defaults to this character's knockbackForce.
        /// </summary>
        public void ApplyKnockback(Vector3 direction, float overrideForce = -1f)
        {
            if (coreRigidbody == null) return;

            float force = overrideForce >= 0f ? overrideForce : knockbackForce;

            // Save original joint motion BEFORE unlocking
            SaveHipJointLinear();

            // Unlock hip joint linear motion so AddForce can move the body freely
            if (hipJoint != null)
            {
                hipJoint.xMotion = ConfigurableJointMotion.Free;
                hipJoint.yMotion = ConfigurableJointMotion.Free;
                hipJoint.zMotion = ConfigurableJointMotion.Free;
            }

            Vector3 dir = direction.normalized;
            dir.y = 0.3f; // slight upward pop
            coreRigidbody.AddForce(dir.normalized * force, ForceMode.Impulse);

            // Re-lock after the knockdown duration (body will be floppy anyway)
            StartCoroutine(RestoreHipJointLinear(knockdownDuration));
        }

        private ConfigurableJointMotion savedJointX, savedJointY, savedJointZ;
        private void SaveHipJointLinear()
        {
            if (hipJoint == null) return;
            savedJointX = hipJoint.xMotion;
            savedJointY = hipJoint.yMotion;
            savedJointZ = hipJoint.zMotion;
        }

        private IEnumerator RestoreHipJointLinear(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (hipJoint != null)
            {
                hipJoint.xMotion = savedJointX;
                hipJoint.yMotion = savedJointY;
                hipJoint.zMotion = savedJointZ;
            }
        }

        /// <summary>
        /// Check for a damageable character within punchRange in front of us and apply damage.
        /// Uses an OverlapSphere at the core body position, filtering to a forward cone.
        /// </summary>
        private bool CheckHit(int damage, float force, HitType hitType, float effectChance, bool sendAway, float overrideKnockbackForce = -1f)
        {
            if (coreRigidbody == null) return false;

            Collider[] hits = Physics.OverlapSphere(coreRigidbody.position + Vector3.up * 0.5f, punchRange + (heldObject?.AttackRangeBonus ?? 0f));
            Vector3 forward = coreRigidbody.transform.forward;

            foreach (var hit in hits)
            {
                // Colliders live on the ragdoll bones; health/controller live on the root
                var targetHealth = hit.GetComponentInParent<PlayerHealth>();
                if (targetHealth == null) continue;
                if (targetHealth.gameObject == this.gameObject) continue; // don't hit self

                // Must be roughly in front of us
                Vector3 toTarget = (hit.transform.position - coreRigidbody.position).normalized;
                if (Vector3.Dot(toTarget, forward) < 0.2f) continue;

                Vector3 knockDir = sendAway ? toTarget : Vector3.zero;
                targetHealth.TakeDamage(damage, knockDir, hitType, effectChance, PlayerIndex);

                if (sendAway)
                    ApplyKnockbackTo(targetHealth, knockDir, overrideKnockbackForce);
                return true;
            }
            return false;
        }

        private void ApplyKnockbackTo(PlayerHealth target, Vector3 direction, float overrideForce = -1f)
        {
            var ctrl = target.GetComponentInParent<PhysicCharacterController>();
            if (ctrl != null)
                ctrl.ApplyKnockback(direction, overrideForce);
        }

    }
}
