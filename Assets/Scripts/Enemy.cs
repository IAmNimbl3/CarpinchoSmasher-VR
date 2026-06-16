using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum CarpinchoType
{
    Sniper,
    Paracaidista,
    Velocista,
    Juggernaut
}

public class Enemy : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Solo se usa cuando esta clase base se instancia directamente. Las subclases (CarpinchoSniper, etc.) sobrescriben este valor.")]
    [SerializeField] private CarpinchoType type = CarpinchoType.Sniper;
    [SerializeField, Min(0)] private int scoreValue = 10;

    [Header("Death")]
    [FormerlySerializedAs("vfx")]
    [SerializeField] private GameObject deathVfx;
    [Tooltip("Destruye el arma al impactar (comportamiento legacy del martillo lanzado).")]
    [SerializeField] private bool destroyWeaponOnHit = true;

    public virtual CarpinchoType Type => type;
    public int ScoreValue => scoreValue;
    public bool IsDead => _isDead;

    public event Action<Enemy> Died;

    private Rigidbody _rigidbody;
    private bool _isDead;

    protected virtual void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    public virtual void OnSpawned(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        _isDead = false;
        gameObject.SetActive(true);
    }

    public virtual void OnDespawned()
    {
        gameObject.SetActive(false);
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (_isDead)
        {
            return;
        }

        if (!collision.gameObject.CompareTag("Weapon"))
        {
            return;
        }

        HammerDamageDealer hammerDamageDealer = collision.gameObject.GetComponentInParent<HammerDamageDealer>();
        if (hammerDamageDealer != null)
        {
            hammerDamageDealer.TryDamage(this);
            return;
        }

        if (destroyWeaponOnHit)
        {
            Destroy(collision.gameObject);
        }

        Die();
    }

    public void Die()
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;

        if (deathVfx != null)
        {
            Instantiate(deathVfx, transform.position, Quaternion.identity);
        }

        Died?.Invoke(this);
    }
}
