using RabbleHouse;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpectatorUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject spectatorPanel;

    private SpectatorManager spectatorManager;

    private void Start()
    {
        // Find SpectatorManager
        spectatorManager = SpectatorManager.Instance;
        if (spectatorManager == null)
        {
            Debug.LogError("[SpectatorUIController] SpectatorManager not found!");
            return;
        }
        
        // Setup UI references
        if (spectatorPanel != null)
        {
            spectatorPanel.SetActive(false);
        }
    }
    
    // Public methods for button interactions
    public void PreviousTarget()
    {
        spectatorManager?.PreviousCharacter();
    }
    
    public void NextTarget()
    {
        spectatorManager?.NextCharacter();
    }
}
