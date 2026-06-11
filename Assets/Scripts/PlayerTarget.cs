using UnityEngine;

public static class PlayerTarget
{
    private static Transform _cached;

    public static Transform Transform
    {
        get
        {
            if (_cached == null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    _cached = cam.transform;
                }
            }
            return _cached;
        }
    }

    public static Transform Root
    {
        get
        {
            Transform t = Transform;
            return t != null ? t.root : null;
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

    public static float HorizontalSqrDistance(Vector3 from, Vector3 playerPos)
    {
        float dx = from.x - playerPos.x;
        float dz = from.z - playerPos.z;
        return dx * dx + dz * dz;
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
