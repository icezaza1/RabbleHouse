using RabbleHouse;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class StageSetup : MonoBehaviour
{
    [Header("Characters")]
    [SerializeField] private CharacterData[] characters;
    [SerializeField] private CinemachineCamera camera;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Healthbars")]
    [SerializeField] private HealthbarUI[] healthbars;

    [Header("Debug / Testing")]
    [SerializeField] private CharacterData defaultCharacter;

    private void Awake()
    {
        SpawnCharacters();
    }

    private void Start()
    {
        ApplyAIDifficulty();
    }

    private void SpawnCharacters()
    {
        CharacterData selectedCharacter = LobbyData.SelectedCharacter;

        if (selectedCharacter == null)
        {
            selectedCharacter = defaultCharacter;

            Debug.Log($"[StageSetup] No character selected. Using default: " + $"{selectedCharacter.characterName}");
        }

        if (spawnPoints.Length < 3)
        {
            Debug.LogError("[StageSetup] Need 3 spawn points!");
            return;
        }

        // Player
        SpawnPlayer(selectedCharacter, 0, spawnPoints[0]);

        // Spawn the remaining characters as AI
        int playerIndex = 1;

        foreach (CharacterData character in characters)
        {
            if (character == selectedCharacter)
                continue;

            SpawnAI(
                character,
                playerIndex,
                spawnPoints[playerIndex]
            );

            playerIndex++;
        }
    }

    private void SetCameraTarget(PlayerHealth target)
    {
        Rigidbody coreRigidbody = FindCoreRigidbody(target);
        // Update camera target
        if (camera != null)
        {
            camera.Follow = coreRigidbody.transform;
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

    private void SpawnPlayer(CharacterData character, int playerIndex, Transform spawnPoint)
    {
        GameObject player = Instantiate(character.playerPrefab, spawnPoint.position, spawnPoint.rotation);

        PlayerHealth health = player.GetComponentInChildren<PlayerHealth>();

        if (health != null)
        {
            // Set Camera to player
            SetCameraTarget(health);

            health.PlayerIndex = playerIndex;
            healthbars[playerIndex].SetCharacterData(character);
            healthbars[playerIndex].SetTarget(health);
        }
    }

    private void SpawnAI(CharacterData character, int playerIndex, Transform spawnPoint)
    {
        GameObject ai = Instantiate(
            character.aiPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        PlayerHealth health = ai.GetComponentInChildren<PlayerHealth>();

        if (health != null)
        {
            health.PlayerIndex = playerIndex;
            healthbars[playerIndex].SetCharacterData(character);
            healthbars[playerIndex].SetTarget(health);
        }
    }

    private void ApplyAIDifficulty()
    {
        int difficulty = LobbyData.SelectedDifficulty;

        Debug.Log($"[StageSetup] Applying difficulty: {difficulty}");

        AIInputHandler[] allAI =
            FindObjectsByType<AIInputHandler>();

        foreach (AIInputHandler ai in allAI)
        {
            ai.SetDifficulty(difficulty);
        }
    }
}