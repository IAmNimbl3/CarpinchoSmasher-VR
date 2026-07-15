using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MaterialOutlineHighlighter : MonoBehaviour
{
    private const string OutlineShaderName = "Carpincho/Inflated Mesh Outline";

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [SerializeField] private Color outlineColor = new Color(1f, 0.82f, 0.15f, 0.65f);
    [SerializeField, Min(0f)] private float outlineWidth = 0.02f;

    private readonly Dictionary<Renderer, Material[]> _sourceMaterials = new Dictionary<Renderer, Material[]>();
    private Material _runtimeMaterial;
    private bool _isHighlighted;

    public bool IsHighlighted => _isHighlighted;

    public void Configure(Color color, float width)
    {
        outlineColor = color;
        outlineWidth = Mathf.Max(0f, width);
        ApplyMaterialProperties();
    }

    public void SetHighlighted(bool highlighted)
    {
        if (_isHighlighted == highlighted)
        {
            return;
        }

        _isHighlighted = highlighted;
        if (highlighted)
        {
            ApplyOutlineMaterial();
        }
        else
        {
            RestoreSourceMaterials();
        }
    }

    private void OnDisable()
    {
        RestoreSourceMaterials();
        _isHighlighted = false;
    }

    private void OnDestroy()
    {
        RestoreSourceMaterials();

        if (_runtimeMaterial != null)
        {
            Destroy(_runtimeMaterial);
        }
    }

    private void ApplyOutlineMaterial()
    {
        Material material = GetOutlineMaterial();
        if (material == null)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || _sourceMaterials.ContainsKey(renderer))
            {
                continue;
            }

            Material[] source = renderer.sharedMaterials;
            _sourceMaterials.Add(renderer, source);

            Material[] highlighted = new Material[source.Length + 1];
            source.CopyTo(highlighted, 0);
            highlighted[highlighted.Length - 1] = material;
            renderer.sharedMaterials = highlighted;
        }
    }

    private Material GetOutlineMaterial()
    {
        if (_runtimeMaterial != null)
        {
            return _runtimeMaterial;
        }

        Shader shader = Shader.Find(OutlineShaderName);
        if (shader == null)
        {
            Debug.LogError($"[MaterialOutlineHighlighter] Shader '{OutlineShaderName}' was not found.", this);
            return null;
        }

        _runtimeMaterial = new Material(shader)
        {
            name = $"Runtime Trophy Outline ({name})",
            hideFlags = HideFlags.DontSave
        };
        ApplyMaterialProperties();
        return _runtimeMaterial;
    }

    private void ApplyMaterialProperties()
    {
        if (_runtimeMaterial == null)
        {
            return;
        }

        _runtimeMaterial.SetColor(OutlineColorId, outlineColor);
        _runtimeMaterial.SetFloat(OutlineWidthId, outlineWidth);
    }

    private void RestoreSourceMaterials()
    {
        foreach (KeyValuePair<Renderer, Material[]> pair in _sourceMaterials)
        {
            if (pair.Key != null)
            {
                pair.Key.sharedMaterials = pair.Value;
            }
        }

        _sourceMaterials.Clear();
    }
}
