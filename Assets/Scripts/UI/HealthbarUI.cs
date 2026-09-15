using RabbleHouse;
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

    public void SetTarget(PlayerHealth health)
    {
        if (targetHealth != null)
            targetHealth.OnHealthChanged -= OnHealthChanged;

        targetHealth = health;

        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged += OnHealthChanged;
            UpdateDisplay();
        }
    }

    private void OnHealthChanged(int senderIndex, int newHealth)
    {
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

    public void SetCharacterData(CharacterData characterData)
    {
        if (characterIcon == null)
            return;

        if (characterData == null)
        {
            characterIcon.enabled = false;
            return;
        }

        characterIcon.sprite = characterData.portrait;
        characterIcon.enabled = characterData.portrait != null;

        if (playerNameText != null)
            playerNameText.text = characterData.characterName;
    }
}
