using RabbleHouse;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthbarUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Image characterIcon;
    [SerializeField] private TMP_Text playerNameText;

    [Header("Target Assignment")]
    [SerializeField] private int playerIndex = 0;

    private PlayerHealth targetHealth;
    private void Start()
    {
        FindTarget();
    }

    private void OnEnable()
    {
        // Re-find target when enabled (scene reload, etc.)
        FindTarget();
    }

    private void FindTarget()
    {
        // Unsubscribe from old target
        if (targetHealth != null)
            targetHealth.OnHealthChanged -= OnHealthChanged;

        targetHealth = null;

        // Find all characters in scene
        GameObject[] allChars = GameObject.FindGameObjectsWithTag("Player");
        foreach (var charObj in allChars)
        {
            PlayerHealth health = charObj.GetComponent<PlayerHealth>();
            if (health != null && health.PlayerIndex == playerIndex)
            {
                targetHealth = health;
                break;
            }
        }

        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged += OnHealthChanged;
            UpdateDisplay();
            Debug.Log($"[HealthbarUI] Player {playerIndex} -> {targetHealth.transform.root.name}");
        }
        else
        {
            Debug.LogWarning($"[HealthbarUI] No PlayerHealth with index {playerIndex} found");
            // Don't hide — just show empty bar until target spawns
        }
    }

    private void OnHealthChanged(int senderIndex, int newHealth)
    {
        if (senderIndex == playerIndex)
            UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (targetHealth == null) return;

        float healthPercent = (float)targetHealth.CurrentHealth / targetHealth.MaxHealth;
        healthBarFill.fillAmount = healthPercent;

        // Color coding: green > yellow > red
        Color healthColor = healthPercent > 0.5f ? Color.green :
                            healthPercent > 0.25f ? Color.yellow : Color.red;
        healthBarFill.color = healthColor;

        // Set name based on index
        if (playerNameText != null)
            playerNameText.text = $"Player {playerIndex}";
    }
}
