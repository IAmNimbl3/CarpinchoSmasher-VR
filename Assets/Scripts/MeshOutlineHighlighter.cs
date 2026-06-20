using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class MeshOutlineHighlighter : MonoBehaviour
{
    private const string ShaderGraphOutlineShaderName = "Shader Graphs/InflatedMeshOutlineShaderGraph";
    private const string FallbackOutlineShaderName = "Carpincho/Inflated Mesh Outline";

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [SerializeField] private Color outlineColor = new Color(1f, 0.82f, 0.15f, 0.32f);
    [SerializeField, Min(0f)] private float outlineWidth = 0.015f;
    [SerializeField] private Material outlineMaterial;

    private MeshFilter[] _meshFilters;
    private Renderer[] _renderers;
    private Material _runtimeMaterial;
    private MaterialPropertyBlock _properties;
    private bool _subscribed;

    public Color OutlineColor
    {
        get => outlineColor;
        set => outlineColor = value;
    }

    public float OutlineWidth
    {
        get => outlineWidth;
        set => outlineWidth = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        CacheRenderers();
        EnsureMaterial();
    }

    private void OnEnable()
    {
        CacheRenderers();
        EnsureMaterial();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (_runtimeMaterial != null)
        {
            Destroy(_runtimeMaterial);
        }
    }

    private void CacheRenderers()
    {
        _meshFilters = GetComponentsInChildren<MeshFilter>(true);
        _renderers = new Renderer[_meshFilters.Length];

        for (int i = 0; i < _meshFilters.Length; i++)
        {
            _renderers[i] = _meshFilters[i] != null
                ? _meshFilters[i].GetComponent<Renderer>()
                : null;
        }
    }

    private void EnsureMaterial()
    {
        if (_properties == null)
        {
            _properties = new MaterialPropertyBlock();
        }

        if (outlineMaterial != null)
        {
            return;
        }

        if (_runtimeMaterial != null)
        {
            return;
        }

        Shader outlineShader = Shader.Find(ShaderGraphOutlineShaderName);
        if (outlineShader == null)
        {
            outlineShader = Shader.Find(FallbackOutlineShaderName);
        }

        if (outlineShader == null)
        {
            return;
        }

        _runtimeMaterial = new Material(outlineShader)
        {
            name = "Runtime Inflated Mesh Outline"
        };
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        RenderPipelineManager.endCameraRendering += RenderOutline;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        RenderPipelineManager.endCameraRendering -= RenderOutline;
        _subscribed = false;
    }

    private void RenderOutline(ScriptableRenderContext context, Camera camera)
    {
        Material material = outlineMaterial != null ? outlineMaterial : _runtimeMaterial;
        if (material == null || _meshFilters == null || _renderers == null)
        {
            return;
        }

        _properties.SetColor(OutlineColorId, outlineColor);
        _properties.SetFloat(OutlineWidthId, outlineWidth);

        CommandBuffer commandBuffer = CommandBufferPool.Get("Mesh Outline Highlighter");

        for (int i = 0; i < _meshFilters.Length; i++)
        {
            MeshFilter meshFilter = _meshFilters[i];
            Renderer sourceRenderer = _renderers[i];
            if (meshFilter == null || sourceRenderer == null || !sourceRenderer.enabled)
            {
                continue;
            }

            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                commandBuffer.DrawMesh(mesh, meshFilter.transform.localToWorldMatrix, material, subMeshIndex, 0, _properties);
            }
        }

        context.ExecuteCommandBuffer(commandBuffer);
        CommandBufferPool.Release(commandBuffer);
    }
}
