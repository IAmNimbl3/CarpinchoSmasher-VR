using UnityEngine;

public static class PlayerTarget
{
    public static Transform Transform
    {
        get
        {
            Camera cam = Camera.main;
            return cam != null ? cam.transform : null;
        }
    }

    public static Vector3 Position
    {
        get
        {
            Transform t = Transform;
            return t != null ? t.position : Vector3.zero;
        }
    }

    public static bool TryGetPosition(out Vector3 position)
    {
        Transform t = Transform;
        if (t != null)
        {
            position = t.position;
            return true;
        }
        position = Vector3.zero;
        return false;
    }
}
