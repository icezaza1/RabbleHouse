using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LobbyUI : MonoBehaviour
{
    [Header("Stage Selection")]
    [SerializeField] private TextMeshProUGUI stageNameText;
    [SerializeField] private Button stageLeftArrow;
    [SerializeField] private Button stageRightArrow;

    [Header("AI Difficulty")]
    [SerializeField] private TextMeshProUGUI difficultyText;
    [SerializeField] private Button difficultyLeftArrow;
    [SerializeField] private Button difficultyRightArrow;

    [Header("Confirm")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private string gameSceneName = "SampleScene";

    private string[] stageNames = { "Living Room", "Kitchen" };
    private string[] difficultyNames = { "Easy", "Normal", "Hard", "Expert" };

    private int currentStageIndex = 0;
    private int currentDifficultyIndex = 1; // default Normal

    private void Start()
    {
        // Subscribe to button clicks
        stageLeftArrow.onClick.AddListener(OnStageLeft);
        stageRightArrow.onClick.AddListener(OnStageRight);
        difficultyLeftArrow.onClick.AddListener(OnDifficultyLeft);
        difficultyRightArrow.onClick.AddListener(OnDifficultyRight);
        confirmButton.onClick.AddListener(OnConfirm);

        // Initialize display
        UpdateStageDisplay();
        UpdateDifficultyDisplay();
    }

    private void OnStageLeft()
    {
        currentStageIndex = (currentStageIndex - 1 + stageNames.Length) % stageNames.Length;
        UpdateStageDisplay();
    }

    private void OnStageRight()
    {
        currentStageIndex = (currentStageIndex + 1) % stageNames.Length;
        UpdateStageDisplay();
    }

    private void OnDifficultyLeft()
    {
        currentDifficultyIndex = (currentDifficultyIndex - 1 + difficultyNames.Length) % difficultyNames.Length;
        UpdateDifficultyDisplay();
    }

    private void OnDifficultyRight()
    {
        currentDifficultyIndex = (currentDifficultyIndex + 1) % difficultyNames.Length;
        UpdateDifficultyDisplay();
    }

    private void UpdateStageDisplay()
    {
        stageNameText.text = stageNames[currentStageIndex];
    }

    private void UpdateDifficultyDisplay()
    {
        difficultyText.text = difficultyNames[currentDifficultyIndex];
    }

    private void OnConfirm()
    {
        // Store selections in static class
        LobbyData.SelectedStageIndex = currentStageIndex;
        LobbyData.SelectedDifficulty = currentDifficultyIndex;

        Debug.Log($"[Lobby] Starting game: Stage={stageNames[currentStageIndex]}, Difficulty={difficultyNames[currentDifficultyIndex]}");

        // Load the game scene
        SceneManager.LoadScene(gameSceneName);
    }
}
