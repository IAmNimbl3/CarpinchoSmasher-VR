using UnityEngine;

[CreateAssetMenu(fileName = "GameAudioLibrary", menuName = "Carpincho Smasher/Audio/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [Header("Music")]
    [SerializeField] private AudioCue menuMusic;
    [SerializeField] private AudioCue gameplayMusic;
    [SerializeField] private AudioCue defeatMusic;

    [Header("SFX")]
    [SerializeField] private AudioCue carpinchoDeath;
    [SerializeField] private AudioCue hammerGrab;
    [SerializeField] private AudioCue menuTeleport;

    public AudioCue CarpinchoDeath => carpinchoDeath;
    public AudioCue HammerGrab => hammerGrab;
    public AudioCue MenuTeleport => menuTeleport;

    public AudioCue GetMusic(GameMusicState state)
    {
        return state switch
        {
            GameMusicState.Gameplay => gameplayMusic,
            GameMusicState.Defeat => defeatMusic,
            _ => menuMusic
        };
    }
}
