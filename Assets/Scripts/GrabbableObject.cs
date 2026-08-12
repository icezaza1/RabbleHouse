using UnityEngine;

namespace RabbleHouse
{
    /// <summary>
    /// Minimal grabbable furniture component. Lets PhysicCharacterController
    /// pick up, carry, and throw furniture via a temporary ConfigurableJoint.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GrabbableObject : MonoBehaviour
    {
        private Rigidbody rb;
        private bool isHeld = false;

        public Rigidbody Rigidbody => rb;
        public bool IsHeld => isHeld;

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