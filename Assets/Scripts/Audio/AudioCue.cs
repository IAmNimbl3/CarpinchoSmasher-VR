using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "AudioCue", menuName = "Carpincho Smasher/Audio/Audio Cue")]
public class AudioCue : ScriptableObject
{
    [SerializeField] private AudioCategory category = AudioCategory.Sfx;
    [SerializeField] private AudioClip[] clips;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private Vector2 pitchRange = Vector2.one;
    [SerializeField, Range(0f, 1f)] private float spatialBlend;
    [SerializeField] private bool spatialize;
    [SerializeField] private bool loop;
    [SerializeField, Min(0f)] private float minDistance = 1f;
    [SerializeField, Min(0.01f)] private float maxDistance = 20f;
    [SerializeField] private AudioMixerGroup output;

    public AudioCategory Category => category;
    public float Volume => volume;
    public bool Loop => loop;
    public bool HasClip => PickClip() != null;

    public AudioClip PickClip()
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        int startIndex = Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[(startIndex + i) % clips.Length];
            if (clip != null)
            {
                return clip;
            }
        }

        return null;
    }

    public float PickPitch()
    {
        float min = Mathf.Min(pitchRange.x, pitchRange.y);
        float max = Mathf.Max(pitchRange.x, pitchRange.y);
        return Mathf.Approximately(min, max) ? min : Random.Range(min, max);
    }

    public void ApplyTo(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.outputAudioMixerGroup = output;
        source.volume = volume;
        source.pitch = PickPitch();
        source.spatialBlend = spatialBlend;
        source.spatialize = spatialize;
        source.loop = loop;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.playOnAwake = false;
    }
}
