using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [Tooltip("Tipos que pueden spawnear acá. Vacío = acepta todos los tipos.")]
    [SerializeField] private CarpinchoType[] allowedTypes;

    [Tooltip("Color del gizmo en escena para identificar el punto.")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.4f, 0.1f, 0.8f);

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

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, 0.15f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);
    }
}
