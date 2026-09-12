using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : Singleton<SceneTransitionManager>
{
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    public void UnloadScene(string sceneName)
    {
    SceneManager.UnloadSceneAsync(sceneName);
    }
    
    public void LoadSceneAdditive(string sceneName)
    {
        if (IsSceneLoaded(sceneName))
            return;
        SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
    }

    public bool IsSceneLoaded(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);

        return scene.IsValid() && scene.isLoaded;
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
