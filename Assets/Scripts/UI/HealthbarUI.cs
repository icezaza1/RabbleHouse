using RabbleHouse;
using UnityEngine;
using UnityEngine.UI;

public class HealthbarUI : MonoBehaviour
{
    [SerializeField] private Image healthBar;
    [SerializeField] private PhysicCharacterController characterController;
    private PlayerHealth playerHealth;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private void Awake()
    {
        playerHealth = characterController.GetComponent<PlayerHealth>();
    }

    // Update is called once per frame
    void Update()
    {
        healthBar.fillAmount = playerHealth.CurrentHealth / 100f;
    }
}
