using System.Collections;
using UnityEngine;

public class HammerSummoner : MonoBehaviour
{
    [Header("Meta rig")]
    [SerializeField] private OVRCameraRig cameraRig;

    [Header("Hammer")]
    [SerializeField] private GameObject hammerPrefab;

    [Header("Holster")]
    [SerializeField] private bool useHeadHeightForHolster = true;
    [SerializeField] private Vector3 holsterOffsetFromPlayer = new Vector3(0.12f, -0.72f, 0.02f);
    [SerializeField] private Vector3 holsterEulerAngles = new Vector3(90f, 0f, 0f);
    [SerializeField, Min(0f)] private float respawnDelayAfterThrow = 0f;

    private Transform _headAnchor;
    private ThrownHammer _holsteredHammer;
    private Coroutine _respawnRoutine;

    private void OnEnable()
    {
        ResolveRigReferences();
    }

    private void Start()
    {
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
        }
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

        Transform holsterReference = cameraRig.transform;
        Vector3 up = Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(holsterReference.forward, up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        Vector3 right = Vector3.Cross(up, forward).normalized;
        Vector3 origin = holsterReference.position;
        if (useHeadHeightForHolster && _headAnchor != null)
        {
            origin.y = _headAnchor.position.y;
        }

        Vector3 position = origin
            + right * holsterOffsetFromPlayer.x
            + up * holsterOffsetFromPlayer.y
            + forward * holsterOffsetFromPlayer.z;
        Quaternion rotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(holsterEulerAngles);

        _holsteredHammer.transform.SetPositionAndRotation(position, rotation);
    }
}
