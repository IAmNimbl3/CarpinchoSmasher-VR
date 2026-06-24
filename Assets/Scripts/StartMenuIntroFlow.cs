using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private bool createTeleportFloorVisuals;
    [SerializeField] private Transform trophiesTeleportPoint;
    [SerializeField] private string trophiesTeleportPointName = "Teleport_TrophiesShelf";
    [SerializeField] private Material teleportFloorVisualMaterial;
    [SerializeField] private Vector3 teleportFloorVisualScale = new Vector3(1.4f, 0.015f, 1.4f);
    [SerializeField, Min(0f)] private float teleportFloorOffset = 0.02f;
    [SerializeField] private bool showTeleportVisualOnlyWhenAimed;
    [SerializeField, Min(0.05f)] private float teleportAimRadius = 0.9f;
    [SerializeField, Min(0.5f)] private float teleportAimRayLength = 12f;
    [SerializeField, Range(0f, 1f)] private float teleportVisualIdleAlpha = 0.14f;
    [SerializeField, Range(0f, 1f)] private float teleportVisualHoverAlpha = 0.42f;
    [SerializeField, Min(0f)] private float teleportVisualHoverEmissionMultiplier = 1.35f;

    [Header("Controller Teleport")]
    [SerializeField] private bool useControllerRayTeleport = true;
    [SerializeField, Min(0.05f)] private float controllerTeleportAimRadius = 0.9f;
    [SerializeField, Min(0.5f)] private float controllerTeleportRayLength = 12f;
    [SerializeField] private bool preserveInitialRigHeightOnTeleport = true;

    private bool _menuActivated;
    private Material _runtimeTeleportVisualMaterial;
    private readonly List<TeleportFloorVisual> _teleportFloorVisuals = new List<TeleportFloorVisual>();
    private readonly List<TeleportHotspotVisual> _teleportHotspotVisuals = new List<TeleportHotspotVisual>();
    private float _initialRigRootY;
    private bool _hasInitialRigRootY;

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
        StoreInitialRigHeight();
        CreateTeleportFloorVisuals();
        ResolveTeleportHotspotVisuals();
    }

    private void Update()
    {
        UpdateControllerRayTeleport();

        if (_menuActivated || tvTeleportPoint == null || tvMenuRoot == null)
        {
            UpdateTeleportFloorVisualHover();
            return;
        }

        ResolveRigReferences();

        if (centerEyeAnchor == null)
        {
            UpdateTeleportFloorVisualHover();
            return;
        }

        Vector2 eyePosition = new Vector2(centerEyeAnchor.position.x, centerEyeAnchor.position.z);
        Vector2 targetPosition = new Vector2(tvTeleportPoint.position.x, tvTeleportPoint.position.z);

        if (Vector2.Distance(eyePosition, targetPosition) <= activationRadius)
        {
            ActivateTvMenu();
        }

        UpdateTeleportFloorVisualHover();
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

    private void MoveRigToTeleportTarget(Transform target)
    {
        if (cameraRig == null || target == null)
        {
            return;
        }

        Transform rigTransform = cameraRig.transform;
        Vector3 targetForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (targetForward.sqrMagnitude < 0.001f)
        {
            targetForward = Vector3.ProjectOnPlane(rigTransform.forward, Vector3.up).normalized;
        }
        if (targetForward.sqrMagnitude < 0.001f)
        {
            targetForward = Vector3.forward;
        }

        float targetY = preserveInitialRigHeightOnTeleport && _hasInitialRigRootY
            ? _initialRigRootY
            : rigTransform.position.y;

        rigTransform.SetPositionAndRotation(
            new Vector3(target.position.x, targetY, target.position.z),
            Quaternion.LookRotation(targetForward, Vector3.up));
    }

    private void StoreInitialRigHeight()
    {
        if (cameraRig == null)
        {
            return;
        }

        _initialRigRootY = cameraRig.transform.position.y;
        _hasInitialRigRootY = true;
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

    private void ResolveTeleportHotspotVisuals()
    {
        _teleportHotspotVisuals.Clear();
        AddTeleportHotspotVisual(tvTeleportPoint);
        AddTeleportHotspotVisual(trophiesTeleportPoint);
    }

    private void AddTeleportHotspotVisual(Transform teleportTarget)
    {
        if (teleportTarget == null)
        {
            return;
        }

        TeleportHotspotVisual visual = teleportTarget.GetComponent<TeleportHotspotVisual>();
        if (visual == null)
        {
            visual = teleportTarget.GetComponentInChildren<TeleportHotspotVisual>(true);
        }

        if (visual != null && !_teleportHotspotVisuals.Contains(visual))
        {
            _teleportHotspotVisuals.Add(visual);
        }
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

        var floorVisual = new TeleportFloorVisual(teleportTarget, visual.transform, renderer);
        _teleportFloorVisuals.Add(floorVisual);
        ApplyTeleportFloorVisualHover(floorVisual, !showTeleportVisualOnlyWhenAimed);
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

        Material resourceMaterial = Resources.Load<Material>("M_TeleportHotspotGTA");
        if (resourceMaterial != null)
        {
            _runtimeTeleportVisualMaterial = new Material(resourceMaterial)
            {
                name = "Runtime_TeleportHotspotGTA"
            };
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

    private void UpdateTeleportFloorVisualHover()
    {
        if (_teleportFloorVisuals.Count == 0)
        {
            return;
        }

        bool hasRightRay = TryGetControllerRay(OVRInput.Controller.RTouch, out Vector3 rightOrigin, out Vector3 rightDirection);
        bool hasLeftRay = TryGetControllerRay(OVRInput.Controller.LTouch, out Vector3 leftOrigin, out Vector3 leftDirection);

        for (int i = 0; i < _teleportFloorVisuals.Count; i++)
        {
            TeleportFloorVisual floorVisual = _teleportFloorVisuals[i];
            bool isAimed = false;

            if (floorVisual.VisualRoot != null)
            {
                Vector3 markerPosition = floorVisual.VisualRoot.position;
                isAimed = (hasRightRay && IsPointNearRay(markerPosition, rightOrigin, rightDirection))
                    || (hasLeftRay && IsPointNearRay(markerPosition, leftOrigin, leftDirection));
            }

            ApplyTeleportFloorVisualHover(floorVisual, isAimed);
        }
    }

    private void UpdateControllerRayTeleport()
    {
        if (!useControllerRayTeleport)
        {
            return;
        }

        ResolveTeleportReferences();
        if (_teleportHotspotVisuals.Count == 0)
        {
            ResolveTeleportHotspotVisuals();
        }

        bool hasRightRay = TryGetControllerRay(OVRInput.Controller.RTouch, out Vector3 rightOrigin, out Vector3 rightDirection);
        bool hasLeftRay = TryGetControllerRay(OVRInput.Controller.LTouch, out Vector3 leftOrigin, out Vector3 leftDirection);
        Transform hoveredTarget = null;

        if (hasRightRay)
        {
            hoveredTarget = FindTeleportTargetNearRay(rightOrigin, rightDirection);
        }

        if (hoveredTarget == null && hasLeftRay)
        {
            hoveredTarget = FindTeleportTargetNearRay(leftOrigin, leftDirection);
        }

        ApplyTeleportHotspotHover(hoveredTarget);

        if (hoveredTarget == null)
        {
            return;
        }

        bool rightPressed = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch)
            || OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger, OVRInput.Controller.RTouch);
        bool leftPressed = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch)
            || OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger, OVRInput.Controller.LTouch);

        if (rightPressed || leftPressed)
        {
            MoveRigToTeleportTarget(hoveredTarget);
            GameAudioEvents.RaiseMenuTeleported(hoveredTarget.position);
        }
    }

    private Transform FindTeleportTargetNearRay(Vector3 origin, Vector3 direction)
    {
        Transform bestTarget = null;
        float bestDistance = float.PositiveInfinity;

        CheckTeleportTargetNearRay(tvTeleportPoint, origin, direction, ref bestTarget, ref bestDistance);
        CheckTeleportTargetNearRay(trophiesTeleportPoint, origin, direction, ref bestTarget, ref bestDistance);

        return bestTarget;
    }

    private void CheckTeleportTargetNearRay(Transform target, Vector3 origin, Vector3 direction, ref Transform bestTarget, ref float bestDistance)
    {
        if (target == null)
        {
            return;
        }

        Vector3 normalizedDirection = direction.normalized;
        float alongRay = Vector3.Dot(target.position - origin, normalizedDirection);
        if (alongRay < 0f || alongRay > controllerTeleportRayLength)
        {
            return;
        }

        Vector3 closestPoint = origin + normalizedDirection * alongRay;
        float distance = Vector3.Distance(target.position, closestPoint);
        if (distance > controllerTeleportAimRadius || distance >= bestDistance)
        {
            return;
        }

        bestDistance = distance;
        bestTarget = target;
    }

    private void ApplyTeleportHotspotHover(Transform hoveredTarget)
    {
        for (int i = 0; i < _teleportHotspotVisuals.Count; i++)
        {
            TeleportHotspotVisual visual = _teleportHotspotVisuals[i];
            if (visual == null)
            {
                continue;
            }

            bool hover = hoveredTarget != null && visual.transform == hoveredTarget;
            visual.SetHover(hover);
        }
    }

    private bool TryGetControllerRay(OVRInput.Controller controller, out Vector3 origin, out Vector3 direction)
    {
        ResolveRigReferences();

        bool controllerConnected = (OVRInput.GetConnectedControllers() & controller) == controller;
        if (controllerConnected && cameraRig != null && cameraRig.trackingSpace != null)
        {
            Vector3 localPosition = OVRInput.GetLocalControllerPosition(controller);
            Quaternion localRotation = OVRInput.GetLocalControllerRotation(controller);
            origin = cameraRig.trackingSpace.TransformPoint(localPosition);
            direction = cameraRig.trackingSpace.rotation * (localRotation * Vector3.forward);
            return direction.sqrMagnitude > 0.0001f;
        }

        origin = Vector3.zero;
        direction = Vector3.forward;
        return false;
    }

    private bool IsPointNearRay(Vector3 point, Vector3 origin, Vector3 direction)
    {
        Vector3 normalizedDirection = direction.normalized;
        float alongRay = Vector3.Dot(point - origin, normalizedDirection);
        if (alongRay < 0f || alongRay > teleportAimRayLength)
        {
            return false;
        }

        Vector3 closestPoint = origin + normalizedDirection * alongRay;
        return Vector3.Distance(point, closestPoint) <= teleportAimRadius;
    }

    private void ApplyTeleportFloorVisualHover(TeleportFloorVisual floorVisual, bool hover)
    {
        if (floorVisual.Renderer == null)
        {
            return;
        }

        bool visible = hover || !showTeleportVisualOnlyWhenAimed || teleportVisualIdleAlpha > 0f;
        floorVisual.Renderer.enabled = visible;

        if (!visible)
        {
            return;
        }

        Material material = floorVisual.Renderer.material;
        float alpha = hover ? teleportVisualHoverAlpha : teleportVisualIdleAlpha;
        Color baseColor = new Color(1f, 0.18f, 0.28f, 1f);
        Color emissionColor = new Color(1f, 0.1f, 0.18f, 1f) * (hover ? teleportVisualHoverEmissionMultiplier : 1f);

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
            material.SetColor("_EmissionColor", emissionColor);
        }

        if (material.HasProperty("_Color"))
        {
            Color fallbackColor = baseColor;
            fallbackColor.a = alpha;
            material.SetColor("_Color", fallbackColor);
        }
    }

    private readonly struct TeleportFloorVisual
    {
        public TeleportFloorVisual(Transform target, Transform visualRoot, Renderer renderer)
        {
            Target = target;
            VisualRoot = visualRoot;
            Renderer = renderer;
        }

        public Transform Target { get; }
        public Transform VisualRoot { get; }
        public Renderer Renderer { get; }
    }
}
