using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "CharacterSoundSet", menuName = "Game/Character Sound Set")]
public class CharacterSoundSet : ScriptableObject
{
    [Header("Combat")]
    public AudioClip swing;
    public AudioClip hit;

    [Header("Character State")]
    public AudioClip death;

    [Range(0f, 1f)] public float volume = 1f;
    public AudioMixerGroup sfxMixerGroup;
}
