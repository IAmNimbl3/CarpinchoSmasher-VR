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
    private float updateInterval = 0.5f;
    private float accumulatedDeltaTime = 0f;
    private int frames = 0;
    private float timeRemaining;

    private Vector3 posVel;

    private void Awake()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        // Snap to the start position so it doesn't fly in from the origin on the first frame.
        SnapToTarget();
    }

    private void Start()
    {
        timeRemaining = updateInterval;
    }

    private void Update()
    {
        timeRemaining -= Time.unscaledDeltaTime;
        accumulatedDeltaTime += Time.unscaledDeltaTime;
        frames++;

        if (timeRemaining <= 0.0f)
        {
            float currentFps = frames / accumulatedDeltaTime;
            label.text = Mathf.Round(currentFps).ToString();

            timeRemaining = updateInterval;
            accumulatedDeltaTime = 0f;
            frames = 0;
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
