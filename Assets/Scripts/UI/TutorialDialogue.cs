using TMPro;
using UnityEngine;

public class TutorialDialogue : MonoBehaviour
{
    [TextArea(3, 10)]
    public string dialogueText; // The text to display
    public TextMeshProUGUI uiDialogueTextField; // Reference to your UI text component

    private bool hasTriggered = false; // Makes sure it only happens once

    private void OnTriggerEnter(Collider other)
    {
        if (!hasTriggered && other.CompareTag("Player"))
        {
            hasTriggered = true;

            // Update the dialogue text box
            if (uiDialogueTextField != null)
            {
                uiDialogueTextField.text = dialogueText;
            }

            // Optional: Disable or destroy the trigger after use
            gameObject.SetActive(false);
        }
    }
}
