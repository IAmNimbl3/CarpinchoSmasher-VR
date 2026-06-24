using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float speed = 8f;
    [SerializeField, Min(0.5f)] private float lifetime = 5f;

    [Header("Hit")]
    [Tooltip("Distancia al HMD del jugador a la que se cuenta un impacto.")]
    [SerializeField, Min(0.05f)] private float playerHitRadius = 0.35f;
    [SerializeField, Min(0)] private int damage = 5;

    [Header("VFX")]
    [SerializeField] private GameObject impactVfx;

    public bool IsDeflected => _isDeflected;

    private Vector3 _direction;
    private float _elapsed;
    private bool _isDeflected;
    private Rigidbody _rigidbody;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody != null)
        {
            _rigidbody.useGravity = false;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    public void Launch(Vector3 origin, Vector3 direction)
    {
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        Quaternion rotation = Quaternion.LookRotation(_direction);

        transform.SetPositionAndRotation(origin, rotation);
        if (_rigidbody != null)
        {
            _rigidbody.position = origin;
            _rigidbody.rotation = rotation;
        }

        _elapsed = 0f;
        _isDeflected = false;

        ApplyVelocity();
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        if (_elapsed >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (!_isDeflected && PlayerTarget.TryGetPosition(out Vector3 playerPos))
        {
            float sqr = (transform.position - playerPos).sqrMagnitude;
            if (sqr <= playerHitRadius * playerHitRadius)
            {
                // TODO: enganchar con PlayerHealth (GDD §9 TBD).
                Debug.Log($"[Projectile] Impacto a jugador. Daño: {damage}.", this);
                PlayerHealth.TryDamage(damage, this);
                Impact();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.gameObject);
    }

    private void HandleHit(GameObject obj)
    {
        if (obj.CompareTag("Weapon"))
        {
            Deflect();
            return;
        }

        Enemy enemy = obj.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            // Sin deflectar el proyectil atraviesa a los carpinchos (sin friendly fire).
            // Repelido con el martillo, puede matar a cualquiera, incluido el tirador.
            if (!_isDeflected)
            {
                return;
            }

            enemy.Die();
            Impact();
            return;
        }

        Impact();
    }

    private void Deflect()
    {
        _isDeflected = true;
        _direction = -_direction;
        transform.rotation = Quaternion.LookRotation(_direction);
        ApplyVelocity();
    }

    private void ApplyVelocity()
    {
        if (_rigidbody == null)
        {
            return;
        }

        _rigidbody.linearVelocity = _direction * speed;
        _rigidbody.angularVelocity = Vector3.zero;
    }

    private void Impact()
    {
        if (impactVfx != null)
        {
            Instantiate(impactVfx, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}
