using System;
using System.Collections;
using Oculus.Interaction;
using UnityEngine;

public class ThrownHammer : MonoBehaviour
{
    private enum HammerState
    {
        Holstered,
        Held,
        Launched
    }

    [Header("Lifetime")]
    [SerializeField, Min(0.5f)] private float launchedLifetime = 3f;
    [SerializeField, Min(0f)] private float handTransferGracePeriod = 1.4f;

    [Header("Physics")]
    [SerializeField] private bool useGravityWhenLaunched = true;
    [SerializeField, Min(0f)] private float launchedBounciness = 0.45f;
    [SerializeField, Min(0f)] private float launchedDynamicFriction = 0.2f;
    [SerializeField, Min(0f)] private float launchedStaticFriction = 0.2f;
    [SerializeField, Min(0f)] private float minLaunchSpeed = 1.5f;
    [SerializeField, Min(0f)] private float maxLaunchSpeed = 12f;
    [SerializeField, Min(0f)] private float maxAngularSpeed = 25f;

    [Header("Grab")]
    [SerializeField] private Transform gripAnchor;
    [SerializeField] private Collider[] nonGrabbableColliders;
    [SerializeField] private bool disableNonGrabbableCollidersWhileHolstered = true;

    [Header("Holstered Assist")]
    [SerializeField] private bool snapGripToNearestControllerOnGrab = true;
    [SerializeField, Min(0f)] private float snapActivationDistance = 0.16f;
    [SerializeField, Min(0f)] private float holsteredGripColliderPadding = 0.07f;
    [SerializeField] private bool showGripAreaHighlight = true;
    [SerializeField, Min(0f)] private float highlightActivationDistance = 0.06f;
    [SerializeField] private MeshOutlineHighlighter gripHighlighter;

    private Rigidbody _rigidbody;
    private Collider[] _colliders;
    private bool[] _originalTriggerStates;
    private Vector3[] _originalBoxColliderSizes;
    private Grabbable _grabbable;
    private HammerDamageDealer _damageDealer;
    private PhysicsMaterial _launchedMaterial;
    private HammerState _state;
    private float _launchedTimer;
    private int _launchVelocityTuneFrames;
    private bool _wasSelected;
    private OVRCameraRig _cameraRig;
    private Transform _leftControllerAnchor;
    private Transform _rightControllerAnchor;
    private Transform _highlightedController;
    private bool _gripHighlightsVisible;
    private bool _holsteredGripAreaInflated;
    private bool _releaseNotified;
    private Coroutine _pendingReleaseRoutine;

    public event Action<ThrownHammer> Grabbed;
    public event Action<ThrownHammer> Released;

    private void Awake()
    {
        CacheReferences();
        ConfigureGrabbable();
    }

    private void OnEnable()
    {
        CacheReferences();

        if (_grabbable != null)
        {
            _grabbable.WhenPointerEventRaised += HandlePointerEventRaised;
        }
    }

    private void OnDisable()
    {
        if (_grabbable != null)
        {
            _grabbable.WhenPointerEventRaised -= HandlePointerEventRaised;
        }

        CancelPendingRelease();
    }

