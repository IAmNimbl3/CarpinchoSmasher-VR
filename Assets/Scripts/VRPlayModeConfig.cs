using UnityEngine;

[CreateAssetMenu(fileName = "VR_PlayMode_Config", menuName = "Carpincho Smasher/VR Play Mode Config")]
public class VRPlayModeConfig : ScriptableObject
{
    [SerializeField] private string displayName = "Sentado";
    [SerializeField] private OVRManager.TrackingOrigin trackingOrigin = OVRManager.TrackingOrigin.EyeLevel;
    [SerializeField, Min(0f)] private float targetEyeHeight = 1.05f;
    [SerializeField] private Vector3 holsterOffsetFromPlayer = new Vector3(0.16f, -0.42f, 0.08f);

    public string DisplayName => displayName;
    public OVRManager.TrackingOrigin TrackingOrigin => trackingOrigin;
    public float TargetEyeHeight => targetEyeHeight;
    public Vector3 HolsterOffsetFromPlayer => holsterOffsetFromPlayer;
}
