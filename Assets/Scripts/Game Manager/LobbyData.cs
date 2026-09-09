using UnityEngine;

public static class LobbyData
{
    // Stage selection
    public static int SelectedStageIndex = 0;
    public static string[] StageNames = { "Living Room", "Kitchen" };
    // Add more as you create stages

    // AI difficulty (0=Easy, 1=Normal, 2=Hard)
    public static int SelectedDifficulty = 1; // default Normal

    // Match settings
    public static int PlayerCount = 3; // fixed at 3
}
