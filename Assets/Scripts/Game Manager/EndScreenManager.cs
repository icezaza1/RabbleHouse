using RabbleHouse;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndScreenManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TextMeshProUGUI endMessageText;

    [Header("Healthbar Visibility")]
    [SerializeField] private GameObject healthbarContainer; // Parent of all healthbar UI elements
    [SerializeField] private float fadeDuration = 0.5f; // Delay before hiding healthbar
    private CanvasGroup healthbarCanvasGroup;
    private Coroutine currentFadeRoutine;

    [Header("Camera Setup")]
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private float zoomDuration = 2f;
    [SerializeField] private float originFOV = 60f;
    [SerializeField] private float zoomFOV = 30f;

    [Header("Game State")]
    [SerializeField] private string mainMenuSceneName = "Menu";

    // Internal state
    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private List<PlayerHealth> aiCharacters = new List<PlayerHealth>();
    private CameraState currentState = CameraState.Normal;
    private bool isInitialized = false;

    // State management
    public enum CameraState { Normal, ZoomingIn, EndScreen }
    public CameraState CurrentState => currentState;

    // Singleton pattern
    public static EndScreenManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (!isInitialized)
        {
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        // Initialize the game
        InitializeGame();

        // Hide end panel initially
        if (endPanel != null)
        {
            endPanel.SetActive(false);
        }

        // Show healthbar initially
        if (healthbarContainer != null)
        {
            healthbarCanvasGroup = healthbarContainer.GetComponent<CanvasGroup>();
            healthbarContainer.SetActive(true);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitializeGame();

        // Reset UI state
        if (endPanel != null)
            endPanel.SetActive(false);

        if (healthbarContainer != null)
        {
            healthbarCanvasGroup = healthbarContainer.GetComponent<CanvasGroup>();
            healthbarContainer.SetActive(true);
        }

        currentState = CameraState.Normal;
        isInitialized = true;
    }
    private void InitializeGame()
    {
        // Find player character (with PlayerInput component)
        PlayerInput playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerInput != null)
        {
            playerHealth = playerInput.GetComponent<PlayerHealth>();
            playerTransform = FindCoreRigidbody(playerHealth).transform;

            // Subscribe to player death
            if (playerHealth != null)
            {
                playerHealth.OnDeath += OnPlayerDied;
            }
        }
        else
        {
            // Fallback: look for character with player tag
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerHealth = playerObj.GetComponent<PlayerHealth>();
            }
        }

        // Find all AI characters
        AIInputHandler[] allAI = FindObjectsByType<AIInputHandler>();
        foreach (var ai in allAI)
        {
            PlayerHealth aiHealth = ai.GetComponent<PlayerHealth>();
            if (aiHealth != null)
            {
                aiCharacters.Add(aiHealth);

                // Subscribe to AI death
                aiHealth.OnDeath += OnAIDied;
            }
        }

        Debug.Log($"[EndScreenManager] Initialized: Player found = {(playerHealth != null)}, AI count = {aiCharacters.Count}");
    }

    private void OnPlayerDied(int playerIndex)
    {
        StartCoroutine(EndGameSequence(false));
    }

    private void OnAIDied(int playerIndex)
    {

        // Check if all AI are dead
        bool allAIDead = true;
        foreach (var ai in aiCharacters)
        {
            if (ai != null && !ai.IsDead)
            {
                allAIDead = false;
                break;
            }
        }

        if (allAIDead)
        {
            Debug.Log("[EndScreenManager] All AI characters defeated - player wins!");
            StartCoroutine(EndGameSequence(true));
        }
        else
        {
            Debug.Log($"[EndScreenManager] AI characters remaining: {CountAliveAI()}");
        }
    }

    private IEnumerator EndGameSequence(bool playerWon)
    {
        currentState = CameraState.ZoomingIn;

        // Hide healthbar UI
        if (healthbarContainer != null)
        {
            StartHealthUIFade(0f, true);
        }

        // Quickly freeze the game
        StartCoroutine(RestoreTimeScale(1f, 0f, 0.4f));
        // Zoom camera to player
        if (virtualCamera != null && playerTransform != null)
        {
            yield return StartCoroutine(ZoomCameraToPlayer(originFOV, zoomFOV, zoomDuration));
        }

        // Brief freeze
        yield return new WaitForSecondsRealtime(0.5f);

        // Gradually restore game speed
        StartCoroutine(RestoreTimeScale(0f, 1f, 3f));
        yield return StartCoroutine(ZoomCameraToPlayer(zoomFOV, 45f, 3f));

        // 3. Show end panel
        currentState = CameraState.EndScreen;
        ShowEndPanel(playerWon);
    }

    private void StartHealthUIFade(float targetAlpha, bool shouldBlockInteractions)
    {
        // Stop the previous fade coroutine if it is still running
        if (currentFadeRoutine != null)
        {
            StopCoroutine(currentFadeRoutine);
        }

        // Start the new fade coroutine
        currentFadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, shouldBlockInteractions));
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool shouldBlockInteractions)
    {
        float startAlpha = healthbarCanvasGroup.alpha;
        float elapsedTime = 0f; //

        // Handle interaction settings immediately during transitions
        if (!shouldBlockInteractions)
        {
            healthbarCanvasGroup.interactable = false;
            healthbarCanvasGroup.blocksRaycasts = false;
        }

        while (elapsedTime < fadeDuration) //
        {
            elapsedTime += Time.unscaledDeltaTime; // Works with normal game time
            // Use Time.unscaledDeltaTime instead if your game pauses using Time.timeScale = 0

            // Calculate progress percentage (0 to 1)
            float normalizedTime = Mathf.Clamp01(elapsedTime / fadeDuration);

            // Linearly interpolate the alpha value
            healthbarCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, normalizedTime);

            yield return null; // Wait for the next frame before looping again
        }

        // Snaps the alpha directly to target to ensure strict accuracy at completion
        healthbarCanvasGroup.alpha = targetAlpha;

        // Apply final interaction settings based on target visibility
        healthbarCanvasGroup.interactable = shouldBlockInteractions;
        healthbarCanvasGroup.blocksRaycasts = shouldBlockInteractions;

        healthbarContainer.SetActive(false);
        currentFadeRoutine = null;
    }

    private IEnumerator RestoreTimeScale(float originalScale, float targetScale, float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsedTime / duration);

            Time.timeScale = Mathf.Lerp(originalScale, targetScale, t);

            yield return null;
        }

        Time.timeScale = targetScale;
    }
    private IEnumerator ZoomCameraToPlayer(float originFOV, float finalFOV, float zoomTime)
    {
        if (virtualCamera == null || playerTransform == null)
            yield break;

        float elapsedTime = 0f;

        while (elapsedTime < zoomTime)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsedTime / zoomTime);

            // Smoothstep
            t = t * t * (3f - 2f * t);

            virtualCamera.Lens.FieldOfView =
                Mathf.Lerp(originFOV, finalFOV, t);

            virtualCamera.transform.LookAt(playerTransform);

            yield return null;
        }

        virtualCamera.Lens.FieldOfView = finalFOV;
        virtualCamera.transform.LookAt(playerTransform);
    }

    private void ShowEndPanel(bool playerWon)
    {
        if (endPanel == null) return;

        endPanel.SetActive(true);

        // Update end message
        if (endMessageText != null)
        {
            endMessageText.text = playerWon ? "VICTORY!" : "DEFEAT";
            endMessageText.color = playerWon ? Color.green : Color.red;
        }
    }

    private int CountAliveAI()
    {
        int count = 0;
        foreach (var ai in aiCharacters)
        {
            if (ai != null && !ai.IsDead)
            {
                count++;
            }
        }
        return count;
    }

    private Rigidbody FindCoreRigidbody(PlayerHealth target)
    {
        Rigidbody[] bodies = target.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in bodies)
            if (rb.transform.name.Contains("Hips"))
                return rb;
        return bodies.Length > 0 ? bodies[0] : null;
    }

    // Button handlers
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // Cleanup event subscriptions
        if (playerHealth != null)
            playerHealth.OnDeath -= OnPlayerDied;

        foreach (var ai in aiCharacters)
        {
            if (ai != null)
                ai.OnDeath -= OnAIDied;
        }
    }
}