    private void Update()
    {
        UpdateGripAreaHighlight();

        if (_state != HammerState.Launched)
        {
            return;
        }

        _launchedTimer -= Time.deltaTime;
        if (_launchedTimer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (_state != HammerState.Launched || _launchVelocityTuneFrames <= 0)
        {
            return;
        }

        _launchVelocityTuneFrames--;
        TuneLaunchVelocity();
    }

    public void BeginHolstered()
    {
        CacheReferences();

        _state = HammerState.Holstered;
        CancelPendingRelease();
        _releaseNotified = false;
        _launchedTimer = 0f;
        _launchVelocityTuneFrames = 0;
        _wasSelected = false;
        _highlightedController = null;

        ConfigureRigidbody(isKinematic: true, useGravity: false);
        SetColliders(enabled: true, forceTrigger: true);
        SetNonGrabbableCollidersEnabled(false);
        SetHolsteredGripAreaInflated(true);

        if (_damageDealer != null)
        {
            _damageDealer.SetDamageEnabled(false);
            _damageDealer.ResetHitCache();
        }
    }

    private void BeginHeld()
    {
        if (_state == HammerState.Held)
        {
            CancelPendingRelease();
            return;
        }

        if (_state == HammerState.Holstered && snapGripToNearestControllerOnGrab)
        {
            SnapGripAnchorToController(_highlightedController);
        }

        _state = HammerState.Held;
        CancelPendingRelease();
        _releaseNotified = false;
        _highlightedController = null;
        _launchedTimer = 0f;
        _launchVelocityTuneFrames = 0;
        SetGripHighlightsVisible(false);

        ConfigureRigidbody(isKinematic: true, useGravity: false);
        SetHolsteredGripAreaInflated(false);
        SetColliders(enabled: true, forceTrigger: true);
        SetNonGrabbableCollidersEnabled(true);

        if (_damageDealer != null)
        {
            _damageDealer.SetDamageEnabled(true);
            _damageDealer.ResetHitCache();
        }

        Grabbed?.Invoke(this);
        GameAudioEvents.RaiseHammerGrabbed(transform.position);
    }

    private void BeginLaunched(bool notifyReleased)
    {
        if (_state == HammerState.Launched)
        {
            return;
        }

        _state = HammerState.Launched;
        CancelPendingRelease();
        _launchedTimer = launchedLifetime;
        _launchVelocityTuneFrames = 3;
        SetGripHighlightsVisible(false);

        ConfigureRigidbody(isKinematic: false, useGravity: useGravityWhenLaunched);
        SetHolsteredGripAreaInflated(false);
        SetColliders(enabled: true, forceTrigger: false);
        SetNonGrabbableCollidersEnabled(true);
        ApplyLaunchedPhysicsMaterial();

        if (_damageDealer != null)
        {
            _damageDealer.SetDamageEnabled(true);
            _damageDealer.ResetHitCache();
        }

        if (notifyReleased)
        {
            NotifyReleased();
        }
    }

    private void HandlePointerEventRaised(PointerEvent evt)
    {
        bool isSelected = _grabbable != null && _grabbable.SelectingPointsCount > 0;

        if (isSelected && !_wasSelected)
        {
            BeginHeld();
        }
        else if (!isSelected && _wasSelected)
        {
            StartReleaseValidation();
        }

        _wasSelected = isSelected;
    }

    private void StartReleaseValidation()
    {
        CancelPendingRelease();
        BeginLaunched(notifyReleased: false);

        if (handTransferGracePeriod <= 0f)
        {
            NotifyReleased();
            return;
        }

        _pendingReleaseRoutine = StartCoroutine(ValidateReleaseAfterGracePeriod());
    }

    private IEnumerator ValidateReleaseAfterGracePeriod()
    {
        yield return new WaitForSeconds(handTransferGracePeriod);
        _pendingReleaseRoutine = null;

        if ((_grabbable == null || _grabbable.SelectingPointsCount == 0)
            && _state == HammerState.Launched)
        {
            NotifyReleased();
        }
    }

    private void NotifyReleased()
    {
        if (_releaseNotified)
        {
            return;
        }

        _releaseNotified = true;
        Released?.Invoke(this);
    }

    private void CancelPendingRelease()
    {
        if (_pendingReleaseRoutine == null)
        {
            return;
        }

        StopCoroutine(_pendingReleaseRoutine);
        _pendingReleaseRoutine = null;
    }

    private void CacheReferences()
    {
        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        if (_grabbable == null)
        {
            _grabbable = GetComponent<Grabbable>();
        }

        if (gripHighlighter == null)
        {
            gripHighlighter = GetComponent<MeshOutlineHighlighter>();
        }

        if (_damageDealer == null)
        {
            _damageDealer = GetComponent<HammerDamageDealer>();
        }

        if (gripAnchor == null)
        {
            Transform foundGripAnchor = transform.Find("GripAnchor");
            if (foundGripAnchor != null)
            {
                gripAnchor = foundGripAnchor;
            }
        }

        if (_colliders == null || _colliders.Length == 0)
        {
            _colliders = GetComponentsInChildren<Collider>(true);
            _originalTriggerStates = new bool[_colliders.Length];
            _originalBoxColliderSizes = new Vector3[_colliders.Length];

            for (int i = 0; i < _colliders.Length; i++)
            {
                _originalTriggerStates[i] = _colliders[i] != null && _colliders[i].isTrigger;
                if (_colliders[i] is BoxCollider boxCollider)
                {
                    _originalBoxColliderSizes[i] = boxCollider.size;
                }
            }
        }

        ResolveControllerAnchors();
        ConfigureGripHighlight();
    }

    private void ConfigureGrabbable()
    {
        if (_grabbable == null)
        {
            return;
        }

        _grabbable.MaxGrabPoints = 1;
        _grabbable.TransferOnSecondSelection = true;
        _grabbable.ForceKinematicDisabled = true;
        _grabbable.InjectOptionalTargetTransform(transform);
        _grabbable.InjectOptionalRigidbody(_rigidbody);
        _grabbable.InjectOptionalKinematicWhileSelected(true);
        _grabbable.InjectOptionalThrowWhenUnselected(true);
    }

    private void ConfigureRigidbody(bool isKinematic, bool useGravity)
    {
        if (_rigidbody == null)
        {
            return;
        }

        _rigidbody.isKinematic = isKinematic;
        _rigidbody.useGravity = useGravity;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.collisionDetectionMode = isKinematic
            ? CollisionDetectionMode.ContinuousSpeculative
            : CollisionDetectionMode.ContinuousDynamic;

        if (isKinematic)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void TuneLaunchVelocity()
    {
        if (_rigidbody == null)
        {
            return;
        }

        Vector3 velocity = _rigidbody.linearVelocity;
        float speed = velocity.magnitude;

        if (maxLaunchSpeed > 0f && speed > maxLaunchSpeed)
        {
            _rigidbody.linearVelocity = velocity.normalized * maxLaunchSpeed;
        }
        else if (minLaunchSpeed > 0f && speed > 0.05f && speed < minLaunchSpeed)
        {
            _rigidbody.linearVelocity = velocity.normalized * minLaunchSpeed;
        }

        if (maxAngularSpeed > 0f)
        {
            _rigidbody.maxAngularVelocity = maxAngularSpeed;
            Vector3 angularVelocity = _rigidbody.angularVelocity;
            if (angularVelocity.magnitude > maxAngularSpeed)
            {
                _rigidbody.angularVelocity = angularVelocity.normalized * maxAngularSpeed;
            }
        }
    }

    private void ApplyLaunchedPhysicsMaterial()
    {
        if (_colliders == null)
        {
            return;
        }

        if (_launchedMaterial == null)
        {
            _launchedMaterial = new PhysicsMaterial("Runtime Hammer Launch Material")
            {
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
        }

        _launchedMaterial.bounciness = launchedBounciness;
        _launchedMaterial.dynamicFriction = launchedDynamicFriction;
        _launchedMaterial.staticFriction = launchedStaticFriction;

        foreach (Collider hammerCollider in _colliders)
        {
            if (hammerCollider != null)
            {
                hammerCollider.sharedMaterial = _launchedMaterial;
            }
        }
    }

    private void SetColliders(bool enabled, bool forceTrigger)
    {
        if (_colliders == null)
        {
            return;
        }

        for (int i = 0; i < _colliders.Length; i++)
        {
            Collider hammerCollider = _colliders[i];
            if (hammerCollider == null)
            {
                continue;
            }

            hammerCollider.enabled = enabled;
            hammerCollider.isTrigger = forceTrigger || (_originalTriggerStates != null && _originalTriggerStates[i]);
        }
    }

    private void SetNonGrabbableCollidersEnabled(bool enabled)
    {
        if (!disableNonGrabbableCollidersWhileHolstered || nonGrabbableColliders == null)
        {
            return;
        }

        foreach (Collider nonGrabbableCollider in nonGrabbableColliders)
        {
            if (nonGrabbableCollider != null)
            {
                nonGrabbableCollider.enabled = enabled;
            }
        }
    }

    private void SetHolsteredGripAreaInflated(bool inflated)
    {
        if (_holsteredGripAreaInflated == inflated || _colliders == null || _originalBoxColliderSizes == null)
        {
            return;
        }

        _holsteredGripAreaInflated = inflated;

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] is not BoxCollider boxCollider || IsNonGrabbableCollider(boxCollider))
            {
                continue;
            }

            Vector3 size = _originalBoxColliderSizes[i];
            if (inflated && holsteredGripColliderPadding > 0f)
            {
                Vector3 scale = boxCollider.transform.lossyScale;
                float localPaddingX = holsteredGripColliderPadding / Mathf.Max(Mathf.Abs(scale.x), 0.0001f);
                float localPaddingZ = holsteredGripColliderPadding / Mathf.Max(Mathf.Abs(scale.z), 0.0001f);
                size.x += localPaddingX * 2f;
                size.z += localPaddingZ * 2f;
            }

            boxCollider.size = size;
        }
    }

