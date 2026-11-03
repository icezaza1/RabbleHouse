using RabbleHouse;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class WaterPuddle : MonoBehaviour
{
    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastHeight = 2f;
    [SerializeField] private float raycastDistance = 10f;

    [SerializeField] private float lifetime = 10f;
    [SerializeField] private AudioClip tripSound;
    [SerializeField] private AudioMixerGroup tripSfxGroup;
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

        // Play the trip sound only when this puddle actually causes a new stun.
        bool wasStunned = targetHealth.IsStunned;
        targetHealth.TakeDamage(0, Vector3.zero, HitType.Stun, 0.15f, playSound: false);
        if (!wasStunned && targetHealth.IsStunned && tripSound != null &&
            tripSfxGroup != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySound(tripSound, targetHealth.transform, 1f, tripSfxGroup);
        }
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
        float duration = 3f;
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
