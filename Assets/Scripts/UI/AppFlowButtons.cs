using UnityEngine;

public class AppFlowButtons : MonoBehaviour
{
    public void LoadScene(string sceneName)
    {
        SceneTransitionManager.Instance.LoadScene(sceneName);
    }

    public void LoadSceneAdditive(string sceneName)
    {
        SceneTransitionManager.Instance.LoadSceneAdditive(sceneName);
    }

    public void UnloadScene(string sceneName)
    {
        SceneTransitionManager.Instance.UnloadScene(sceneName);
    }

    public void Quit()
    {
        SceneTransitionManager.Instance.ExitGame();
    }
}
