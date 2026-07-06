using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Plays an intro cinematic on a floating screen, then loads the game scene.
/// The player can either watch the whole clip or skip it (controller button and/or a
/// world-space UI button). Either way, when it ends the next scene is loaded.
/// </summary>
public class CinematicIntroController : MonoBehaviour
{
    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;
    [Tooltip("Your imported .mp4 (as a VideoClip). Leave empty to use the VideoPlayer's own clip/url.")]
    [SerializeField] private VideoClip clip;

    [Header("Flow")]
    [Tooltip("Scene loaded when the cinematic finishes or is skipped. Overridden by the menu at runtime.")]
    [SerializeField] private string nextSceneName = "SceneMapaVR";

    [Header("Skip input")]
    [Tooltip("Ignore skip input for the first moments so the click that started the game can't skip instantly.")]
    [SerializeField] private float skipLockSeconds = 0.5f;
    [SerializeField] private OVRInput.Button skipButton = OVRInput.Button.Two;    // B / Y (fallback)
    [SerializeField] private OVRInput.Button skipButtonAlt = OVRInput.Button.One; // A / X (fallback)
    [Tooltip("Optional world-space Skip button (also driven by the laser ray below).")]
    [SerializeField] private Button skipUIButton;

    [Header("Laser skip (aim the ray at the Skip button and pull the trigger)")]
    [Tooltip("Collider on the on-screen Skip button that the ray must hit.")]
    [SerializeField] private Collider skipButtonCollider;
    [Tooltip("Pointer pose of each controller's ray interactor (origin + forward = the laser).")]
    [SerializeField] private Transform rayOriginRight;
    [SerializeField] private Transform rayOriginLeft;
    [SerializeField] private float rayMaxDistance = 25f;
    [SerializeField] private OVRInput.Button triggerButton = OVRInput.Button.PrimaryIndexTrigger;

    [Header("Screen placement (auto-position in front of the player on load)")]
    [Tooltip("Root of the floating screen (the world-space canvas). Positioned in front of the head on load.")]
    [SerializeField] private Transform screenRoot;
    [Tooltip("Player head (CenterEyeAnchor). Falls back to Camera.main if empty.")]
    [SerializeField] private Transform head;
    [SerializeField] private bool placeInFrontOnStart = true;
    [SerializeField] private float screenDistance = 3f;
    [Tooltip("Height offset from the head (0 = at eye level).")]
    [SerializeField] private float verticalOffset = 0f;

    /// <summary>Set by the menu before loading this scene, so we know where to go next.</summary>
    public static string PendingGameScene;

    private bool loading;
    private bool placed;
    private float startTime;

    private void Awake()
    {
        if (videoPlayer == null) videoPlayer = GetComponentInChildren<VideoPlayer>(true);
        if (!string.IsNullOrEmpty(PendingGameScene)) nextSceneName = PendingGameScene;
    }

    private void OnEnable()
    {
        if (skipUIButton != null) skipUIButton.onClick.AddListener(Skip);
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoFinished;
            videoPlayer.errorReceived += OnVideoError;
        }
    }

    private void OnDisable()
    {
        if (skipUIButton != null) skipUIButton.onClick.RemoveListener(Skip);
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.errorReceived -= OnVideoError;
        }
    }

    private void Start()
    {
        startTime = Time.unscaledTime;

        if (videoPlayer == null)
        {
            Debug.LogWarning("[CinematicIntro] No VideoPlayer assigned — waiting for skip only.");
            return;
        }

        videoPlayer.isLooping = false;
        videoPlayer.playOnAwake = false;
        if (clip != null) videoPlayer.clip = clip;

        if (videoPlayer.clip == null && string.IsNullOrEmpty(videoPlayer.url))
        {
            Debug.LogWarning("[CinematicIntro] No video assigned — waiting for skip.");
            return;
        }

        // Prepare then play to avoid a hitch on the first frame.
        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.Prepare();
    }

    private void OnPrepared(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnPrepared;
        vp.Play();
    }

    // Place once on the first LateUpdate, after the rig has applied the live head pose.
    private void LateUpdate()
    {
        if (placed || !placeInFrontOnStart) return;
        PlaceScreenInFront();
        placed = true;
    }

    /// <summary>Positions the screen (and its skip UI) in front of the player's head.</summary>
    public void PlaceScreenInFront()
    {
        Transform h = head != null ? head : (Camera.main != null ? Camera.main.transform : null);
        if (h == null || screenRoot == null) return;

        Vector3 fwd = h.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();

        screenRoot.position = h.position + fwd * screenDistance + Vector3.up * verticalOffset;
        screenRoot.rotation = Quaternion.LookRotation(fwd, Vector3.up);
    }

    private void OnVideoFinished(VideoPlayer vp) => LoadNext();

    private void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogError("[CinematicIntro] Video error: " + message + " — advancing to next scene.");
        LoadNext();
    }

    private void Update()
    {
        if (loading) return;
        if (Time.unscaledTime - startTime < skipLockSeconds) return;

        // Aim the laser at the Skip button and pull the trigger.
        if (TriggerHitsSkip(rayOriginRight, OVRInput.Controller.RTouch) ||
            TriggerHitsSkip(rayOriginLeft, OVRInput.Controller.LTouch))
        {
            Skip();
            return;
        }

        // Fallback: face buttons.
        if (OVRInput.GetDown(skipButton) || OVRInput.GetDown(skipButtonAlt))
        {
            Skip();
        }
    }

    private bool TriggerHitsSkip(Transform origin, OVRInput.Controller controller)
    {
        if (origin == null || skipButtonCollider == null) return false;
        if (!OVRInput.GetDown(triggerButton, controller)) return false;

        if (Physics.Raycast(origin.position, origin.forward, out RaycastHit hit,
                             rayMaxDistance, ~0, QueryTriggerInteraction.Collide))
        {
            return hit.collider == skipButtonCollider;
        }
        return false;
    }

    /// <summary>Public so it can be hooked to a UI Button or called from elsewhere.</summary>
    public void Skip()
    {
        if (loading) return;
        Debug.Log("[CinematicIntro] Cinematic skipped.");
        LoadNext();
    }

    private void LoadNext()
    {
        if (loading) return;
        loading = true;

        if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();

        PendingGameScene = null; // consume it

        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogError("[CinematicIntro] Scene '" + nextSceneName +
                           "' is not in Build Settings — cannot load.");
        }
    }
}
