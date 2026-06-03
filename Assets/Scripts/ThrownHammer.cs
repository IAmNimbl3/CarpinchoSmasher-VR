using UnityEngine;

public class ThrownHammer : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField, Min(0.1f)] private float tumbleSpeed = 14f;
    [SerializeField, Min(0.5f)] private float lifetime = 5f;

    private Rigidbody _rigidbody;
    private Collider[] _colliders;
    private bool[] _originalTriggerStates;
    private Vector3 _originalScale;
    private Vector3 _tumbleAxis = Vector3.right;
    private bool _isThrown;
    private float _flightTimer;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>(true);
        _originalTriggerStates = new bool[_colliders.Length];
        _originalScale = transform.localScale;

        for (int i = 0; i < _colliders.Length; i++)
        {
            _originalTriggerStates[i] = _colliders[i] != null && _colliders[i].isTrigger;
        }
    }

    public void BeginHolstered()
    {
        _isThrown = false;
        _flightTimer = 0f;
        transform.SetParent(null, true);
        transform.localScale = _originalScale;
        ConfigureRigidbody(isKinematic: true, useGravity: false, Vector3.zero, Vector3.zero);
        SetColliders(enabled: true, forceTrigger: true);
    }

    public void BeginHeld()
    {
        _isThrown = false;
        _flightTimer = 0f;
        transform.localScale = _originalScale;
        ConfigureRigidbody(isKinematic: true, useGravity: false, Vector3.zero, Vector3.zero);
        SetColliders(enabled: false, forceTrigger: true);
    }

    public void Throw(Vector3 velocity)
    {
        _isThrown = true;
        _flightTimer = lifetime;
        transform.SetParent(null, true);
        transform.localScale = _originalScale;

        Vector3 horizontalDirection = new Vector3(velocity.x, 0f, velocity.z);
        if (horizontalDirection.sqrMagnitude < 0.0001f)
        {
            horizontalDirection = transform.forward;
            horizontalDirection.y = 0f;
        }

        horizontalDirection.Normalize();
        _tumbleAxis = Vector3.Cross(Vector3.up, horizontalDirection).normalized;
        if (_tumbleAxis.sqrMagnitude < 0.0001f)
        {
            _tumbleAxis = transform.right;
        }

        SetColliders(enabled: true, forceTrigger: false);
        ConfigureRigidbody(isKinematic: false, useGravity: true, velocity, _tumbleAxis * tumbleSpeed);
    }

    private void FixedUpdate()
    {
        if (!_isThrown || _rigidbody == null)
        {
            return;
        }

        _rigidbody.angularVelocity = Vector3.Project(_rigidbody.angularVelocity, _tumbleAxis);
    }

    private void Update()
    {
        if (!_isThrown)
        {
            return;
        }

        _flightTimer -= Time.deltaTime;
        if (_flightTimer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void ConfigureRigidbody(bool isKinematic, bool useGravity, Vector3 linearVelocity, Vector3 angularVelocity)
    {
        if (_rigidbody == null)
        {
            return;
        }

        _rigidbody.isKinematic = isKinematic;
        _rigidbody.useGravity = useGravity;
        _rigidbody.linearVelocity = linearVelocity;
        _rigidbody.angularVelocity = angularVelocity;
        _rigidbody.collisionDetectionMode = isKinematic
            ? CollisionDetectionMode.ContinuousSpeculative
            : CollisionDetectionMode.ContinuousDynamic;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void SetColliders(bool enabled, bool forceTrigger)
    {
        for (int i = 0; i < _colliders.Length; i++)
        {
            Collider hammerCollider = _colliders[i];
            if (hammerCollider == null)
            {
                continue;
            }

            hammerCollider.enabled = enabled;
            hammerCollider.isTrigger = forceTrigger || _originalTriggerStates[i];
        }
    }
}
