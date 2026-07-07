using System.Collections;
using UnityEngine;

public class HammerSummoner : MonoBehaviour
{
    [Header("Configs")]
    [SerializeField] private VRPlayModeConfig seatedConfig;
    [SerializeField] private VRPlayModeConfig standingConfig;

    [Header("Meta rig")]
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private Transform holsterReference;
    [SerializeField] private Transform holsterOrientationReference;

    [Header("Hammer")]
    [SerializeField] private GameObject hammerPrefab;

    [Header("Holster")]
    [SerializeField] private bool useHeadHeightForHolster = false;
    [Tooltip("Holster follows the player's view: it orbits with the head's yaw so it rotates with you when you turn.")]
    [SerializeField] private bool followViewYaw = true;
    [SerializeField] private Vector3 holsterOffsetFromPlayer = new Vector3(0.12f, -0.72f, 0.02f);
    [SerializeField] private Vector3 seatedHolsterOffsetFromPlayer = new Vector3(0.16f, -0.42f, 0.08f);
    [SerializeField] private Vector3 holsterEulerAngles = new Vector3(90f, 0f, 0f);
    [SerializeField, Min(0f)] private float respawnDelayAfterThrow = 0f;

    private Transform _headAnchor;
    private ThrownHammer _holsteredHammer;
    private Coroutine _respawnRoutine;

    private void OnEnable()
    {
        RegisterPlayModeConfigs();
        ResolveRigReferences();
    }

    private void Start()
    {
        RegisterPlayModeConfigs();
        ResolveRigReferences();
        SpawnHolsteredHammer();
    }

    private void Update()
    {
        ResolveRigReferences();
        UpdateHolsteredHammerPose();
    }

    private void ResolveRigReferences()
    {
        if (cameraRig == null)
        {
            cameraRig = FindAnyObjectByType<OVRCameraRig>();
        }

        if (cameraRig != null)
        {
            _headAnchor = cameraRig.centerEyeAnchor;

            if (holsterReference == null)
            {
                holsterReference = FindHolsterReference(cameraRig.transform);
            }

            if (holsterOrientationReference == null)
            {
                holsterOrientationReference = cameraRig.transform;
            }
        }
    }

    private Transform FindHolsterReference(Transform rigRoot)
    {
        CharacterController characterController = rigRoot.GetComponentInChildren<CharacterController>(true);
        if (characterController != null)
        {
            return characterController.transform;
        }

        return rigRoot;
    }

    private void RegisterPlayModeConfigs()
    {
        VRPlayModeSettings.RegisterConfigs(seatedConfig, standingConfig);
    }

    private void SpawnHolsteredHammer()
    {
        if (hammerPrefab == null || _holsteredHammer != null)
        {
            return;
        }

        GameObject hammerObject = Instantiate(hammerPrefab);
        hammerObject.name = "Holstered Hammer";

        _holsteredHammer = hammerObject.GetComponent<ThrownHammer>();
        if (_holsteredHammer == null)
        {
            _holsteredHammer = hammerObject.AddComponent<ThrownHammer>();
        }

        _holsteredHammer.Grabbed += HandleHammerGrabbed;
        _holsteredHammer.Released += HandleHammerReleased;
        _holsteredHammer.BeginHolstered();

        UpdateHolsteredHammerPose();
    }

    [ContextMenu("Spawn Test Hammer")]
    private void SpawnTestHammer()
    {
        if (hammerPrefab == null)
        {
            Debug.LogWarning("[HammerSummoner] Cannot spawn test hammer: hammerPrefab is not assigned.", this);
            return;
        }

        // Spawn through the real in-game path so the test hammer is holstered on the player exactly as it would be at runtime.
        ResolveRigReferences();
        SpawnHolsteredHammer();

        if (_holsteredHammer != null)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(_holsteredHammer.gameObject, "Spawn Test Hammer");
            UnityEditor.Selection.activeGameObject = _holsteredHammer.gameObject;
#endif
            Debug.Log("[HammerSummoner] Spawned holstered test hammer.", _holsteredHammer.gameObject);
        }
        else
        {
            Debug.Log("[HammerSummoner] A hammer is already holstered; nothing spawned.", this);
        }
    }

    private void HandleHammerGrabbed(ThrownHammer hammer)
    {
        if (hammer != _holsteredHammer)
        {
            return;
        }

        _holsteredHammer = null;
    }

    private void HandleHammerReleased(ThrownHammer hammer)
    {
        hammer.Grabbed -= HandleHammerGrabbed;
        hammer.Released -= HandleHammerReleased;

        if (_respawnRoutine != null)
        {
            StopCoroutine(_respawnRoutine);
        }

        _respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        if (respawnDelayAfterThrow > 0f)
        {
            yield return new WaitForSeconds(respawnDelayAfterThrow);
        }

        _respawnRoutine = null;
        SpawnHolsteredHammer();
    }

    private void UpdateHolsteredHammerPose()
    {
        if (_holsteredHammer == null || cameraRig == null)
        {
            return;
        }

        Vector3 up = Vector3.up;

        // Height/position pivot reference (defaults to the rig root) — kept stable so vertical placement doesn't change.
        Transform orientationReference = holsterOrientationReference != null ? holsterOrientationReference : holsterReference;

        // Facing reference: follow the player's view (head yaw) so the holster rotates with you when you turn.
        Transform yawReference = (followViewYaw && _headAnchor != null) ? _headAnchor : orientationReference;
        Vector3 forward = Vector3.ProjectOnPlane(yawReference.forward, up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        Vector3 right = Vector3.Cross(up, forward).normalized;
        Vector3 origin = holsterReference.position;
        origin.y = orientationReference.position.y;
        if (useHeadHeightForHolster && _headAnchor != null)
        {
            origin.y = _headAnchor.position.y;
        }

        VRPlayModeConfig activeConfig = VRPlayModeSettings.ActiveConfig;
        Vector3 activeHolsterOffset = activeConfig != null
            ? activeConfig.HolsterOffsetFromPlayer
            : (VRPlayModeSettings.SeatedMode ? seatedHolsterOffsetFromPlayer : holsterOffsetFromPlayer);

        Vector3 position = origin
            + right * activeHolsterOffset.x
            + up * activeHolsterOffset.y
            + forward * activeHolsterOffset.z;
        Quaternion rotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(holsterEulerAngles);

        _holsteredHammer.transform.SetPositionAndRotation(position, rotation);
    }
}
