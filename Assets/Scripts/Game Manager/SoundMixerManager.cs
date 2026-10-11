using UnityEngine;
using UnityEngine.Audio;

public class SoundMixerManager : MonoBehaviour
{
    [SerializeField] protected SoundSettings m_SoundSettings;

    private void Start()
    {
        InitialiseVolumes();
    }

    private void InitialiseVolumes()
    {
        SetMusicVolume(m_SoundSettings.BGMVolume);
        SetSFXVolume(m_SoundSettings.SFXVolume);
    }

    public void SetSFXVolume(float level)
    {
        m_SoundSettings.AudioMixer.SetFloat("SFXVolume", level);
        //Set float to the scriptable object to persist the value although the game is closed
        m_SoundSettings.BGMVolume = level;
    }

    public void SetMusicVolume(float level)
    {
        m_SoundSettings.AudioMixer.SetFloat("MusicVolume", level);
        //Set float to the scriptable object to persist the value although the game is closed
        m_SoundSettings.SFXVolume = level;
    }
}
