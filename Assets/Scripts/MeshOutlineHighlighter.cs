using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class MeshOutlineHighlighter : MonoBehaviour
{
    private const string OutlineObjectPrefix = "__RuntimeMeshOutline_";

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private Color outlineColor = new Color(1f, 0.82f, 0.15f, 0.32f);
    [SerializeField, Min(0f)] private float outlineWidth = 0.015f;
    [SerializeField] private Material outlineMaterial;

    private readonly List<GameObject> _outlineObjects = new();
    private Material _runtimeMaterial;

    public Color OutlineColor
    {
        get => outlineColor;
        set
        {
            outlineColor = value;
            ApplyMaterialProperties();
        }
    }

    public float OutlineWidth
    {
        get => outlineWidth;
        set
        {
            outlineWidth = Mathf.Max(0f, value);
            ApplyOutlineScale();
        }
    }

    private void OnEnable()
    {
        RebuildOutlines();
        SetOutlinesVisible(true);
    }

    private void OnDisable()
    {
        SetOutlinesVisible(false);
    }

    private void OnDestroy()
    {
        ClearOutlines();

        if (_runtimeMaterial != null)
        {
            Destroy(_runtimeMaterial);
        }
    }

    private void OnValidate()
    {
        outlineWidth = Mathf.Max(0f, outlineWidth);
        ApplyMaterialProperties();
        ApplyOutlineScale();
    }

    private void RebuildOutlines()
    {
        ClearOutlines();
        Material material = GetOutlineMaterial();
        if (material == null)
        {
            return;
        }

        MeshFilter[] sourceFilters = GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter sourceFilter in sourceFilters)
        {
            if (sourceFilter == null
                || sourceFilter.sharedMesh == null
                || sourceFilter.name.StartsWith(OutlineObjectPrefix))
            {
                continue;
            }

            MeshRenderer sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();
            if (sourceRenderer == null)
            {
                continue;
            }

            GameObject outlineObject = new GameObject(OutlineObjectPrefix + sourceFilter.name);
            outlineObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            outlineObject.transform.SetParent(sourceFilter.transform, false);
            outlineObject.transform.localPosition = Vector3.zero;
            outlineObject.transform.localRotation = Quaternion.identity;

            MeshFilter outlineFilter = outlineObject.AddComponent<MeshFilter>();
            outlineFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
            int materialCount = Mathf.Max(1, sourceRenderer.sharedMaterials.Length);
            Material[] materials = new Material[materialCount];
            for (int i = 0; i < materialCount; i++)
            {
                materials[i] = material;
            }

            outlineRenderer.sharedMaterials = materials;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
            outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            outlineRenderer.allowOcclusionWhenDynamic = false;

            _outlineObjects.Add(outlineObject);
        }

        ApplyMaterialProperties();
        ApplyOutlineScale();
    }

    private Material GetOutlineMaterial()
    {
        if (outlineMaterial != null)
        {
            return outlineMaterial;
        }

        if (_runtimeMaterial != null)
        {
            return _runtimeMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            return null;
        }

        _runtimeMaterial = new Material(shader)
        {
            name = "Runtime Mesh Outline",
            renderQueue = (int)RenderQueue.Transparent
        };

        _runtimeMaterial.SetInt("_Cull", (int)CullMode.Front);
        _runtimeMaterial.SetInt("_ZWrite", 0);
        _runtimeMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _runtimeMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        _runtimeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        return _runtimeMaterial;
    }

    private void ApplyMaterialProperties()
    {
        Material material = outlineMaterial != null ? outlineMaterial : _runtimeMaterial;
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, outlineColor);
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, outlineColor);
        }
    }

    private void ApplyOutlineScale()
    {
        float inflatedScale = 1f + outlineWidth;
        foreach (GameObject outlineObject in _outlineObjects)
        {
            if (outlineObject != null)
            {
                outlineObject.transform.localScale = Vector3.one * inflatedScale;
            }
        }
    }

    private void SetOutlinesVisible(bool visible)
    {
        foreach (GameObject outlineObject in _outlineObjects)
        {
            if (outlineObject != null)
            {
                outlineObject.SetActive(visible);
            }
        }
    }

    private void ClearOutlines()
    {
        for (int i = _outlineObjects.Count - 1; i >= 0; i--)
        {
            GameObject outlineObject = _outlineObjects[i];
            if (outlineObject == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(outlineObject);
            }
            else
            {
                DestroyImmediate(outlineObject);
            }
        }

        _outlineObjects.Clear();
    }
}
