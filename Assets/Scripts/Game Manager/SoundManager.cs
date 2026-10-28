using UnityEngine;
using UnityEngine.Audio;
using static Unity.VisualScripting.Member;

public class SoundManager : Singleton<SoundManager>
{
    [SerializeField] private AudioSource SFXObject;

    public override void Awake()
    {
        base.Awake();
    }

    public void PlaySound(AudioClip audioClip, Transform spawnTransform, float volume, AudioMixerGroup mixerGroup = null)
    {
        // Spawn gameobject
        AudioSource audioSource = Instantiate(SFXObject, spawnTransform.position, Quaternion.identity);

        audioSource.clip = audioClip;
        audioSource.volume = volume;

        if (mixerGroup != null)
            audioSource.outputAudioMixerGroup = mixerGroup;

        audioSource.Play();
        Destroy(audioSource.gameObject, audioSource.clip.length);
    }

    public void PlayRandomSound(AudioClip[] audioClip, Transform spawnTransform, float volume, AudioMixerGroup mixerGroup = null)
    {
        // Assign random index
        int rand = Random.Range(0, audioClip.Length);

        // Spawn gameobject
        AudioSource audioSource = Instantiate(SFXObject, spawnTransform.position, Quaternion.identity);

        audioSource.clip = audioClip[rand];
        audioSource.volume = volume;

        if (mixerGroup != null)
            audioSource.outputAudioMixerGroup = mixerGroup;

        audioSource.Play();
        Destroy(audioSource.gameObject, audioSource.clip.length);
    }
}
