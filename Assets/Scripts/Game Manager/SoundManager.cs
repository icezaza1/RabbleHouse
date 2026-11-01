using UnityEngine;
using UnityEngine.Audio;

public class SoundManager : Singleton<SoundManager>
{
    [SerializeField] private AudioSource SFXObject;
    private AudioSource musicSource;

    public override void Awake()
    {
        base.Awake();

        // SoundManager is kept across scenes by Singleton, so this source can
        // own the currently playing music for the lifetime of the game.
        musicSource = GetComponent<AudioSource>();
        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
    }

    public void PlaySound(AudioClip audioClip, Transform spawnTransform, float volume, AudioMixerGroup mixerGroup = null, float spatialBlend = 1f)
    {
        // Spawn gameobject
        AudioSource audioSource = Instantiate(SFXObject, spawnTransform.position, Quaternion.identity);

        audioSource.clip = audioClip;
        audioSource.volume = volume;
        audioSource.spatialBlend = spatialBlend;

        if (mixerGroup != null)
            audioSource.outputAudioMixerGroup = mixerGroup;

        audioSource.Play();
        Destroy(audioSource.gameObject, audioSource.clip.length);
    }

    public void PlayRandomSound(AudioClip[] audioClip, Transform spawnTransform, float volume, AudioMixerGroup mixerGroup = null, float spatialBlend = 1f)
    {
        // Assign random index
        int rand = Random.Range(0, audioClip.Length);

        // Spawn gameobject
        AudioSource audioSource = Instantiate(SFXObject, spawnTransform.position, Quaternion.identity);

        audioSource.clip = audioClip[rand];
        audioSource.volume = volume;
        audioSource.spatialBlend = spatialBlend;

        if (mixerGroup != null)
            audioSource.outputAudioMixerGroup = mixerGroup;

        audioSource.Play();
        Destroy(audioSource.gameObject, audioSource.clip.length);
    }

    public void PlayMusic(AudioClip[] audioClip, float volume, AudioMixerGroup mixerGroup = null, float spatialBlend = 0f)
    {
        if (audioClip == null || audioClip.Length == 0)
        {
            Debug.LogWarning("PlayMusic called with no music clips.", this);
            return;
        }

        // Reuse the same source: calling this again replaces the current track.
        int rand = Random.Range(0, audioClip.Length);
        musicSource.Stop();
        musicSource.clip = audioClip[rand];
        musicSource.volume = volume;
        musicSource.spatialBlend = spatialBlend;
        musicSource.loop = true;
        musicSource.outputAudioMixerGroup = mixerGroup;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }
}
