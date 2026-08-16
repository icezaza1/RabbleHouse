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
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GrabbableObject : MonoBehaviour
    {
        public GrabbableType grabbableType = GrabbableType.SmallObject;

        [Header("Mass / Physics")]
        [Tooltip("Used to calculate how much the object resists hip rotation (LargeObject only).")]
        [SerializeField] private float hipRotationResistance = 10f;

        private Rigidbody rb;
        private bool isHeld = false;

        public Rigidbody Rigidbody => rb;
        public bool IsHeld => isHeld;
        public float HipRotationResistance => hipRotationResistance;

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

        /// <summary>Called when thrown.</summary>
        public void ThrowByDirection(Vector3 velocity)
        {
            isHeld = false;
            if (rb != null)
            {
                rb.linearVelocity = velocity;
            }
        }
    }
}