using System;
using UnityEngine;
using UnityEngine.Audio;

public class SceneMusic : MonoBehaviour
{
    [SerializeField] private AudioClip[] musics;
    [SerializeField] private AudioMixerGroup mixerGroup;

    private void Start()
    {
        SoundManager.Instance.PlayMusic(musics, 0.5f, mixerGroup);
    }
}
