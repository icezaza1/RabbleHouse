using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace RabbleHouse
{
    /// <summary>
    /// Matches the player rig and keeps track of player states.
    /// Manages round flow: spawn, fight, countdown, victory.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum GameState
        {
            Menu,
            Countdown,
            Playing,
            RoundEnd,
            MatchEnd
        }

        [Header("Game Settings")]
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private int scoreToWin = 5;
        [SerializeField] private float countdownTime = 3f;
        [SerializeField] private float roundEndDelay = 2f;
        [SerializeField] private float matchEndDelay = 3f;

        [Header("Player Spawns")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private GameObject playerPrefab;

        [Header("References")]
        [SerializeField] private Camera mainCamera;

        private GameState currentState = GameState.Menu;
        private List<PlayerController> players = new List<PlayerController>();
        private Dictionary<int, int> playerScores = new Dictionary<int, int>();
        private List<int> activePlayerIndices = new List<int>();
        private float stateTimer;

        public static GameManager Instance { get; private set; }

        public GameState CurrentState => currentState;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Spawn 1 human player + 2 AI opponents
            if (playerPrefab != null && spawnPoints != null && spawnPoints.Length > 0)
            {
                var spawnPos = spawnPoints[0].position;
                var spawnRot = spawnPoints[0].rotation;

                // Human player (index 0)
                var human = Instantiate(playerPrefab, spawnPos, spawnRot);
                human.name = "Player_0";
                if (human.GetComponent<PlayerController>() != null)
                {
                    human.GetComponent<PlayerController>().PlayerIndex = 0;
                }

                // AI 1 (index 1)
                var ai1 = Instantiate(playerPrefab, spawnPos + Vector3.right * 2, spawnRot);
                ai1.name = "AI_1";
                var ai1Ctrl = ai1.GetComponent<PlayerController>();
                if (ai1Ctrl != null)
                {
                    ai1Ctrl.PlayerIndex = 1;
                    // Mark as AI by adding AIController (PlayerController will auto-detect it)
                    ai1.AddComponent<AIController>();
                    // Disable PlayerInput so the AI never claims the keyboard/gamepad
                    var ai1Input = ai1.GetComponent<PlayerInput>();
                    if (ai1Input != null) ai1Input.enabled = false;
                }

                // AI 2 (index 2)
                var ai2 = Instantiate(playerPrefab, spawnPos - Vector3.right * 2, spawnRot);
                ai2.name = "AI_2";
                var ai2Ctrl = ai2.GetComponent<PlayerController>();
                if (ai2Ctrl != null)
                {
                    ai2Ctrl.PlayerIndex = 2;
                    ai2.AddComponent<AIController>();
                    // Disable PlayerInput so the AI never claims the keyboard/gamepad
                    var ai2Input = ai2.GetComponent<PlayerInput>();
                    if (ai2Input != null) ai2Input.enabled = false;
                }
            }

            // Register all combatants for round-flow / elimination tracking
            RegisterCombatants();

            // Start countdown
            StartGame();
        }

        /// <summary>
        /// Registers the spawned human player and the 2 AI opponents so the
        /// game loop can detect eliminations and end rounds.
        /// </summary>
        private void RegisterCombatants()
        {
            foreach (var pc in FindObjectsByType<PlayerController>())
            {
                var health = pc.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    // Score the point when a combatant is eliminated
                    health.OnDeath += OnPlayerEliminated;
                }

                if (pc.GetComponent<AIController>() != null)
                {
                    RegisterPlayerAI(pc.PlayerIndex);
                }
                else
                {
                    RegisterPlayer(pc);
                }
            }
        }

        private void Update()
        {
            switch (currentState)
            {
                case GameState.Countdown:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0)
                    {
                        SetState(GameState.Playing);
                    }
                    break;

                case GameState.Playing:
                    // Detect round end: only one combatant still alive
                    if (GetAliveCombatantCount() <= 1)
                    {
                        EndRound();
                    }
                    break;

                case GameState.RoundEnd:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0)
                    {
                        if (CheckMatchEnd())
                        {
                            SetState(GameState.MatchEnd);
                        }
                        else
                        {
                            StartNextRound();
                        }
                    }
                    break;

                case GameState.MatchEnd:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0)
                    {
                        SetState(GameState.Countdown);
                        StartNextRound();
                    }
                    break;
            }
        }

        public void RegisterPlayer(PlayerController player)
        {
            if (!players.Contains(player))
            {
                players.Add(player);
                activePlayerIndices.Add(player.PlayerIndex);
                playerScores[player.PlayerIndex] = 0;
            }
        }

        public void RegisterPlayerAI(int playerIndex)
        {
            if (!activePlayerIndices.Contains(playerIndex))
            {
                activePlayerIndices.Add(playerIndex);
                playerScores[playerIndex] = 0;
            }
        }

        public void UnregisterPlayerAI(int playerIndex)
        {
            players.RemoveAll(p => p.PlayerIndex == playerIndex);
            activePlayerIndices.Remove(playerIndex);
        }

        public void UnregisterPlayer(PlayerController player)
        {
            players.Remove(player);
            activePlayerIndices.Remove(player.PlayerIndex);
        }

        public void StartGame()
        {
            SetState(GameState.Countdown);
            stateTimer = countdownTime;
        }

        /// <summary>
        /// Counts how many combatants (players + AI) are still alive this round.
        /// </summary>
        private int GetAliveCombatantCount()
        {
            int alive = 0;
            foreach (var pc in FindObjectsByType<PlayerController>())
            {
                var health = pc.GetComponent<PlayerHealth>();
                if (health != null && health.CurrentHealth > 0)
                {
                    alive++;
                }
            }
            return alive;
        }

        /// <summary>
        /// Ends the current round: awards the survivor a score point and pauses.
        /// </summary>
        private void EndRound()
        {
            if (currentState != GameState.Playing) return;

            // Award the last-standing combatant a point
            foreach (var pc in FindObjectsByType<PlayerController>())
            {
                var health = pc.GetComponent<PlayerHealth>();
                if (health != null && health.CurrentHealth > 0)
                {
                    OnPlayerRoundWin(pc.PlayerIndex);
                    break;
                }
            }

            SetState(GameState.RoundEnd);
            stateTimer = roundEndDelay;
        }

        private void StartNextRound()
        {
            // Reset health + state on ALL combatants (players and AI)
            foreach (var pc in FindObjectsByType<PlayerController>())
            {
                var health = pc.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    health.ResetHealth();
                }

                var controller = pc.GetComponent<PlayerController>();
                if (controller != null)
                {
                    controller.SetState(PlayerController.PlayerState.Idle);
                    controller.RagdollController.EnableRagdoll(false);
                }

                // Move to spawn point (index cycles through spawnPoints)
                if (spawnPoints.Length > 0)
                {
                    int spawnIndex = pc.PlayerIndex % spawnPoints.Length;
                    pc.transform.position = spawnPoints[spawnIndex].position;
                    pc.transform.rotation = spawnPoints[spawnIndex].rotation;
                }
            }

            SetState(GameState.Countdown);
            stateTimer = countdownTime;
        }

        private void SetState(GameState newState)
        {
            currentState = newState;
            Debug.Log($"[RabbleHouse] Game state: {newState}");
        }

        private bool CheckMatchEnd()
        {
            foreach (var kvp in playerScores)
            {
                if (kvp.Value >= scoreToWin)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnPlayerEliminated(int playerIndex)
        {
            // The eliminated combatant should NOT get a point.
            // The survivor gets it (handled by EndRound's last-standing check).
            Debug.Log($"[RabbleHouse] Player {playerIndex} eliminated. Survivor gets the point on round end.");
        }

        private void OnPlayerRoundWin(int playerIndex)
        {
            if (playerScores.ContainsKey(playerIndex))
            {
                playerScores[playerIndex]++;
            }
        }

        private void SpawnPlayer()
        {
            // Deprecated: spawning is now handled in Start() so that all
            // combatants (human + AI) are created and registered together.
            if (spawnPoints.Length > 0 && playerPrefab != null)
            {
                var player = Instantiate(playerPrefab, spawnPoints[0].position, spawnPoints[0].rotation);
                player.name = "Player_0";
                if (player.GetComponent<PlayerController>() != null)
                {
                    player.GetComponent<PlayerController>().PlayerIndex = 0;
                }
            }
        }
    }
}