    private void ResolveControllerAnchors()
    {
        if (_cameraRig == null)
        {
            _cameraRig = FindAnyObjectByType<OVRCameraRig>();
        }

        if (_cameraRig == null)
        {
            return;
        }

        _leftControllerAnchor = _cameraRig.leftHandAnchor;
        _rightControllerAnchor = _cameraRig.rightHandAnchor;
    }

    private void SnapGripAnchorToController(Transform preferredController)
    {
        if (gripAnchor == null)
        {
            return;
        }

        Transform controllerAnchor = preferredController;
        if (controllerAnchor == null
            && !TryGetClosestControllerToGrip(out controllerAnchor, out _, snapActivationDistance))
        {
            return;
        }

        Quaternion rotationDelta = controllerAnchor.rotation * Quaternion.Inverse(gripAnchor.rotation);
        transform.rotation = rotationDelta * transform.rotation;
        transform.position += controllerAnchor.position - gripAnchor.position;
    }

    private void UpdateGripAreaHighlight()
    {
        if (!showGripAreaHighlight || _state != HammerState.Holstered)
        {
            _highlightedController = null;
            SetGripHighlightsVisible(false);
            return;
        }

        float effectiveHighlightDistance = Mathf.Min(highlightActivationDistance, snapActivationDistance);
        bool controllerIsNearGrip = TryGetClosestControllerToGrip(
            out Transform controllerAnchor,
            out _,
            effectiveHighlightDistance);

        _highlightedController = controllerIsNearGrip ? controllerAnchor : null;
        SetGripHighlightsVisible(controllerIsNearGrip);
    }

