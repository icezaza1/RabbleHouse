using UnityEngine;

namespace RabbleHouse
{
    /// <summary>
    /// Minimal health component for any damageable entity (players, breakables, etc.)
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private int maxHealth = 100;
        private int currentHealth;
        private bool isStunned = false;
        private bool isKnockedDown = false;

        // References
        private PhysicCharacterController controller;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public bool IsStunned => isStunned;
        public bool IsKnockedDown => isKnockedDown;
        public int PlayerIndex { get; set; } = 0;

        public System.Action<int> OnTakeDamage;
        public System.Action<int> OnStunned;
        public System.Action<int> OnKnockdown;
        public System.Action<int> OnRecovery;
        public System.Action<int, int> OnHealthChanged;

        private void Awake()
        {
            controller = GetComponent<PhysicCharacterController>();
            currentHealth = maxHealth;
        }

        public void TakeDamage(int damage, Vector3 forceDirection, int attackerIndex = -1)
        {
            if (isKnockedDown || currentHealth <= 0) return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            OnHealthChanged?.Invoke(PlayerIndex, currentHealth);

            if (damage >= 30) // stunThreshold
            {
                if (Random.value > 0.5f)
                    ApplyStun();
                else
                    ApplyKnockdown(forceDirection);
            }

            OnTakeDamage?.Invoke(PlayerIndex);

            if (currentHealth <= 0)
                Die();
        }

        private void ApplyStun()
        {
            isStunned = true;
            OnStunned?.Invoke(PlayerIndex);
            if (controller != null)
                controller.OnStunned(2f);
        }

        private void ApplyKnockdown(Vector3 forceDirection)
        {
            isKnockedDown = true;
            OnKnockdown?.Invoke(PlayerIndex);
            if (controller != null)
                controller.OnKnockdown(1.2f);
        }

        private void Die()
        {
            OnKnockdown?.Invoke(PlayerIndex);
            if (controller != null)
                controller.OnKnockdown(3f);
        }

        public void Heal(int amount)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(PlayerIndex, currentHealth);
        }
    }
}