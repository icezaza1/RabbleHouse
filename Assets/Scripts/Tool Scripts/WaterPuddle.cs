using RabbleHouse;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class WaterPuddle : MonoBehaviour
{
    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastHeight = 2f;
    [SerializeField] private float raycastDistance = 10f;

    [SerializeField] private float lifetime = 10f;
    private readonly HashSet<PlayerHealth> characters = new HashSet<PlayerHealth>();

    private void Start()
    {
        StickToGround();
        StartCoroutine(ScalePuddle(Vector3.zero, transform.localScale));
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerHealth targetHealth =
            other.GetComponentInParent<PlayerHealth>();

        if (targetHealth == null)
            return;

        if (characters.Contains(targetHealth))
            return;

        characters.Add(targetHealth);

        // Trip character
        targetHealth.TakeDamage(0, Vector3.zero, HitType.Stun, 0.3f);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerHealth targetHealth =
            other.GetComponentInParent<PlayerHealth>();

        if (targetHealth == null)
            return;

        characters.Remove(targetHealth);
    }

    private void StickToGround()
    {
        transform.rotation = Quaternion.identity;
        Vector3 rayOrigin = transform.position + Vector3.up * raycastHeight;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            raycastDistance,
            groundLayer))
        {
            transform.position = hit.point;
        }
        else
        {
            Debug.LogWarning(
                $"[WaterPuddle] Could not find ground below {gameObject.name}"
            );
        }
    }

    private IEnumerator ScalePuddle(Vector3 initialScale, Vector3 targetScale)
    {
        float duration = 1f;
        float currentTime = 0f;

        while (currentTime < duration)
        {
            currentTime += Time.deltaTime;

            // Calculate progress ratio (0 to 1)
            float progress = currentTime / duration;

            // Linearly interpolate between the starting scale and the target scale
            transform.localScale = Vector3.Lerp(initialScale, targetScale, progress);

            // Wait until the next frame before continuing the loop
            yield return null;
        }

        // Ensure it snaps perfectly to the target scale at the end
        transform.localScale = targetScale;
    }
}
