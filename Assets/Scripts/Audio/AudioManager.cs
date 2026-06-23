using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    private const string GameplaySceneName = "SceneMapaVR";
    private const string DefeatSceneName = "Defeat";

    [SerializeField] private AudioLibrary library;
    [SerializeField, Min(0f)] private float musicFadeDuration = 0.5f;
    [SerializeField, Min(1)] private int initialSfxPoolSize = 8;
    [SerializeField, Min(1)] private int maxSfxPoolSize = 24;

    private readonly List<AudioSource> _sfxSources = new List<AudioSource>();
    private AudioSource _musicSourceA;
    private AudioSource _musicSourceB;
    private AudioSource _activeMusicSource;
    private Coroutine _musicFadeRoutine;
    private GameMusicState? _currentMusicState;

    public static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureSources();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        GameAudioEvents.CarpinchoDied += HandleCarpinchoDied;
        GameAudioEvents.HammerGrabbed += HandleHammerGrabbed;
        GameAudioEvents.MenuTeleported += HandleMenuTeleported;
        GameAudioEvents.MusicRequested += PlayMusic;
    }

    private void Start()
    {
        PlayMusic(GetMusicStateForScene(SceneManager.GetActiveScene().name));
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        GameAudioEvents.CarpinchoDied -= HandleCarpinchoDied;
        GameAudioEvents.HammerGrabbed -= HandleHammerGrabbed;
        GameAudioEvents.MenuTeleported -= HandleMenuTeleported;
        GameAudioEvents.MusicRequested -= PlayMusic;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void PlayMusic(GameMusicState state)
    {
        if (_currentMusicState == state)
        {
            return;
        }

        AudioCue cue = library != null ? library.GetMusic(state) : null;
        AudioClip clip = cue != null ? cue.PickClip() : null;
        if (clip == null)
        {
            _currentMusicState = state;
            return;
        }

        _currentMusicState = state;

        if (_musicFadeRoutine != null)
        {
            StopCoroutine(_musicFadeRoutine);
        }

        _musicFadeRoutine = StartCoroutine(FadeToMusic(cue, clip));
    }

    public void PlaySfxAt(AudioCue cue, Vector3 position)
    {
        AudioClip clip = cue != null ? cue.PickClip() : null;
        if (clip == null)
        {
            return;
        }

        AudioSource source = GetSfxSource();
        if (source == null)
        {
            return;
        }

        source.transform.position = position;
        source.Stop();
        source.clip = clip;
        cue.ApplyTo(source);
        source.loop = false;
        source.Play();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayMusic(GetMusicStateForScene(scene.name));
    }

    private void HandleCarpinchoDied(Vector3 position)
    {
        PlaySfxAt(library != null ? library.CarpinchoDeath : null, position);
    }

    private void HandleHammerGrabbed(Vector3 position)
    {
        PlaySfxAt(library != null ? library.HammerGrab : null, position);
    }

    private void HandleMenuTeleported(Vector3 position)
    {
        PlaySfxAt(library != null ? library.MenuTeleport : null, position);
    }

    private IEnumerator FadeToMusic(AudioCue cue, AudioClip clip)
    {
        AudioSource previous = _activeMusicSource;
        AudioSource next = previous == _musicSourceA ? _musicSourceB : _musicSourceA;

        next.Stop();
        next.clip = clip;
        cue.ApplyTo(next);
        next.loop = true;
        next.volume = 0f;
        next.Play();

        float targetVolume = cue.Volume;
        float previousStartVolume = previous != null ? previous.volume : 0f;
        float duration = Mathf.Max(0.01f, musicFadeDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / duration;
            next.volume = Mathf.Lerp(0f, targetVolume, t);

            if (previous != null)
            {
                previous.volume = Mathf.Lerp(previousStartVolume, 0f, t);
            }

            yield return null;
        }

        next.volume = targetVolume;

        if (previous != null)
        {
            previous.Stop();
            previous.clip = null;
        }

        _activeMusicSource = next;
        _musicFadeRoutine = null;
    }

    private void EnsureSources()
    {
        _musicSourceA = CreateSource("Music Source A", transform);
        _musicSourceB = CreateSource("Music Source B", transform);
        _activeMusicSource = _musicSourceA;

        int poolSize = Mathf.Clamp(initialSfxPoolSize, 1, maxSfxPoolSize);
        for (int i = 0; i < poolSize; i++)
        {
            _sfxSources.Add(CreateSource($"SFX Source {i:00}", transform));
        }
    }

    private AudioSource GetSfxSource()
    {
        for (int i = 0; i < _sfxSources.Count; i++)
        {
            if (!_sfxSources[i].isPlaying)
            {
                return _sfxSources[i];
            }
        }

        if (_sfxSources.Count >= maxSfxPoolSize)
        {
            return null;
        }

        AudioSource source = CreateSource($"SFX Source {_sfxSources.Count:00}", transform);
        _sfxSources.Add(source);
        return source;
    }

    private static AudioSource CreateSource(string sourceName, Transform parent)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(parent, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return source;
    }

    private static GameMusicState GetMusicStateForScene(string sceneName)
    {
        if (sceneName == GameplaySceneName)
        {
            return GameMusicState.Gameplay;
        }

        if (sceneName.Contains(DefeatSceneName))
        {
            return GameMusicState.Defeat;
        }

        return GameMusicState.Menu;
    }
}
