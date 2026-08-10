using UnityEngine;

namespace RabbleHouse
{
    /// <summary>
    /// Component attached to grabbable furniture items.
    /// Handles grab, throw, and damage logic for furniture.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class GrabbableObject : MonoBehaviour
    {
        [Header("Grabbable Settings")]
        [SerializeField] private float mass = 5f;
        [SerializeField] private float maxThrowForce = 30f;
        [SerializeField] private float damageOnHit = 10f;
        [SerializeField] private float durability = 1;
        [SerializeField] private string animTag = "IsGrabbable";
        [SerializeField] private float throwDebounceTime = 0.5f;

        [Header("Material")]
        [SerializeField] private ParticleSystem throwParticles;
        [SerializeField] private Renderer meshRenderer;

        [Header("State")]
        [SerializeField] private bool isHeld = false;
        [SerializeField] private bool isThrown = false;

        private Rigidbody rb;
        private PlayerController ownerController;
        private int ownerPlayerIndex;

        public bool IsHeld => isHeld;
        public bool IsThrown => isThrown;
        public float Durability => durability;
        public Rigidbody Rigidbody => rb;

        public event System.Action<int, GrabbableObject> OnGrabbed;
        public event System.Action<int, GrabbableObject> OnThrown;
        public event System.Action<int, GrabbableObject> OnDestroyEvent;

        public void GrabByPlayer(PlayerController controller)
        {
            if (isHeld || isThrown || ownerController != null) return;

            isHeld = true;
            ownerController = controller;
            ownerPlayerIndex = controller.PlayerIndex;

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            OnGrabbed?.Invoke(ownerPlayerIndex, this);
        }

        public void ReleaseByPlayer()
        {
            if (!isHeld) return;

            isHeld = false;
            isThrown = false;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
            }

            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = true;

            ownerController?.ReleaseObject(this);
            ownerController = null;
        }

        public void ThrowByDirection(Vector3 direction)
        {
            if (!isHeld || isThrown) return;

            isThrown = true;

            if (ownerController != null)
            {
                ownerController.ReleaseObject(this);
            }

            if (rb != null && direction != Vector3.zero)
            {
                rb.AddForce(direction * maxThrowForce, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 20f, ForceMode.Impulse);
            }

            if (throwParticles != null)
            {
                throwParticles.Play();
            }

            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }

            OnThrown?.Invoke(ownerPlayerIndex, this);

            // Re-enable after a brief delay to allow for pickup
            Invoke(nameof(ReenableAfterThrow), throwDebounceTime);
        }

        private void ReenableAfterThrow()
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
            }
            isThrown = false;
        }

        public void ApplyForce(Vector3 force)
        {
            if (rb == null) return;
            rb.AddForce(force);
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.mass = mass;
            rb.linearDamping = 2f;
            rb.angularDamping = 3f;
        }

        private void OnDestroy()
        {
            if (ownerController != null)
            {
                ownerController.ReleaseObject(this);
            }
            OnDestroyEvent?.Invoke(ownerPlayerIndex, this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isHeld ? Color.yellow : Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.15f);
        }
    }
}