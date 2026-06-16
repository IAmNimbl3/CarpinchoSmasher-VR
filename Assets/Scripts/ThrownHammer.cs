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

    private Rigidbody _rigidbody;
    private Collider[] _colliders;
    private bool[] _originalTriggerStates;
    private Grabbable _grabbable;
    private HammerDamageDealer _damageDealer;
    private HammerState _state;
    private float _launchedTimer;
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

    public void BeginHolstered()
    {
        _state = HammerState.Holstered;
        _launchedTimer = 0f;
        _wasSelected = false;

        ConfigureRigidbody(isKinematic: true, useGravity: false);
        SetColliders(enabled: true, forceTrigger: true);

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

        ConfigureRigidbody(isKinematic: true, useGravity: false);
        SetColliders(enabled: true, forceTrigger: true);

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

        ConfigureRigidbody(isKinematic: false, useGravity: useGravityWhenLaunched);
        SetColliders(enabled: true, forceTrigger: false);

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
}
