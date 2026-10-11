using UnityEngine;
using UnityEngine.Audio;

public class SoundMixerManager : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;

    public void SetSFXVolume(float level)
    {
        audioMixer.SetFloat("SFXVolume", level);
    }

    public void SetMusicVolume(float level)
    {
        audioMixer.SetFloat("MusicVolume", level);
    }
}
