using System;
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

    private Rigidbody _rigidbody;
    private Collider[] _colliders;
    private bool[] _originalTriggerStates;
    private Grabbable _grabbable;
    private HammerDamageDealer _damageDealer;
    private PhysicsMaterial _launchedMaterial;
    private HammerState _state;
    private float _launchedTimer;
    private int _launchVelocityTuneFrames;
    private bool _wasSelected;

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
    }

    private void Update()
    {
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
        _launchedTimer = 0f;
        _launchVelocityTuneFrames = 0;
        _wasSelected = false;

        ConfigureRigidbody(isKinematic: true, useGravity: false);
        SetColliders(enabled: true, forceTrigger: true);
        SetNonGrabbableCollidersEnabled(false);

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
            return;
        }

        _state = HammerState.Held;
        _launchedTimer = 0f;
        _launchVelocityTuneFrames = 0;

        ConfigureRigidbody(isKinematic: true, useGravity: false);
        SetColliders(enabled: true, forceTrigger: true);
        SetNonGrabbableCollidersEnabled(true);

        if (_damageDealer != null)
        {
            _damageDealer.SetDamageEnabled(true);
            _damageDealer.ResetHitCache();
        }

        Grabbed?.Invoke(this);
    }

    private void BeginLaunched()
    {
        if (_state == HammerState.Launched)
        {
            return;
        }

        _state = HammerState.Launched;
        _launchedTimer = launchedLifetime;
        _launchVelocityTuneFrames = 3;

        ConfigureRigidbody(isKinematic: false, useGravity: useGravityWhenLaunched);
        SetColliders(enabled: true, forceTrigger: false);
        SetNonGrabbableCollidersEnabled(true);
        ApplyLaunchedPhysicsMaterial();

        if (_damageDealer != null)
        {
            _damageDealer.SetDamageEnabled(true);
            _damageDealer.ResetHitCache();
        }

        Released?.Invoke(this);
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
            BeginLaunched();
        }

        _wasSelected = isSelected;
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

            for (int i = 0; i < _colliders.Length; i++)
            {
                _originalTriggerStates[i] = _colliders[i] != null && _colliders[i].isTrigger;
            }
        }
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
}
