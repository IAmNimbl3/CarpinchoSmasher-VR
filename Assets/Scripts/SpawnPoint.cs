using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [Tooltip("Tipos que pueden spawnear acá. Vacío = acepta todos los tipos.")]
    [SerializeField] private CarpinchoType[] allowedTypes;

    [Tooltip("Radio (en XZ) del área de spawn. 0 = spawn puntual en transform.position.")]
    [SerializeField, Min(0f)] private float radius = 1.5f;

    [Tooltip("Color del gizmo en escena para identificar el punto.")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.4f, 0.1f, 0.8f);

    public float Radius => radius;

    public bool Accepts(CarpinchoType type)
    {
        if (allowedTypes == null || allowedTypes.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < allowedTypes.Length; i++)
        {
            if (allowedTypes[i] == type)
            {
                return true;
            }
        }

        return false;
    }

    public Vector3 GetRandomPosition()
    {
        if (radius <= 0f)
        {
            return transform.position;
        }

        Vector2 offset = Random.insideUnitCircle * radius;
        Vector3 pos = transform.position;
        pos.x += offset.x;
        pos.z += offset.y;
        return pos;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, 0.15f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);

        if (radius > 0f)
        {
            DrawCircle(transform.position, radius, 48);
        }
    }

    private static void DrawCircle(Vector3 center, float radius, int segments)
    {
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float t = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 curr = center + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }
    }
}
