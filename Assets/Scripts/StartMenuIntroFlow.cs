using System.Collections;
using UnityEngine;

public class StartMenuIntroFlow : MonoBehaviour
{
    [Header("Rig")]
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private Transform centerEyeAnchor;
    [SerializeField] private PlayerBounds playerBounds;

    [Header("Intro")]
    [SerializeField] private Transform initialSpawnPoint;
    [SerializeField] private GameObject introLogoRoot;

    [Header("Menu Activation")]
    [SerializeField] private Transform tvTeleportPoint;
    [SerializeField] private GameObject tvMenuRoot;
    [SerializeField, Min(0.05f)] private float activationRadius = 0.8f;
    [SerializeField] private bool hideIntroLogoWhenMenuActivates = true;

    [Header("Teleport Floor Visuals")]
    [SerializeField] private bool createTeleportFloorVisuals = true;
    [SerializeField] private Transform trophiesTeleportPoint;
    [SerializeField] private string trophiesTeleportPointName = "Teleport_TrophiesShelf";
    [SerializeField] private Material teleportFloorVisualMaterial;
    [SerializeField] private Vector3 teleportFloorVisualScale = new Vector3(1.4f, 0.015f, 1.4f);
    [SerializeField, Min(0f)] private float teleportFloorOffset = 0.02f;

    private bool _menuActivated;
    private Material _runtimeTeleportVisualMaterial;

    private IEnumerator Start()
    {
        ResolveRigReferences();
        ConfigurePlayerBounds();
        ResolveTeleportReferences();

        if (tvMenuRoot != null)
        {
            tvMenuRoot.SetActive(false);
        }

        yield return null;

        ResolveRigReferences();
        ConfigurePlayerBounds();
        MoveRigTo(initialSpawnPoint);
        CreateTeleportFloorVisuals();
    }

    private void Update()
    {
        if (_menuActivated || tvTeleportPoint == null || tvMenuRoot == null)
        {
            return;
        }

        ResolveRigReferences();

        if (centerEyeAnchor == null)
        {
            return;
        }

        Vector2 eyePosition = new Vector2(centerEyeAnchor.position.x, centerEyeAnchor.position.z);
        Vector2 targetPosition = new Vector2(tvTeleportPoint.position.x, tvTeleportPoint.position.z);

        if (Vector2.Distance(eyePosition, targetPosition) <= activationRadius)
        {
            ActivateTvMenu();
        }
    }

    private void ActivateTvMenu()
    {
        _menuActivated = true;
        tvMenuRoot.SetActive(true);
        GameAudioEvents.RaiseMenuTeleported(tvTeleportPoint.position);

        if (hideIntroLogoWhenMenuActivates && introLogoRoot != null)
        {
            introLogoRoot.SetActive(false);
        }
    }

    private void ResolveRigReferences()
    {
        if (cameraRig == null)
        {
            cameraRig = FindAnyObjectByType<OVRCameraRig>();
        }

        if (centerEyeAnchor == null && cameraRig != null)
        {
            centerEyeAnchor = cameraRig.centerEyeAnchor;
        }

        if (playerBounds == null && cameraRig != null)
        {
            playerBounds = cameraRig.GetComponent<PlayerBounds>();
        }
    }

    private void ResolveTeleportReferences()
    {
        if (trophiesTeleportPoint == null && !string.IsNullOrEmpty(trophiesTeleportPointName))
        {
            GameObject trophyPoint = GameObject.Find(trophiesTeleportPointName);
            if (trophyPoint != null)
            {
                trophiesTeleportPoint = trophyPoint.transform;
            }
        }
    }

    private void ConfigurePlayerBounds()
    {
        if (playerBounds != null && initialSpawnPoint != null)
        {
            playerBounds.SetSpawnPoint(initialSpawnPoint);
        }
    }

    private void MoveRigTo(Transform target)
    {
        if (cameraRig == null || target == null)
        {
            return;
        }

        Transform rigTransform = cameraRig.transform;
        Vector3 targetForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (targetForward.sqrMagnitude < 0.001f)
        {
            targetForward = Vector3.forward;
        }

        rigTransform.SetPositionAndRotation(
            target.position,
            Quaternion.LookRotation(targetForward, Vector3.up));
    }

    private void CreateTeleportFloorVisuals()
    {
        if (!createTeleportFloorVisuals)
        {
            return;
        }

        ResolveTeleportReferences();
        CreateTeleportFloorVisual(tvTeleportPoint, "Teleport_FrontOfTV_FloorVisual");
        CreateTeleportFloorVisual(trophiesTeleportPoint, "Teleport_TrophiesShelf_FloorVisual");
    }

    private void CreateTeleportFloorVisual(Transform teleportTarget, string objectName)
    {
        if (teleportTarget == null || GameObject.Find(objectName) != null)
        {
            return;
        }

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = objectName;
        visual.transform.position = teleportTarget.position;
        visual.transform.rotation = Quaternion.LookRotation(ProjectForwardOnFloor(teleportTarget.forward), Vector3.up);
        visual.transform.localScale = teleportFloorVisualScale;

        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Destroy(visualCollider);
        }

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetTeleportFloorVisualMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        TeleportFloorVisualProjector projector = visual.AddComponent<TeleportFloorVisualProjector>();
        projector.Configure(teleportTarget, visual.transform, teleportFloorOffset);
        projector.ProjectVisualToFloor();
    }

    private Material GetTeleportFloorVisualMaterial()
    {
        if (teleportFloorVisualMaterial != null)
        {
            return teleportFloorVisualMaterial;
        }

        if (_runtimeTeleportVisualMaterial != null)
        {
            return _runtimeTeleportVisualMaterial;
        }

        Shader shader = Shader.Find("Carpincho/Teleport Hotspot GTA");
        Shader fallbackShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        if (shader == null && fallbackShader == null)
        {
            return null;
        }

        _runtimeTeleportVisualMaterial = new Material(shader != null ? shader : fallbackShader);

        _runtimeTeleportVisualMaterial.name = "Runtime_TeleportHotspotGTA";
        _runtimeTeleportVisualMaterial.SetColor("_BaseColor", new Color(1f, 0.18f, 0.28f, 1f));
        _runtimeTeleportVisualMaterial.SetColor("_EmissionColor", new Color(1f, 0.1f, 0.18f, 1f));
        _runtimeTeleportVisualMaterial.SetFloat("_Alpha", 0.42f);
        _runtimeTeleportVisualMaterial.SetFloat("_TopFade", 0.58f);
        _runtimeTeleportVisualMaterial.SetFloat("_BottomGlow", 0.9f);
        _runtimeTeleportVisualMaterial.SetFloat("_RimPower", 2.3f);
        _runtimeTeleportVisualMaterial.SetFloat("_RimIntensity", 1.35f);
        _runtimeTeleportVisualMaterial.SetFloat("_PulseSpeed", 1.4f);
        _runtimeTeleportVisualMaterial.SetFloat("_PulseAmount", 0.16f);
        return _runtimeTeleportVisualMaterial;
    }

    private Vector3 ProjectForwardOnFloor(Vector3 forward)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        return flatForward.sqrMagnitude > 0.0001f ? flatForward.normalized : Vector3.forward;
    }
}
