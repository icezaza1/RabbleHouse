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
        [Tooltip("Used to calculate how heavy the object is when being held.")]
        [SerializeField] private float heldMass = 1f;
        [Tooltip("Used to calculate how much the object resists hip rotation (LargeObject only).")]
        [SerializeField] private float hipRotationResistance = 10f;

        [Header("Damage (swung while held)")]
        [SerializeField] private int swingDamage = 15;
        [Tooltip("0-1 chance a swung object knocks the target down (launches them).")]
        [SerializeField] private float swingStunChance = 0.3f;
        [Tooltip("Extra attack/punch range this object provides when held (meters). Larger objects reach farther.")]
        [SerializeField] private float attackRangeBonus = 0f;
        [Tooltip("Extra attack range detection this object provides when held (meters) for AI.")]
        [SerializeField] private float aiRangeBonus = 0f;

        [Header("Damage (thrown / airborne)")]
        [SerializeField] private int throwDamage = 20;
        [Tooltip("0-1 chance a thrown object knocks the target down (launches them).")]
        [SerializeField] private float throwStunChance = 0.7f;
        [Tooltip("Minimum velocity needed for a thrown object to deal damage on impact.")]
        [SerializeField] private float throwMinSpeed = 3f;
        [Tooltip("Impulse applied to launch the target when this object hits (swing or throw). -1 = use target's default knockbackForce.")]
        [SerializeField] private float knockbackForce = -1f;

        private Rigidbody rb;
        private float originMass;
        private bool isHeld = false;
        private bool isThrown = false; // only deal damage after explicit throw
        private PhysicCharacterController thrower; // set by PhysicCharacterController before throw

        public Rigidbody Rigidbody => rb;
        public bool IsHeld => isHeld;
        public float HipRotationResistance => hipRotationResistance;

        /// <summary>Damage dealt while held and swung.</summary>
        public int SwingDamage => swingDamage;
        public float SwingStunChance => swingStunChance;
        public float KnockbackForce => knockbackForce;
        public float AttackRangeBonus => attackRangeBonus;
        public float AIRangeBonus => aiRangeBonus;

        /// <summary>Register who threw this object (for self-damage prevention).</summary>
        public void SetThrower(PhysicCharacterController owner) => thrower = owner;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            originMass = rb.mass;
        }

        /// <summary>Called by PhysicCharacterController when grabbed.</summary>
        public void GrabByPlayer(Object holder)
        {
            rb.mass = heldMass;
            isHeld = true;
            thrower = holder as PhysicCharacterController;
        }

        /// <summary>Called by PhysicCharacterController when released.</summary>
        public void ReleaseByPlayer()
        {
            rb.mass = originMass;
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
            rb.mass = originMass;
            isHeld = false;
            isThrown = true;
            if (rb != null)
            {
                rb.linearVelocity = velocity;
            }
        }

        /// <summary>
        /// Detect impact against a damageable character after an explicit throw.
        /// Only deals damage if the object was thrown (isThrown) AND speed >= throwMinSpeed.
        /// A character merely bumping into a resting object will not trigger this.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            // Only deal damage when the object was thrown, not when held or just bumped
            if (isHeld || !isThrown || rb == null) return;

            float speed = rb.linearVelocity.magnitude;
            if (speed < throwMinSpeed) return;

            // Colliders live on the ragdoll bones; health/controller live on the root
            var targetHealth = collision.gameObject.GetComponentInParent<PlayerHealth>();
            if (targetHealth == null) return;

            // Don't damage the thrower
            if (thrower != null && targetHealth.gameObject == thrower.gameObject) return;

            Vector3 hitDir = collision.contacts[0].point - transform.position;
            if (hitDir == Vector3.zero) hitDir = rb.linearVelocity.normalized;
            hitDir = hitDir.normalized;
            hitDir.y = 0.3f; // slight upward pop

            // Thrown objects: high stun chance, high damage, DO send away
            targetHealth.TakeDamage(throwDamage, hitDir, HitType.Knockdown, throwStunChance);

            // Knock the target away from the impact
            var targetController = targetHealth.GetComponentInParent<PhysicCharacterController>();
            if (targetController != null)
            {
                targetController.ApplyKnockback(hitDir, knockbackForce);
            }

            // Consume the throw — object must be re-thrown to deal damage again
            isThrown = false;
        }
    }
}
