using UnityEngine;

public class TeleportHotspotVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material hotspotMaterial;

    [Header("Runtime Visual")]
    [SerializeField] private bool createRuntimeCylinder = true;
    [SerializeField] private string runtimeVisualName = "GTA_HoverVisual";
    [SerializeField] private Vector3 runtimeVisualScale = new Vector3(1.4f, 0.015f, 1.4f);
    [SerializeField, Min(0f)] private float floorOffset = 0.02f;
    [SerializeField] private bool projectVisualToFloor = true;
    [SerializeField] private LayerMask floorMask = ~0;
    [SerializeField, Min(0.05f)] private float floorRaycastUpOffset = 5f;
    [SerializeField, Min(0.05f)] private float floorRaycastDownDistance = 20f;

    [Header("Visual State")]
    [SerializeField] private bool visibleWhileIdle = true;
    [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.22f;
    [SerializeField, Range(0f, 1f)] private float hoverAlpha = 0.58f;
    [SerializeField, Min(0f)] private float hoverEmissionMultiplier = 1.8f;
    [SerializeField] private Color baseColor = new Color(1f, 0.18f, 0.28f, 1f);
    [SerializeField] private Color emissionColor = new Color(1f, 0.1f, 0.18f, 1f);

    private Material _runtimeMaterial;
    private bool _nativeHovered;
    private bool _externalHovered;

    private void Awake()
    {
        EnsureVisual();
        ApplyState();
    }

    private void OnEnable()
    {
        ApplyState();
    }

    public void Show()
    {
        _nativeHovered = true;
        ApplyState();
    }

    public void Hide()
    {
        _nativeHovered = false;
        ApplyState();
    }

    public void SetHover(bool hover)
    {
        SetExternalHover(hover);
    }

    public void SetExternalHover(bool hover)
    {
        _externalHovered = hover;
        ApplyState();
    }

    private void EnsureVisual()
    {
        if (targetRenderer != null)
        {
            EnsureMaterial();
            ProjectVisualToFloor();
            return;
        }

        Transform existing = transform.Find(runtimeVisualName);
        if (existing != null)
        {
            targetRenderer = existing.GetComponent<Renderer>();
            EnsureMaterial();
            ProjectVisualToFloor();
            return;
        }

        if (!createRuntimeCylinder)
        {
            targetRenderer = GetComponentInChildren<Renderer>(true);
            EnsureMaterial();
            ProjectVisualToFloor();
            return;
        }

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = runtimeVisualName;
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = Vector3.up * floorOffset;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = runtimeVisualScale;

        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Destroy(visualCollider);
        }

        targetRenderer = visual.GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            targetRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            targetRenderer.receiveShadows = false;
        }

        EnsureMaterial();
        ProjectVisualToFloor();
    }

    private void ProjectVisualToFloor()
    {
        if (!projectVisualToFloor || targetRenderer == null)
        {
            return;
        }

        Transform visual = targetRenderer.transform;
        Vector3 origin = transform.position + Vector3.up * floorRaycastUpOffset;
        float maxDistance = floorRaycastUpOffset + floorRaycastDownDistance;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, floorMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        visual.position = hit.point + hit.normal * floorOffset;

        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, hit.normal);
        if (flatForward.sqrMagnitude < 0.0001f)
        {
            flatForward = Vector3.ProjectOnPlane(Vector3.forward, hit.normal);
        }

        visual.rotation = Quaternion.LookRotation(flatForward.normalized, hit.normal);
    }

    private void EnsureMaterial()
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (_runtimeMaterial == null)
        {
            Material source = hotspotMaterial != null
                ? hotspotMaterial
                : Resources.Load<Material>("M_TeleportHotspotGTA");

            if (source != null)
            {
                _runtimeMaterial = new Material(source)
                {
                    name = $"{name}_TeleportHotspotVisual"
                };
            }
            else
            {
                Shader shader = Shader.Find("Carpincho/Teleport Hotspot GTA")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Standard");

                if (shader != null)
                {
                    _runtimeMaterial = new Material(shader)
                    {
                        name = $"{name}_TeleportHotspotVisual"
                    };
                }
            }
        }

        if (_runtimeMaterial != null)
        {
            targetRenderer.sharedMaterial = _runtimeMaterial;
        }
    }

    private void ApplyState()
    {
        EnsureVisual();

        if (targetRenderer == null)
        {
            return;
        }

        bool hovered = _nativeHovered || _externalHovered;
        bool visible = hovered || visibleWhileIdle || idleAlpha > 0f;
        targetRenderer.enabled = visible;

        if (!visible)
        {
            return;
        }

        EnsureMaterial();

        Material material = targetRenderer.sharedMaterial;
        if (material == null)
        {
            return;
        }

        float alpha = hovered ? hoverAlpha : idleAlpha;
        Color currentEmission = emissionColor * (hovered ? hoverEmissionMultiplier : 1f);

        if (material.HasProperty("_Alpha"))
        {
            material.SetFloat("_Alpha", alpha);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", baseColor);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", currentEmission);
        }

        if (material.HasProperty("_Color"))
        {
            Color fallbackColor = baseColor;
            fallbackColor.a = alpha;
            material.SetColor("_Color", fallbackColor);
        }
    }
}
