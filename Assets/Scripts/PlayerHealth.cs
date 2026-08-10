using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using System;

namespace RabbleHouse
{
    /// <summary>
    /// Player health, damage, and stun system.
    /// Handles taking damage, being stunned/knocked down, and healing.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int currentHealth;
        [SerializeField] private int stunThreshold = 30;

        [Header("Stun Settings")]
        [SerializeField] private float stunDuration = 1.5f;
        [SerializeField] private float knockdownDuration = 1.2f;
        [SerializeField] private float recoveryTime = 3f;

        [Header("Effects")]
        [SerializeField] private ParticleSystem hitParticles;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private AudioClip stunSound;
        [SerializeField] private AudioClip knockdownSound;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public bool IsStunned => isStunned;
        public bool IsKnockedDown => isKnockedDown;
        public int PlayerIndex { get; set; }

        public Action<int> OnTakeDamage;
        public Action<int> OnStunned;
        public Action<int> OnKnockdown;
        public Action<int> OnRecovery;
        public Action<int> OnDeath;
        public Action<int, int> OnHealthChanged;

        private PlayerController controller;
        private bool isStunned = false;
        private bool isKnockedDown = false;
        private int damageThisRound = 0;

        public void OnEnable()
        {
            controller = GetComponentInParent<PlayerController>();
            if (controller != null)
            {
                PlayerIndex = controller.PlayerIndex;
            }
            currentHealth = maxHealth;
        }

        public void TakeDamage(int damage, Vector3 forceDirection, int attackerIndex = -1)
        {
            if (isKnockedDown || currentHealth <= 0) return;

            damageThisRound += damage;
            currentHealth = Mathf.Max(0, currentHealth - damage);

            OnHealthChanged?.Invoke(PlayerIndex, currentHealth);

            if (hitParticles != null)
            {
                hitParticles.transform.position = transform.position + Vector3.up * 1f;
                hitParticles.Play();
            }

            if (audioSource != null && hitSound != null)
            {
                audioSource.PlayOneShot(hitSound);
            }

            if (damage >= stunThreshold || damageThisRound >= stunThreshold)
            {
                ApplyStunOrKnockdown(forceDirection);
            }

            OnTakeDamage?.Invoke(PlayerIndex);

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private void ApplyStunOrKnockdown(Vector3 forceDirection)
        {
            if (isStunned || isKnockedDown) return;

            if (UnityEngine.Random.value > 0.5f)
            {
                ApplyStun();
            }
            else
            {
                ApplyKnockdown(forceDirection);
            }
        }

        public void ApplyStun() // testing purpose, change to private later
        {
            isStunned = true;
            OnStunned?.Invoke(PlayerIndex);

            if (audioSource != null && stunSound != null)
            {
                audioSource.PlayOneShot(stunSound);
            }

            if (controller != null)
            {
                controller.OnStunned(stunDuration);
            }

            StartCoroutine(StunRoutine());
        }

        private IEnumerator StunRoutine()
        {
            yield return new WaitForSeconds(stunDuration);
            isStunned = false;
        }

        private void ApplyKnockdown(Vector3 forceDirection)
        {
            isKnockedDown = true;
            OnKnockdown?.Invoke(PlayerIndex);

            if (audioSource != null && knockdownSound != null)
            {
                audioSource.PlayOneShot(knockdownSound);
            }

            if (controller != null)
            {
                controller.OnKnockdown(knockdownDuration);
            }

            StartCoroutine(KnockdownRoutine());
        }

        private IEnumerator KnockdownRoutine()
        {
            yield return new WaitForSeconds(knockdownDuration);
            isKnockedDown = false;

            yield return new WaitForSeconds(recoveryTime);

            OnRecovery?.Invoke(PlayerIndex);
            if (controller != null)
            {
                // Re-enable the ragdoll controller (Hip-based if available)
                if (controller.TryGetComponent<HipRagdollController>(out var hipRC))
                {
                    hipRC.SetRagdollMode(false);
                }
                else
                {
                    controller.RagdollController.EnableRagdoll(false);
                }
            }
            currentHealth = maxHealth;
            isStunned = false;
            isKnockedDown = false;
        }

        private void Die()
        {
            OnDeath?.Invoke(PlayerIndex);
        }

        public void Heal(int amount)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(PlayerIndex, currentHealth);
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
            damageThisRound = 0;
            isStunned = false;
            isKnockedDown = false;
            OnHealthChanged?.Invoke(PlayerIndex, currentHealth);
        }

        private void OnDisable()
        {
            // Nothing to clean here - PlayerHealth is not a GrabbableObject
        }
    }
}