using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Always-visible FPS readout for VR. Rendered as a small world-space label in the
/// lower periphery that follows the player's gaze with damping (smooth lag) instead of
/// being rigidly head-locked, which keeps it readable without inducing sim-sickness.
/// Screen-space overlay UI is intentionally NOT used because it does not render in HMDs.
/// </summary>
public class VrFpsHud : MonoBehaviour
{
    [Header("Follow (comfort)")]
    [Tooltip("Player head (CenterEyeAnchor). Falls back to Camera.main if empty.")]
    [SerializeField] private Transform head;
    [SerializeField] private float distance = 1.2f;
    [Tooltip("Metres below the gaze line, so it sits in the lower periphery.")]
    [SerializeField] private float verticalOffset = -0.32f;
    [Tooltip("Higher = smoother/laggier follow (gentler on the eyes).")]
    [SerializeField] private float positionSmoothTime = 0.12f;
    [SerializeField] private float rotationSmoothSpeed = 8f;

    [Header("Readout")]
    [SerializeField] private Text label;
    [SerializeField] private Color textColor = new Color(0.4f, 1f, 0.4f); // always green
    [Tooltip("0..1 — lower is smoother/steadier, higher reacts faster.")]
    [SerializeField] private float sampleSmoothing = 0.1f;
    [SerializeField] private float refreshInterval = 0.25f;
    [Tooltip("Used only to seed the initial reading.")]
    [SerializeField] private float targetFps = 72f;

    private float smoothedFps;
    private float refreshTimer;
    private Vector3 posVel;

    private void Awake()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        smoothedFps = targetFps;
        if (label != null) label.color = textColor;
        // Snap to the start position so it doesn't fly in from the origin on the first frame.
        SnapToTarget();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f)
        {
            float fps = 1f / dt;
            smoothedFps = Mathf.Lerp(smoothedFps, fps, sampleSmoothing);
        }

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f && label != null)
        {
            refreshTimer = refreshInterval;
            label.text = Mathf.RoundToInt(smoothedFps) + " FPS";
        }
    }

    private void LateUpdate()
    {
        Transform h = ResolveHead();
        if (h == null) return;

        Vector3 target = h.position + h.forward * distance + h.up * verticalOffset;
        transform.position = Vector3.SmoothDamp(transform.position, target, ref posVel,
                                                positionSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        Vector3 lookDir = transform.position - h.position;
        if (lookDir.sqrMagnitude > 1e-5f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
            float t = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.unscaledDeltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
        }
    }

    private void SnapToTarget()
    {
        Transform h = ResolveHead();
        if (h == null) return;
        transform.position = h.position + h.forward * distance + h.up * verticalOffset;
        transform.rotation = Quaternion.LookRotation(transform.position - h.position, Vector3.up);
    }

    private Transform ResolveHead()
    {
        if (head != null) return head;
        return Camera.main != null ? Camera.main.transform : null;
    }
}
