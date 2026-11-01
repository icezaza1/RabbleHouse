using UnityEngine;
using UnityEngine.Audio;

public class AppFlowButtons : MonoBehaviour
{
    [SerializeField] private AudioClip onClickedSound;
    [SerializeField] private AudioMixerGroup mixerGroup;

    public void PlayClickSound()
    {
        if (onClickedSound != null && mixerGroup != null)
            SoundManager.Instance.PlaySound(onClickedSound, transform, 1f, mixerGroup, 0f);
    }
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
