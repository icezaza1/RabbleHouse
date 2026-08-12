using UnityEngine;

public class ActiveRagdollBalancer : MonoBehaviour
{
    [SerializeField] private Transform animatedHips;
    [SerializeField] private float balanceStrength = 10000f;
    [SerializeField] private float balanceDamper = 200f;

    /// <summary>0..1 blend of corrective torque. Lower while the player moves/turns
    /// so physics-driven rotation wins; restore to 1 when idle to stand upright.</summary>
    [Range(0f, 1f)] public float weight = 1f;

    private Rigidbody hipsRigidbody;

    void Start()
    {
        hipsRigidbody = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (animatedHips == null) return;

        // Calculate the rotational difference between the physical hips and animated hips
        Quaternion rotationDifference = animatedHips.rotation * Quaternion.Inverse(transform.rotation);

        rotationDifference.ToAngleAxis(out float angleDegree, out Vector3 rotationAxis);

        // Convert angle to radians for physics calculations
        if (angleDegree > 180f) angleDegree -= 360f;

        if (rotationAxis != Vector3.zero && !float.IsNaN(rotationAxis.x))
        {
            Vector3 targetAngularVelocity = rotationAxis * (angleDegree * Mathf.Deg2Rad * balanceStrength);

            // Apply torque to the physics hips to force them to match the upright angle.
            // Scaled by weight (0 = off, 1 = full correction).
            hipsRigidbody.angularVelocity = Vector3.MoveTowards(
                hipsRigidbody.angularVelocity,
                targetAngularVelocity * weight,
                balanceDamper * Time.fixedDeltaTime
            );
        }
    }
}
