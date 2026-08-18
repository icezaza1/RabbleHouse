using UnityEngine;

namespace RabbleHouse
{
    /// <summary>
    /// Health + damage recipient for any character (player or AI).
    /// Damage is applied by PhysicCharacterController.CheckHit(); this component
    /// decides whether the hit stuns/knocks down/dies and notifies the controller.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private int maxHealth = 100;

        [Header("Stun / Knockdown Chances (0-1)")]
        [Tooltip("Chance a hit that clears the stun damage threshold causes a stun (vs knockdown).")]
        [SerializeField] private float stunChance = 0.5f;
        [Tooltip("Damage at or above this value can stun/knockdown the target.")]
        [SerializeField] private int stunThreshold = 20;

        private int currentHealth;
        private bool isStunned = false;
        private bool isKnockedDown = false;
        private bool isDead = false;

        // References
        private PhysicCharacterController controller;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public bool IsStunned => isStunned;
        public bool IsKnockedDown => isKnockedDown;
        public bool IsDead => isDead;
        public int PlayerIndex { get; set; } = 0;

        // Callbacks for UI / AI awareness
        public System.Action<int> OnTakeDamage;
        public System.Action<int> OnStunned;
        public System.Action<int> OnKnockdown;
        public System.Action<int> OnDeath;
        public System.Action<int, int> OnHealthChanged;

        private void Awake()
        {
            controller = GetComponent<PhysicCharacterController>();
            currentHealth = maxHealth;
        }

        /// <summary>
        /// Apply damage from a hit. forceDirection is used by knockdown to send the
        /// target flying. stunChanceOverride lets the attacker bias the outcome
        /// (e.g. a heavy punch or a swung object has a higher stun chance).
        /// </summary>
        public void TakeDamage(int damage, Vector3 forceDirection, float stunChanceOverride = -1f, int attackerIndex = -1)
        {
            if (isDead) return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            OnHealthChanged?.Invoke(PlayerIndex, currentHealth);
            OnTakeDamage?.Invoke(PlayerIndex);

            // Stun / knockdown only if damage is meaningful
            if (damage >= stunThreshold && !isStunned && !isKnockedDown)
            {
                float chance = stunChanceOverride >= 0f ? stunChanceOverride : stunChance;
                if (Random.value < chance)
                    ApplyStun();
                else
                    ApplyKnockdown(forceDirection);
            }

            if (currentHealth <= 0)
                Die();
        }

        private void ApplyStun()
        {
            isStunned = true;
            OnStunned?.Invoke(PlayerIndex);
            if (controller != null)
                controller.OnStunned(controller.StunDuration);
        }

        private void ApplyKnockdown(Vector3 forceDirection)
        {
            isKnockedDown = true;
            OnKnockdown?.Invoke(PlayerIndex);
            if (controller != null)
                controller.OnKnockdown(controller.KnockdownDuration);

            // Send the body flying
            if (controller != null && forceDirection != Vector3.zero)
                controller.ApplyKnockback(forceDirection);
        }

        private void Die()
        {
            isDead = true;
            OnDeath?.Invoke(PlayerIndex);
            if (controller != null)
                controller.OnDead();
        }

        /// <summary>Called by the controller when stun/knockdown finishes.</summary>
        public void NotifyRecovered()
        {
            isStunned = false;
            isKnockedDown = false;
        }

        public void Heal(int amount)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(PlayerIndex, currentHealth);
        }
    }
}
