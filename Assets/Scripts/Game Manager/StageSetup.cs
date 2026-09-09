using RabbleHouse;
using UnityEngine;

public class StageSetup : MonoBehaviour
{
    private void Awake()
    {
        // Find all characters and assign PlayerIndex
        PhysicCharacterController[] controllers = FindObjectsByType<PhysicCharacterController>();

        for (int i = 0; i < controllers.Length; i++)
        {
            PlayerHealth health = controllers[i].GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.PlayerIndex = i;  // 0 = human player, 1 = AI1, 2 = AI2
                Debug.Log($"[StageSetup] {controllers[i].gameObject.name} -> PlayerIndex {i}");
            }
        }
    }

    private void Start()
    {
        // Read selected difficulty from LobbyData
        int difficulty = LobbyData.SelectedDifficulty;

        Debug.Log($"[GameSetup] Applying difficulty: {difficulty}");

        // Find all AI characters and apply difficulty
        AIInputHandler[] allAI = FindObjectsByType<AIInputHandler>();

        foreach (var ai in allAI)
        {
            ai.SetDifficulty(difficulty);
        }
    }
}