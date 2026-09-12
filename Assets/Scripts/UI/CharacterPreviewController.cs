using UnityEngine;

public class CharacterPreviewController : MonoBehaviour
{
    [SerializeField] private Transform previewSpawnPoint;

    private GameObject currentPreview;

    public void SetCharacter(CharacterData character)
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }

        currentPreview = Instantiate(
            character.previewModel,
            previewSpawnPoint.position,
            previewSpawnPoint.rotation
        );

        currentPreview.transform.SetParent(previewSpawnPoint);
    }
}
