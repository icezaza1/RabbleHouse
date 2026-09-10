using RabbleHouse;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;

public class SpectatorManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject spectatorPanel;
    [SerializeField] private TextMeshProUGUI spectatingText;  // UI Text component

    [Header("Camera Setup")]
    [SerializeField] private CinemachineCamera virtualCamera;

    // Internal state
    private List<PlayerHealth> aliveCharacters = new List<PlayerHealth>();
    private PlayerHealth playerCharacter; // Reference to player character
    private int currentTargetIndex = 0;

    private PlayerHealth previousTarget;

    // State management
    public enum CameraState { FollowingPlayer, Spectating }
    public CameraState CurrentState { get; private set; } = CameraState.FollowingPlayer;

    // Singleton pattern
    public static SpectatorManager Instance { get; private set; }

    private void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Find the player character first
        FindPlayerCharacter();
        // Find all player characters
        FindAllCharacters();

        DeactivateSpectatorMode();
    }

    private void Update()
    {
        UpdateUI();
    }

    private void FindPlayerCharacter()
    {
        // Find character with PlayerInput component (human player)
        PlayerInput playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerInput != null)
        {
            playerCharacter = playerInput.GetComponent<PlayerHealth>();
            if (playerCharacter == null)
            {
                Debug.LogError("[SpectatorManager] PlayerInput found but no PlayerHealth component");
            }
        }
        else
        {
            // Fallback: look for character with player tag
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerCharacter = playerObj.GetComponent<PlayerHealth>();
            }
        }

        if (playerCharacter == null)
        {
            Debug.LogWarning("[SpectatorManager] Could not find player character");
        }
        else
        {
            Debug.Log($"[SpectatorManager] Found player character: {playerCharacter.gameObject.name}");
        }
    }
    private void FindAllCharacters()
    {
        // Find all characters with PlayerHealth component
        PlayerHealth[] allCharacters = FindObjectsByType<PlayerHealth>();

        foreach (var character in allCharacters)
        {
            AddCharacterToTracking(character);
        }
    }

    private void AddCharacterToTracking(PlayerHealth character)
    {
        if (!aliveCharacters.Contains(character))
        {
            aliveCharacters.Add(character);

            // Subscribe to death events
            character.OnDeath += OnCharacterDied;
        }
    }

    private void RemoveCharacterFromTracking(PlayerHealth character)
    {
        if (aliveCharacters.Contains(character))
        {
            aliveCharacters.Remove(character);

            // Unsubscribe from events
            character.OnDeath -= OnCharacterDied;
        }
    }

    private void OnCharacterDied(int playerIndex)
    {
        // Find the character that died
        PlayerHealth deadCharacter = aliveCharacters.Find(c => c.PlayerIndex == playerIndex);
        if (deadCharacter != null)
        {
            RemoveCharacterFromTracking(deadCharacter);

            // Check if this was the player character
            if (deadCharacter == playerCharacter)
            {
                ActivateSpectatorMode();
            }
        }

        // Update current target if needed
        UpdateCurrentTarget();

        // Check if we need to switch targets
        CheckForTargetSwitch();
    }

    private void UpdateCurrentTarget()
    {
        if (CurrentState == CameraState.FollowingPlayer && playerCharacter != null)
        {
            // In following mode, target is always the player
            SetTarget(playerCharacter);
        }
        else if (CurrentState == CameraState.Spectating && aliveCharacters.Count > 0)
        {
            // In spectator mode, target is whatever we have selected
            // (currentTargetIndex should already be valid)
            SetTarget(aliveCharacters[currentTargetIndex]);
        }
    }

    private void CheckForTargetSwitch()
    {
        // If in spectator mode and current target is dead, switch to next
        PlayerHealth currentTarget = GetCurrentTarget();
        if (currentTarget != null && currentTarget.IsDead)
        {
            SwitchToNextAvailable();
        }
    }
    private void ActivateSpectatorMode()
    {
        CurrentState = CameraState.Spectating;

        if (spectatorPanel != null)
        {
            spectatorPanel.SetActive(true);

            // Set initial target to a random alive character (not the dead player)
            if (aliveCharacters.Count > 0)
            {
                // Find a character that's not the dead player
                PlayerHealth firstAlive = aliveCharacters[0];
                if (firstAlive != playerCharacter)
                {
                    SetTarget(firstAlive);
                }
                else if (aliveCharacters.Count > 1)
                {
                    // If first alive is the player (who is dead), pick the next one
                    SetTarget(aliveCharacters[1]);
                }
                else
                {
                    // Only one character left and it's the dead player
                    SetTarget(firstAlive);
                }
            }
        }
    }

    private void DeactivateSpectatorMode()
    {
        CurrentState = CameraState.FollowingPlayer;

        if (spectatorPanel != null)
        {
            spectatorPanel.SetActive(false);
        }

        // Return to following the player if they're alive
        if (playerCharacter != null && !playerCharacter.IsDead)
        {
            SetTarget(playerCharacter);
        }
        else if (aliveCharacters.Count > 0)
        {
            // Player is dead but others are alive - stay in spectator mode
            // (but we shouldn't get here if player is dead)
        }
    }

    private void SetTarget(PlayerHealth target)
    {
        previousTarget = GetCurrentTarget();
        Rigidbody coreRigidbody = FindCoreRigidbody(target);
        // Update camera target
        if (virtualCamera != null)
        {
            virtualCamera.Follow = coreRigidbody.transform;
        }

        // Update currentTargetIndex based on the target
        if (aliveCharacters.Contains(target))
        {
            currentTargetIndex = aliveCharacters.IndexOf(target);
        }
    }

    private Rigidbody FindCoreRigidbody(PlayerHealth target)
    {
        Rigidbody[] bodies = target.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in bodies)
            if (rb.transform.name.Contains("Hips"))
                return rb;
        return bodies.Length > 0 ? bodies[0] : null;
    }

    private PlayerHealth GetCurrentTarget()
    {
        if (CurrentState == CameraState.FollowingPlayer && playerCharacter != null)
        {
            return playerCharacter;
        }
        else if (CurrentState == CameraState.Spectating && currentTargetIndex >= 0 && currentTargetIndex < aliveCharacters.Count)
        {
            return aliveCharacters[currentTargetIndex];
        }
        return null;
    }

    public void PreviousCharacter()
    {
        if (CurrentState != CameraState.Spectating || aliveCharacters.Count <= 1) return;

        currentTargetIndex = (currentTargetIndex - 1 + aliveCharacters.Count) % aliveCharacters.Count;
        SetTarget(aliveCharacters[currentTargetIndex]);
    }

    public void NextCharacter()
    {
        if (CurrentState != CameraState.Spectating || aliveCharacters.Count <= 1) return;

        currentTargetIndex = (currentTargetIndex + 1) % aliveCharacters.Count;
        SetTarget(aliveCharacters[currentTargetIndex]);
    }

    private void SwitchToNextAvailable()
    {
        if (aliveCharacters.Count == 0)
            return;

        // Calculate next target index
        currentTargetIndex = (currentTargetIndex + 1) % aliveCharacters.Count;
        SetTarget(aliveCharacters[currentTargetIndex]);
    }

    private void UpdateUI()
    {
        if (spectatorPanel != null)
        {
            bool shouldShow = CurrentState == CameraState.Spectating;
            spectatorPanel.SetActive(shouldShow);

            if (shouldShow)
            {
                UpdateSpectatorText();
            }
        }
    }

    private void UpdateSpectatorText()
    {
        if (spectatingText != null)
        {
            PlayerHealth currentTarget = GetCurrentTarget();
            if (currentTarget != null)
            {
                spectatingText.text = $"Spectating: {currentTarget.gameObject.name} (Index: {currentTarget.PlayerIndex})";
            }
        }
    }

    // Public methods for external control
    public void ForceSpectateCharacter(int playerIndex)
    {
        if (CurrentState == CameraState.Spectating)
        {
            PlayerHealth target = aliveCharacters.Find(c => c.PlayerIndex == playerIndex);
            if (target != null)
            {
                SetTarget(target);
            }
        }
    }

    public List<PlayerHealth> GetAliveCharacters()
    {
        return new List<PlayerHealth>(aliveCharacters);
    }

    public PlayerHealth GetPlayerCharacter()
    {
        return playerCharacter;
    }

    public PlayerHealth GetCurrentTargetCharacter()
    {
        return GetCurrentTarget();
    }

    private void OnDestroy()
    {
        // Cleanup event subscriptions
        foreach (var character in aliveCharacters)
        {
            if (character != null)
            {
                character.OnDeath -= OnCharacterDied;
            }
        }
    }
}