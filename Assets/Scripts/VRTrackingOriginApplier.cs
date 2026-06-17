using UnityEngine;

public class VRTrackingOriginApplier : MonoBehaviour
{
    [Header("Configs")]
    [SerializeField] private VRPlayModeConfig seatedConfig;
    [SerializeField] private VRPlayModeConfig standingConfig;

    [Header("Rig")]
    [SerializeField] private OVRManager ovrManager;
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private Transform trackingSpace;
    [SerializeField] private Transform centerEyeAnchor;
    [SerializeField] private bool applyTargetEyeHeight = true;
    [SerializeField] private bool keepTargetEyeHeightUpdated = true;

    private float _baseTrackingSpaceX;
    private float _baseTrackingSpaceZ;
    private bool _hasBaseTrackingSpacePosition;

    private void Awake()
    {
        Apply();
    }

    private void Start()
    {
        Apply();
    }

    private void LateUpdate()
    {
        if (keepTargetEyeHeightUpdated)
        {
            ApplyTargetEyeHeight();
        }
    }

    public void Apply()
    {
        VRPlayModeSettings.RegisterConfigs(seatedConfig, standingConfig);

        if (ovrManager == null)
        {
            ovrManager = FindAnyObjectByType<OVRManager>();
        }

        if (ovrManager == null)
        {
            return;
        }

        VRPlayModeConfig activeConfig = VRPlayModeSettings.ActiveConfig;
        ovrManager.trackingOriginType = activeConfig != null
            ? activeConfig.TrackingOrigin
            : (VRPlayModeSettings.SeatedMode ? OVRManager.TrackingOrigin.EyeLevel : OVRManager.TrackingOrigin.FloorLevel);

        ApplyTargetEyeHeight();
    }

    private void ResolveRigReferences()
    {
        if (cameraRig == null)
        {
            cameraRig = FindAnyObjectByType<OVRCameraRig>();
        }

        if (cameraRig == null)
        {
            return;
        }

        if (trackingSpace == null)
        {
            trackingSpace = cameraRig.trackingSpace;
        }

        if (centerEyeAnchor == null)
        {
            centerEyeAnchor = cameraRig.centerEyeAnchor;
        }
    }

    private void ApplyTargetEyeHeight()
    {
        if (!applyTargetEyeHeight)
        {
            return;
        }

        ResolveRigReferences();

        if (trackingSpace == null || centerEyeAnchor == null)
        {
            return;
        }

        if (!_hasBaseTrackingSpacePosition)
        {
            Vector3 currentPosition = trackingSpace.localPosition;
            _baseTrackingSpaceX = currentPosition.x;
            _baseTrackingSpaceZ = currentPosition.z;
            _hasBaseTrackingSpacePosition = true;
        }

        VRPlayModeConfig activeConfig = VRPlayModeSettings.ActiveConfig;
        if (activeConfig == null)
        {
            return;
        }

        Vector3 trackingSpacePosition = trackingSpace.localPosition;
        trackingSpacePosition.x = _baseTrackingSpaceX;
        trackingSpacePosition.y = activeConfig.TargetEyeHeight - centerEyeAnchor.localPosition.y;
        trackingSpacePosition.z = _baseTrackingSpaceZ;
        trackingSpace.localPosition = trackingSpacePosition;
    }
}
