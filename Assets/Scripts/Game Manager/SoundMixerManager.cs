using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SoundMixerManager : MonoBehaviour
{
    [SerializeField] protected SoundSettings m_SoundSettings;

    public Slider m_SliderMusicVolume;
    public Slider m_SliderSFXVolume;

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
        m_SoundSettings.AudioMixer.SetFloat(m_SoundSettings.SFXVolumeName, level);
        //Set float to the scriptable object to persist the value although the game is closed
        m_SoundSettings.SFXVolume = level;
        //Set the slider bar’s value
        m_SliderSFXVolume.value = m_SoundSettings.SFXVolume;
    }

    public void SetMusicVolume(float level)
    {
        m_SoundSettings.AudioMixer.SetFloat(m_SoundSettings.BGMVolumeName, level);
        //Set float to the scriptable object to persist the value although the game is closed
        m_SoundSettings.BGMVolume = level;
        //Set the slider bar’s value
        m_SliderMusicVolume.value = m_SoundSettings.BGMVolume;
    }
}
