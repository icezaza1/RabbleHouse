using UnityEngine;

namespace RabbleHouse
{
    /// <summary>
    /// Type of grabbable object — affects how the character holds and attacks with it.
    /// </summary>
    public enum GrabbableType
    {
        SmallObject,  // Two-handed, LightAttack = heavy swing, HeavyAttack = throw
        LargeObject,  // Two-handed, LightAttack = heavy swing, heavier mass affects hip rotation
        Tool          // One-handed (right arm only), more details TBD
    }

    /// <summary>
    /// Minimal grabbable furniture component. Lets PhysicCharacterController
    /// pick up, carry, and throw furniture via temporary ConfigurableJoints.
    /// When thrown and colliding with another character it deals damage.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GrabbableObject : MonoBehaviour
    {
        public GrabbableType grabbableType = GrabbableType.SmallObject;

        [Header("Mass / Physics")]
        [Tooltip("Used to calculate how much the object resists hip rotation (LargeObject only).")]
        [SerializeField] private float hipRotationResistance = 10f;

        [Header("Damage (swung while held)")]
        [SerializeField] private int swingDamage = 15;
        [Tooltip("0-1 chance this swung object stuns vs knocks the target away.")]
        [SerializeField] private float swingStunChance = 0.3f;

        [Header("Damage (thrown / airborne)")]
        [SerializeField] private int throwDamage = 20;
        [Tooltip("0-1 chance a thrown object stuns. Very high for thrown weapons.")]
        [SerializeField] private float throwStunChance = 0.7f;
        [Tooltip("Minimum velocity needed for a thrown object to deal damage on impact.")]
        [SerializeField] private float throwMinSpeed = 3f;

        private Rigidbody rb;
        private bool isHeld = false;
        private PhysicCharacterController thrower; // set by PhysicCharacterController before throw

        public Rigidbody Rigidbody => rb;
        public bool IsHeld => isHeld;
        public float HipRotationResistance => hipRotationResistance;

        /// <summary>Damage dealt while held and swung.</summary>
        public int SwingDamage => swingDamage;
        public float SwingStunChance => swingStunChance;

        /// <summary>Register who threw this object (for self-damage prevention).</summary>
        public void SetThrower(PhysicCharacterController owner) => thrower = owner;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
        }

        /// <summary>Called by PhysicCharacterController when grabbed.</summary>
        public void GrabByPlayer(Object holder)
        {
            isHeld = true;
            thrower = holder as PhysicCharacterController;
        }

        /// <summary>Called by PhysicCharacterController when released.</summary>
        public void ReleaseByPlayer()
        {
            isHeld = false;
        }

        /// <summary>Called when punched or knocked.</summary>
        public void ApplyForce(Vector3 force)
        {
            if (rb != null)
                rb.AddForce(force, ForceMode.Impulse);
        }

        /// <summary>Called when thrown — the object now does damage on impact.</summary>
        public void ThrowByDirection(Vector3 velocity)
        {
            isHeld = false;
            if (rb != null)
            {
                rb.linearVelocity = velocity;
            }
        }

        /// <summary>
        /// Detect impact against a damageable character while airborne.
        /// Only deals damage if speed >= throwMinSpeed.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            // Only deal damage when the object is airborne (not held)
            if (isHeld || rb == null) return;

            float speed = rb.linearVelocity.magnitude;
            if (speed < throwMinSpeed) return;

            var targetHealth = collision.gameObject.GetComponent<PlayerHealth>();
            if (targetHealth == null) return;

            // Don't damage the thrower
            if (thrower != null && targetHealth.gameObject == thrower.gameObject) return;

            // Don't damage self — if the object was grabbed by a target, skip
            // (prevents double-damage if the object is stuck on a held character)
            if (targetHealth.GetComponent<PhysicCharacterController>() != null &&
                targetHealth.GetComponent<PhysicCharacterController>().IsHoldingObject)
                return;

            Vector3 hitDir = collision.contacts[0].point - transform.position;
            if (hitDir == Vector3.zero) hitDir = rb.linearVelocity.normalized;
            hitDir = hitDir.normalized;
            hitDir.y = 0.3f; // slight upward pop

            // Thrown objects: high stun chance, high damage, DO send away
            targetHealth.TakeDamage(throwDamage, hitDir, throwStunChance);

            // Knock the target away from the impact
            var targetController = targetHealth.GetComponent<PhysicCharacterController>();
            if (targetController != null)
            {
                targetController.ApplyKnockback(hitDir);
            }
        }
    }
}
