using UnityEngine;

namespace RabbleHouse
{
    /// <summary>
    /// What kind of disabling effect an attack applies.
    /// Stun    = ragdoll in place (no launch).
    /// Knockdown = ragdoll AND send the target flying.
    /// None    = pure damage, no disable.
    /// </summary>
    public enum HitType
    {
        None = 0,
        Stun = 1,
        Knockdown = 2
    }

    /// <summary>
    /// Health + damage recipient for any character (player or AI).
    /// Damage is applied by PhysicCharacterController.CheckHit() / GrabbableObject;
    /// this component decides whether the hit stuns / knocks down / dies and notifies
    /// the controller. The attack specifies its HitType explicitly (no random coin-flip).
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private int maxHealth = 100;

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
        /// Apply damage from a hit.
        /// hitType        — what disabling effect this attack causes (Stun / Knockdown / None).
        /// effectChance   — likelihood (0-1) the disabling effect actually triggers.
        ///                  If the roll fails, the target just takes damage (no disable).
        ///                  Defaults to 1 (always applies).
        /// forceDirection — used by Knockdown to launch the target.
        /// </summary>
        public void TakeDamage(int damage, Vector3 forceDirection, HitType hitType, float effectChance = 1f, int attackerIndex = -1)
        {
            if (isDead) return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            OnHealthChanged?.Invoke(PlayerIndex, currentHealth);
            OnTakeDamage?.Invoke(PlayerIndex);

            // Apply the attacker-specified effect (if any), gated by its chance.
            if (hitType != HitType.None && !isStunned && !isKnockedDown)
            {
                if (Random.value < effectChance)
                {
                    if (hitType == HitType.Stun)
                        ApplyStun();
                    else if (hitType == HitType.Knockdown)
                        ApplyKnockdown(forceDirection);
                }
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
            Debug.Log("die");
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
