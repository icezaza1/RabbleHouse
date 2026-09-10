using RabbleHouse;
using UnityEngine;

public class PlayerDeathHandler : MonoBehaviour
{
    private PlayerHealth playerHealth;
    private SpectatorManager spectatorManager;

    private void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();
        spectatorManager = SpectatorManager.Instance;

        if (playerHealth != null)
        {
            // Subscribe to death events
            playerHealth.OnDeath += OnPlayerDeath;
        }
    }

    private void OnPlayerDeath(int playerIndex)
    {
        Debug.Log($"[PlayerDeathHandler] Player {playerIndex} died - notifying spectator system");

        if (spectatorManager != null)
        {
            // Notify spectator manager about the death
            // The spectator manager will automatically switch to another target
            spectatorManager.ForceSpectateCharacter(playerIndex);
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= OnPlayerDeath;
        }
    }
}
