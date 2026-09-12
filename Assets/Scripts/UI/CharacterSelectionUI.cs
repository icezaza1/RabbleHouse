using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectionUI : MonoBehaviour
{
    [Header("Character Select")]
    [SerializeField] private TextMeshProUGUI characterText;
    [SerializeField] private Button characterLeftArrow;
    [SerializeField] private Button characterRightArrow;

    [Header("Character Data")]
    [SerializeField] private CharacterData[] characters;

    [Header("Confirm")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private string nextSceneName = "LobbyCreation";

    private int currentCharacterIndex = 0;
    private CharacterPreviewController characterPreview;

    private void Start()
    {
        characterPreview = GetComponent<CharacterPreviewController>();
        // Subscribe to button clicks
        characterLeftArrow.onClick.AddListener(OnCharacterLeft);
        characterRightArrow.onClick.AddListener(OnCharacterRight);
        confirmButton.onClick.AddListener(OnConfirm);
        // Initialize display
        UpdateCharacterDisplay();
    }
    private void OnCharacterLeft()
    {
        currentCharacterIndex--;
        if (currentCharacterIndex < 0) currentCharacterIndex = characters.Length - 1;
        UpdateCharacterDisplay();
    }

    private void OnCharacterRight()
    {
        currentCharacterIndex++;
        if (currentCharacterIndex >= characters.Length) currentCharacterIndex = 0;
        UpdateCharacterDisplay();
    }

    private void UpdateCharacterDisplay()
    {
        CharacterData character = characters[currentCharacterIndex];
        characterPreview.SetCharacter(character);

        characterText.text = character.characterName;
    }

    private void OnConfirm()
    {
        LobbyData.SelectedCharacter = characters[currentCharacterIndex];

        SceneTransitionManager.Instance.LoadSceneAdditive(nextSceneName);
    }
}
