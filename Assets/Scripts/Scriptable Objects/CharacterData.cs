using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Basic Info")]
    public string characterName;
    public Sprite portrait;

    [Header("Prefabs")]
    public GameObject playerPrefab;
    public GameObject aiPrefab;

    [Header("Preview")]
    public GameObject previewModel;
}
