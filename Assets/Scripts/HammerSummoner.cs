using UnityEngine;

public class HammerSummoner : MonoBehaviour
{
    [Header("Meta controller")]
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.RTouch;
    [SerializeField] private float gripPressThreshold = 0.55f;

    [Header("Hammer")]
    [SerializeField] private GameObject hammerPrefab;
    [SerializeField] private string gripAnchorName = "GripAnchor";
    [SerializeField] private Vector3 heldLocalEulerAngles = Vector3.zero;

    [Header("Holster")]
    [SerializeField] private bool useHeadHeightForHolster = true;
    [SerializeField] private Vector3 holsterOffsetFromPlayer = new Vector3(0.12f, -0.72f, 0.02f);
    [SerializeField] private Vector3 holsterEulerAngles = new Vector3(90f, 0f, 0f);
    [SerializeField, Min(0.05f)] private float pickupRadius = 0.5f;
    [SerializeField, Min(0f)] private float respawnDelay = 2f;

    [Header("Throw")]
    [SerializeField, Min(1f)] private float maxThrowSpeed = 15f;
    [SerializeField, Min(0f)] private float minThrowSpeed = 0.5f;

    private const int VelocityHistorySize = 8;

    private readonly Vector3[] _velocityHistory = new Vector3[VelocityHistorySize];
    private Transform _controllerAnchor;
    private Transform _headAnchor;
    private GameObject _availableHammer;
    private GameObject _heldHammer;
    private ThrownHammer _heldThrownHammer;
    private bool _wasGripPressed;
    private bool _hasLastControllerPosition;
    private int _velocityHistoryIndex;
    private float _nextRespawnTime = -1f;
    private Vector3 _lastControllerPosition;

    private void OnEnable()
    {
        ResolveRigReferences();
        ResetVelocityHistory();
        _wasGripPressed = false;
    }

    private void Start()
    {
        ResolveRigReferences();
        SpawnHolsteredHammer();
    }

    private void Update()
    {
        ResolveRigReferences();

        if (_controllerAnchor == null || _headAnchor == null || hammerPrefab == null)
        {
            return;
        }

        TrackControllerVelocity();
        UpdateHolsteredHammerPose();
        TryRespawnHolsteredHammer();

        bool gripPressed = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, controller) >= gripPressThreshold;
        if (gripPressed && !_wasGripPressed)
        {
            TryPickupHammer();
        }
        else if (!gripPressed && _wasGripPressed)
        {
            ThrowHeldHammer();
        }

