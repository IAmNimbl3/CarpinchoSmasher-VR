using System;
using UnityEngine;

public static class GameAudioEvents
{
    public static event Action<Vector3> CarpinchoDied;
    public static event Action<Vector3> HammerGrabbed;
    public static event Action<Vector3> MenuTeleported;
    public static event Action<GameMusicState> MusicRequested;

    public static void RaiseCarpinchoDied(Vector3 position)
    {
        CarpinchoDied?.Invoke(position);
    }

    public static void RaiseHammerGrabbed(Vector3 position)
    {
        HammerGrabbed?.Invoke(position);
    }

    public static void RaiseMenuTeleported(Vector3 position)
    {
        MenuTeleported?.Invoke(position);
    }

    public static void RequestMusic(GameMusicState state)
    {
        MusicRequested?.Invoke(state);
    }
}
