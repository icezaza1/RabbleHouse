using UnityEngine;

public class RoombaController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 5f;

    [Header("Collision Detection")]
    [SerializeField] private float collisionCheckDistance = 0.5f;  // Raycast distance
    [SerializeField] private float collisionTimeout = 0.1f;  // Delay between collision checks
    private int collisionLayerMask;  // Layer mask for obstacles

    [Header("Randomization")]
    [SerializeField] private float minRandomAngle = 30f;  // Minimum turn angle
    [SerializeField] private float maxRandomAngle = 150f;  // Maximum turn angle
    [SerializeField] private float directionSmoothing = 5f;  // Smooth direction changes

    private Rigidbody rb;
    private Vector3 moveDirection;
    private float lastCollisionCheckTime;
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        SetRandomDirection();

        // Setup collision layer mask
        collisionLayerMask = LayerMask.GetMask("Default", "Wall", "Environment", "Grabbable");
    }

    private void FixedUpdate()
    {
        // Maintain movement velocity
        Vector3 targetVelocity = moveDirection * speed;
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, Time.fixedDeltaTime * directionSmoothing);

        // Auto-rotation to face movement direction
        RotateToFaceDirection();
        // Check for collisions periodically (avoid excessive checking)
        CheckForCollisions();
    }

    private void RotateToFaceDirection()
    {
        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 10f));
        }
    }

    private void CheckForCollisions()
    {
        if (Time.time - lastCollisionCheckTime < collisionTimeout)
            return;

        lastCollisionCheckTime = Time.time;

        // Cast forward to detect obstacles
        Vector3 forward = transform.forward;
        RaycastHit hit;

        if (Physics.Raycast(transform.position, forward, out hit, collisionCheckDistance, collisionLayerMask))
        {
            Debug.Log($"[RoombaController] Collision detected with {hit.collider.gameObject.name}");
            SetRandomDirection();
        }
    }

    private void SetRandomDirection()
    {
        // Pick a random turn angle between your min and max limits
        float randomAngle = Random.Range(minRandomAngle, maxRandomAngle);

        // Randomly decide to turn Left (-1) or Right (1)
        float turnDirection = Random.value > 0.5f ? 1f : -1f;
        float finalAngle = randomAngle * turnDirection;

        // Create a rotation quaternion around the Y-axis (up vector)
        Quaternion rotation = Quaternion.AngleAxis(finalAngle, Vector3.up);

        // Rotate the CURRENT move direction by that angle to get the NEW direction
        moveDirection = (rotation * moveDirection).normalized;
    }
}
