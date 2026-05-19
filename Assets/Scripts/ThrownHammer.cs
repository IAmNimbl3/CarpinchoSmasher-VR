using System.Collections;
using UnityEngine;

public class ThrownHammer : MonoBehaviour
{
    [Header("Aparición")]
    [Tooltip("Duración del scale-in al spawnear.")]
    [SerializeField, Min(0.01f)] private float appearDuration = 0.25f;

    [Header("Vuelo")]
    [Tooltip("Velocidad angular del tumble (rad/s) alrededor del eje perpendicular a la dirección del throw.")]
    [SerializeField, Min(0.1f)] private float tumbleSpeed = 14f;
    [Tooltip("Tiempo máximo en vuelo antes de auto-destruirse (si no pegó nada).")]
    [SerializeField, Min(0.5f)] private float lifetime = 5f;

    private Rigidbody _rigidbody;
    private Collider[] _colliders;
    private Vector3 _originalScale;
    private Vector3 _tumbleAxis;
    private bool _isThrown;
    private float _flightTimer;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>(true);
        _originalScale = transform.localScale;
    }

    public void BeginSummon()
    {
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        SetCollidersEnabled(false);
        StopAllCoroutines();
        StartCoroutine(AppearRoutine());
    }

    private IEnumerator AppearRoutine()
    {
        transform.localScale = Vector3.zero;
        float t = 0f;
        while (t < appearDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / appearDuration);
            transform.localScale = _originalScale * p;
            yield return null;
        }
        transform.localScale = _originalScale;
        SetCollidersEnabled(true);
    }

    public void Throw(Vector3 velocity)
    {
        _isThrown = true;
        StopAllCoroutines();
        transform.localScale = _originalScale;
        transform.SetParent(null, true);

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
            _rigidbody.linearVelocity = velocity;

            Vector3 horizontalDir = new Vector3(velocity.x, 0f, velocity.z);
            if (horizontalDir.sqrMagnitude < 0.0001f)
            {
                horizontalDir = transform.forward;
                horizontalDir.y = 0f;
            }
            horizontalDir.Normalize();
            _tumbleAxis = Vector3.Cross(Vector3.up, horizontalDir).normalized;
            _rigidbody.angularVelocity = _tumbleAxis * tumbleSpeed;
        }

        SetCollidersEnabled(true);
        _flightTimer = lifetime;
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

    private void SetCollidersEnabled(bool enabled)
    {
        if (_colliders == null)
        {
            return;
        }
        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
            {
                _colliders[i].enabled = enabled;
            }
        }
    }
}
