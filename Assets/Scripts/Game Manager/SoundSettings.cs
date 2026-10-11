using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "Sounds/SoundSettingsPreset", fileName = "SoundSettingsPreset")]
public class SoundSettings : ScriptableObject
{
    public AudioMixer AudioMixer;
    [Header("MasterVolume")]
    public string MasterVolumeName = "MasterVolume";
    [Range(-80, 0)]
    public float MasterVolume;

    [Header("BGMVolume")]
    public string BGMVolumeName = "BGMVolume";
    [Range(-80, 0)]
    public float BGMVolume;

    [Header("SFXVolume")]
    public string SFXVolumeName = "SFXVolume";
    [Range(-80, 0)]
    public float SFXVolume;
}
