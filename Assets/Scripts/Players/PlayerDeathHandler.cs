using RabbleHouse;
using UnityEngine;

public class PlayerDeathHandler : MonoBehaviour
{
    private PlayerHealth playerHealth;

    private void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();

        if (playerHealth != null)
        {
            // Subscribe to death events
            playerHealth.OnDeath += OnPlayerDeath;
        }
    }

    private void OnPlayerDeath(int playerIndex)
    {

    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= OnPlayerDeath;
        }
    }
}
