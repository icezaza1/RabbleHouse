using UnityEngine;

public class EndScreenUI : MonoBehaviour
{
    private EndScreenManager endScreenManager;

    private void Start()
    {
        // Find SpectatorManager
        endScreenManager = EndScreenManager.Instance;
    }
    public void RestartGame()
    {
        endScreenManager?.RestartGame();
    }

    public void ReturnToMainMenu()
    {
        endScreenManager?.ReturnToMainMenu();
    }
}