    private bool TryGetClosestControllerToGrip(out Transform closestController, out float closestDistance, float maxDistance)
    {
        ResolveControllerAnchors();

        Transform bestController = null;
        float bestDistance = float.PositiveInfinity;

        CheckController(_leftControllerAnchor);
        CheckController(_rightControllerAnchor);

        closestController = bestController;
        closestDistance = bestDistance;
        return closestController != null && closestDistance <= maxDistance;

        void CheckController(Transform controllerAnchor)
        {
            if (controllerAnchor == null)
            {
                return;
            }

            float distance = GetDistanceToGrabbableArea(controllerAnchor.position);
            if (distance < bestDistance)
            {
                bestController = controllerAnchor;
                bestDistance = distance;
            }
        }
    }

    private float GetDistanceToGrabbableArea(Vector3 position)
    {
        if (_colliders == null)
        {
            return float.PositiveInfinity;
        }

        float closestDistance = float.PositiveInfinity;

        foreach (Collider hammerCollider in _colliders)
        {
            if (hammerCollider == null || !hammerCollider.enabled || IsNonGrabbableCollider(hammerCollider))
            {
                continue;
            }

            Vector3 closestPoint = hammerCollider.ClosestPoint(position);
            float distance = Vector3.Distance(position, closestPoint);
            if (distance < closestDistance)
            {
                closestDistance = distance;
            }
        }

        return closestDistance;
    }

    private bool IsNonGrabbableCollider(Collider hammerCollider)
    {
        if (nonGrabbableColliders == null)
        {
            return false;
        }

        foreach (Collider nonGrabbableCollider in nonGrabbableColliders)
        {
            if (hammerCollider == nonGrabbableCollider)
            {
                return true;
            }
        }

        return false;
    }

    private void ConfigureGripHighlight()
    {
        if (!showGripAreaHighlight)
        {
            return;
        }

        if (gripHighlighter == null)
        {
            return;
        }

        gripHighlighter.enabled = false;
        _gripHighlightsVisible = false;
    }

    private void SetGripHighlightsVisible(bool visible)
    {
        if (_gripHighlightsVisible == visible)
        {
            return;
        }

        _gripHighlightsVisible = visible;

        if (gripHighlighter != null)
        {
            gripHighlighter.enabled = visible;
        }
    }
}
