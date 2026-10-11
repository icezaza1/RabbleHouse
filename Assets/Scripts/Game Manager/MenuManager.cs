using UnityEngine;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private string MenuName;
    private void Start()
    {
        SceneTransitionManager.Instance.LoadSceneAdditive(MenuName);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