        _wasGripPressed = gripPressed;
    }

    private void LateUpdate()
    {
        if (_heldHammer != null)
        {
            SnapHeldHammerToController(_heldHammer);
        }
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

        _headAnchor = cameraRig.centerEyeAnchor;
        _controllerAnchor = controller == OVRInput.Controller.LTouch
            ? cameraRig.leftControllerAnchor
            : cameraRig.rightControllerAnchor;

        if (_controllerAnchor == null)
        {
            _controllerAnchor = controller == OVRInput.Controller.LTouch
                ? cameraRig.leftHandAnchor
                : cameraRig.rightHandAnchor;
        }
    }

    private void TrackControllerVelocity()
    {
        Vector3 currentPosition = _controllerAnchor.position;
        if (_hasLastControllerPosition && Time.deltaTime > 0f)
        {
            _velocityHistory[_velocityHistoryIndex] = (currentPosition - _lastControllerPosition) / Time.deltaTime;
            _velocityHistoryIndex = (_velocityHistoryIndex + 1) % VelocityHistorySize;
        }

        _lastControllerPosition = currentPosition;
        _hasLastControllerPosition = true;
    }

    private void TryPickupHammer()
    {
        if (_heldHammer != null || _availableHammer == null)
        {
            return;
        }

        if (GetDistanceToHammer(_availableHammer) > pickupRadius)
        {
            return;
        }

        _heldHammer = _availableHammer;
        _availableHammer = null;
        _nextRespawnTime = Time.time + respawnDelay;
        _heldThrownHammer = _heldHammer.GetComponent<ThrownHammer>();

        if (_heldThrownHammer != null)
        {
            _heldThrownHammer.BeginHeld();
        }

        ResetVelocityHistory();
        SnapHeldHammerToController(_heldHammer);
    }

    private void ThrowHeldHammer()
    {
        if (_heldHammer == null)
        {
            return;
        }

        Vector3 throwVelocity = GetThrowVelocity();
        if (throwVelocity.magnitude < minThrowSpeed)
        {
            throwVelocity = Vector3.zero;
        }

        if (_heldThrownHammer != null)
        {
            _heldThrownHammer.Throw(throwVelocity);
        }
        else
        {
            _heldHammer.transform.SetParent(null, true);
        }

        _heldHammer = null;
        _heldThrownHammer = null;
    }

    private void SpawnHolsteredHammer()
    {
        if (hammerPrefab == null || _availableHammer != null || _heldHammer != null)
        {
            return;
        }

        _availableHammer = Instantiate(hammerPrefab);
        _availableHammer.name = "Holstered Hammer";
        EnsureGripAnchor(_availableHammer);

        ThrownHammer thrownHammer = _availableHammer.GetComponent<ThrownHammer>();
        if (thrownHammer != null)
        {
            thrownHammer.BeginHolstered();
        }

        UpdateHolsteredHammerPose();
    }

    private void TryRespawnHolsteredHammer()
    {
        if (_availableHammer != null || _heldHammer != null || _nextRespawnTime < 0f || Time.time < _nextRespawnTime)
        {
            return;
        }

        SpawnHolsteredHammer();
        _nextRespawnTime = -1f;
    }

    private void UpdateHolsteredHammerPose()
    {
        if (_availableHammer == null || _headAnchor == null)
        {
            return;
        }

        Vector3 up = Vector3.up;
        Transform holsterReference = cameraRig != null ? cameraRig.transform : transform;
        Vector3 forward = Vector3.ProjectOnPlane(holsterReference.forward, up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        Vector3 right = Vector3.Cross(up, forward).normalized;
        Vector3 origin = holsterReference.position;
        if (useHeadHeightForHolster)
        {
            origin.y = _headAnchor.position.y;
        }

        Vector3 position = origin
            + right * holsterOffsetFromPlayer.x
            + up * holsterOffsetFromPlayer.y
            + forward * holsterOffsetFromPlayer.z;
        Quaternion rotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(holsterEulerAngles);

        _availableHammer.transform.SetPositionAndRotation(position, rotation);
    }

    private void SnapHeldHammerToController(GameObject hammer)
    {
        Transform gripAnchor = EnsureGripAnchor(hammer);
        Quaternion targetRotation = _controllerAnchor.rotation * Quaternion.Euler(heldLocalEulerAngles);
        Quaternion rootRotation = targetRotation * Quaternion.Inverse(gripAnchor.localRotation);
        Vector3 scaledGripOffset = Vector3.Scale(hammer.transform.localScale, gripAnchor.localPosition);
        Vector3 rootPosition = _controllerAnchor.position - (rootRotation * scaledGripOffset);

        hammer.transform.SetParent(null, true);
        hammer.transform.SetPositionAndRotation(rootPosition, rootRotation);
        hammer.transform.SetParent(_controllerAnchor, true);
    }

    private Transform EnsureGripAnchor(GameObject hammer)
    {
        Transform gripAnchor = hammer.transform.Find(gripAnchorName);
        if (gripAnchor != null)
        {
            return gripAnchor;
        }

        GameObject anchorObject = new GameObject(gripAnchorName);
        gripAnchor = anchorObject.transform;
        gripAnchor.SetParent(hammer.transform, false);
        gripAnchor.localPosition = FindHandleLocalCenter(hammer);
        gripAnchor.localRotation = Quaternion.identity;
        gripAnchor.localScale = Vector3.one;
        return gripAnchor;
    }

    private Vector3 FindHandleLocalCenter(GameObject hammer)
    {
        Collider[] colliders = hammer.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            return Vector3.zero;
        }

        Collider bestCollider = colliders[0];
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider current = colliders[i];
            Vector3 localSize = current.transform == hammer.transform ? current.bounds.size : hammer.transform.InverseTransformVector(current.bounds.size);
            float score = Mathf.Max(localSize.x, localSize.y, localSize.z) - Mathf.Min(localSize.x, localSize.y, localSize.z);
            if (score > bestScore)
            {
                bestCollider = current;
                bestScore = score;
            }
        }

        return hammer.transform.InverseTransformPoint(bestCollider.bounds.center);
    }

    private float GetDistanceToHammer(GameObject hammer)
    {
        Vector3 controllerPosition = _controllerAnchor.position;
        float bestSqrDistance = (hammer.transform.position - controllerPosition).sqrMagnitude;
        Collider[] colliders = hammer.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider hammerCollider = colliders[i];
            if (hammerCollider == null)
            {
                continue;
            }

            Vector3 closestPoint = hammerCollider.bounds.ClosestPoint(controllerPosition);
            float sqrDistance = (closestPoint - controllerPosition).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
            }
        }

        return Mathf.Sqrt(bestSqrDistance);
    }

    private Vector3 GetThrowVelocity()
    {
        Vector3 bestVelocity = Vector3.zero;
        float bestSqrMagnitude = 0f;

        for (int i = 0; i < VelocityHistorySize; i++)
        {
            float sqrMagnitude = _velocityHistory[i].sqrMagnitude;
            if (sqrMagnitude > bestSqrMagnitude)
            {
                bestSqrMagnitude = sqrMagnitude;
                bestVelocity = _velocityHistory[i];
            }
        }

        return Vector3.ClampMagnitude(bestVelocity, maxThrowSpeed);
    }

    private void ResetVelocityHistory()
    {
        _hasLastControllerPosition = false;
        _velocityHistoryIndex = 0;

        for (int i = 0; i < VelocityHistorySize; i++)
        {
            _velocityHistory[i] = Vector3.zero;
        }
    }
}
