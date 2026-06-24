using System;
using System.Collections;
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
    private static readonly int IsWalkingParameter = Animator.StringToHash("IsWalking");
    private static readonly int MeleeParameter = Animator.StringToHash("Melee");
    private static readonly int ShootParameter = Animator.StringToHash("Shoot");
    private static readonly int IdleState = Animator.StringToHash("Idle");
    private static readonly int ChargeState = Animator.StringToHash("Charge Shoot");
    private static readonly int MeleeState = Animator.StringToHash("Melee Attack");
    private static readonly int ShootState = Animator.StringToHash("Shoot");

    [Header("Identity")]
    [Tooltip("Solo se usa cuando esta clase base se instancia directamente. Las subclases (CarpinchoSniper, etc.) sobrescriben este valor.")]
    [SerializeField] private CarpinchoType type = CarpinchoType.Sniper;
    [SerializeField, Min(0)] private int scoreValue = 10;

    [Header("Death")]
    [FormerlySerializedAs("vfx")]
    [SerializeField] private GameObject deathVfx;
    [Tooltip("Destruye el arma al impactar (comportamiento legacy del martillo lanzado).")]
    [SerializeField] private bool destroyWeaponOnHit = true;

    [Header("Orientation")]
    [Tooltip("Mantiene el root del enemigo derecho, evitando que herede pitch/roll de spawn points o agentes.")]
    [SerializeField] private bool forceUprightRotation = true;
    [Tooltip("Cantidad de frames posteriores al spawn en los que se revalida world position/rotation.")]
    [SerializeField, Min(0)] private int postSpawnAlignmentFrames = 1;

    public virtual CarpinchoType Type => type;
    public int ScoreValue => scoreValue;
    public bool IsDead => _isDead;

    public event Action<Enemy> Died;

    private Rigidbody _rigidbody;
    private Animator _animator;
    private bool _hasWalkingParameter;
    private bool _hasMeleeParameter;
    private bool _hasShootParameter;
    private bool _isDead;

    protected virtual void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponentInChildren<Animator>(true);
        CacheAnimatorParameters();
    }

    public virtual void OnSpawned(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, GetUprightRotation(rotation));

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        _isDead = false;
        gameObject.SetActive(true);

        ResetAnimation();

        if (postSpawnAlignmentFrames > 0)
        {
            StartCoroutine(ApplyPostSpawnAlignment());
        }
    }

    protected virtual void LateUpdate()
    {
        if (!IsDead)
        {
            ForceUprightRotation();
        }
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

        GameAudioEvents.RaiseCarpinchoDied(transform.position);

        if (deathVfx != null)
        {
            Instantiate(deathVfx, transform.position, Quaternion.identity);
        }

        Died?.Invoke(this);
    }

    public virtual bool TryReceiveDamage(HammerDamageDealer damageDealer, Vector3 hitPoint)
    {
        if (_isDead)
        {
            return false;
        }

        Die();
        return true;
    }

    protected void SetWalkingAnimation(bool isWalking)
    {
        if (_animator == null || !_hasWalkingParameter)
        {
            return;
        }

        _animator.SetBool(IsWalkingParameter, isWalking);
    }

    protected void PlayIdleAnimation()
    {
        SetWalkingAnimation(false);
        PlayStateIfAvailable(IdleState, 0.08f);
    }

    protected void PlayChargeAnimation()
    {
        SetWalkingAnimation(false);
        PlayStateIfAvailable(ChargeState, 0.06f);
    }

    protected void PlayMeleeAnimation()
    {
        SetWalkingAnimation(false);
        if (_animator != null && _hasMeleeParameter)
        {
            _animator.SetTrigger(MeleeParameter);
        }

        PlayStateIfAvailable(MeleeState, 0.04f);
    }

    protected void PlayShootAnimation()
    {
        SetWalkingAnimation(false);
        if (_animator != null && _hasShootParameter)
        {
            _animator.ResetTrigger(ShootParameter);
        }

        PlayStateIfAvailable(ShootState, 0.02f);
    }

    protected void ForceUprightRotation()
    {
        if (!forceUprightRotation)
        {
            return;
        }

        transform.rotation = GetUprightRotation(transform.rotation);
    }

    private IEnumerator ApplyPostSpawnAlignment()
    {
        for (int i = 0; i < postSpawnAlignmentFrames; i++)
        {
            yield return null;

            Vector3 worldPosition = transform.position;
            Quaternion worldRotation = GetUprightRotation(transform.rotation);
            transform.SetPositionAndRotation(worldPosition, worldRotation);

            if (_rigidbody != null)
            {
                _rigidbody.position = worldPosition;
                _rigidbody.rotation = worldRotation;
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }
    }

    private Quaternion GetUprightRotation(Quaternion rotation)
    {
        if (!forceUprightRotation)
        {
            return rotation;
        }

        return Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
    }

    private void ResetAnimation()
    {
        if (_animator == null)
        {
            return;
        }

        _animator.Rebind();
        _animator.Update(0f);
    }

    private void PlayStateIfAvailable(int stateHash, float transitionDuration)
    {
        if (_animator == null || !_animator.HasState(0, stateHash))
        {
            return;
        }

        AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(0);
        if (current.shortNameHash == stateHash || next.shortNameHash == stateHash)
        {
            return;
        }

        _animator.CrossFadeInFixedTime(stateHash, transitionDuration, 0);
    }

    private void CacheAnimatorParameters()
    {
        if (_animator == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = _animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == IsWalkingParameter && parameter.type == AnimatorControllerParameterType.Bool)
            {
                _hasWalkingParameter = true;
            }
            else if (parameter.nameHash == MeleeParameter && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                _hasMeleeParameter = true;
            }
            else if (parameter.nameHash == ShootParameter && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                _hasShootParameter = true;
            }
        }
    }
}
