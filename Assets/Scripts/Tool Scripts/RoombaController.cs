using RabbleHouse;
using System.Collections.Generic;
using UnityEngine;

public class RoombaController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 5f;

    [Header("Combat Settings")]
    [SerializeField] private int damage;
    [SerializeField] private float stunChance;

    [Header("Randomization")]
    [SerializeField] private float minRandomAngle = 30f;  // Minimum turn angle
    [SerializeField] private float maxRandomAngle = 150f;  // Maximum turn angle
    [SerializeField] private float directionSmoothing = 5f;  // Smooth direction changes

    private Rigidbody rb;
    private Vector3 moveDirection;
    private HashSet<PlayerHealth> playersHit = new HashSet<PlayerHealth>();

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        // If we don't have a direction yet, choose a completely random direction
        if (moveDirection == Vector3.zero)
        {
            float randomAngle = Random.Range(0f, 360f);

            moveDirection = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward;
            moveDirection.Normalize();

            return;
        }
    }

    private void FixedUpdate()
    {
        // Maintain movement velocity
        Vector3 targetVelocity = moveDirection * speed;
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, Time.fixedDeltaTime * directionSmoothing);

        // Auto-rotation to face movement direction
        RotateToFaceDirection();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Ignore ground collisions
        if (collision.gameObject.CompareTag("Ground"))
            return;

        // If collision is player, knock them down
        if (collision.gameObject.CompareTag("Player"))
        {
            var targetHealth = collision.gameObject.GetComponentInParent<PlayerHealth>();

            if (targetHealth == null)
                return;

            // Already damaged this player during this collision
            if (playersHit.Contains(targetHealth))
                return;

            // Mark player as hit
            playersHit.Add(targetHealth);

            // Set knockback direction
            Vector3 hitDir = collision.GetContact(0).point - transform.position;
            if (hitDir == Vector3.zero) hitDir = rb.linearVelocity.normalized;
            hitDir = hitDir.normalized;
            hitDir.y = 0.3f; // slight upward pop

            // Trip target
            targetHealth.TakeDamage(damage, hitDir, HitType.Knockdown, stunChance);

            // Knock the target away from the impact
            var targetController = targetHealth.GetComponentInParent<PhysicCharacterController>();
            if (targetController != null)
            {
                targetController.ApplyKnockback(hitDir, 100f);
            }

            return;
        }

        // Other collision behavior
        // Get the average collision normal
        Vector3 normal = collision.GetContact(0).normal;

        // Generate a random direction
        float randomAngle = Random.Range(minRandomAngle, maxRandomAngle);

        if (Random.value < 0.5f)
            randomAngle *= -1f;

        Vector3 newDirection =
            Quaternion.Euler(0f, randomAngle, 0f) * moveDirection;

        // Make sure we're moving away from the surface
        if (Vector3.Dot(newDirection, normal) < 0)
        {
            newDirection = Vector3.Reflect(newDirection, normal);
        }

        moveDirection = newDirection.normalized;
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        var targetHealth = collision.gameObject.GetComponentInParent<PlayerHealth>();

        if (targetHealth != null)
        {
            playersHit.Remove(targetHealth);
        }
    }

    private void RotateToFaceDirection()
    {
        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 10f));
        }
    }
}
