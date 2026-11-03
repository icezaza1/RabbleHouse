using UnityEngine;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private string MenuName;
    [SerializeField] protected SoundSettings m_SoundSettings;
    private void Start()
    {
        SceneTransitionManager.Instance.LoadSceneAdditive(MenuName);
        InitialiseVolumes();
    }

    private void InitialiseVolumes()
    {
        SetMusicVolume(m_SoundSettings.BGMVolume);
        SetSFXVolume(m_SoundSettings.SFXVolume);
    }

    private void SetSFXVolume(float level)
    {
        m_SoundSettings.AudioMixer.SetFloat(m_SoundSettings.SFXVolumeName, level);
        //Set float to the scriptable object to persist the value although the game is closed
        m_SoundSettings.SFXVolume = level;
    }

    private void SetMusicVolume(float level)
    {
        m_SoundSettings.AudioMixer.SetFloat(m_SoundSettings.BGMVolumeName, level);
        //Set float to the scriptable object to persist the value although the game is closed
        m_SoundSettings.BGMVolume = level;
    }
}
