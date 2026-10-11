using UnityEngine;


[RequireComponent(typeof(AudioSource))]
public class SoundManager : Singleton<SoundManager>
{
    [SerializeField] private AudioSource SFXObject;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    private AudioSource audioSource;

    private void Awake()
    {
        base.Awake();
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void PlaySound(AudioClip audioClip, Transform spawnTransform, float volume)
    {
        // Spawn gameobject
        AudioSource audioSource = Instantiate(SFXObject, spawnTransform.position, Quaternion.identity);

        audioSource.clip = audioClip;
        audioSource.volume = volume;
        audioSource.Play();

        float clipLength = audioSource.clip.length;

        Destroy(audioSource.gameObject, clipLength);
    }

    public void PlayRandomSound(AudioClip[] audioClip, Transform spawnTransform, float volume)
    {
        // Assign random index
        int rand = Random.Range(0, audioClip.Length);

        // Spawn gameobject
        AudioSource audioSource = Instantiate(SFXObject, spawnTransform.position, Quaternion.identity);

        audioSource.clip = audioClip[rand];
        audioSource.volume = volume;
        audioSource.Play();

        float clipLength = audioSource.clip.length;

        Destroy(audioSource.gameObject, clipLength);
    }
}
