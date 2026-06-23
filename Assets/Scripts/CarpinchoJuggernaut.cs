using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

[RequireComponent(typeof(NavMeshAgent))]
public class CarpinchoJuggernaut : Enemy
{
    private enum State
    {
        Chasing,
        Telegraphing,
        Attacking
    }

    [Header("Juggernaut · Chase")]
    [SerializeField, Min(1f)] private float chaseSpeed = 1f;
    [SerializeField, Min(0.1f)] private float repathInterval = 0.5f;
    [Tooltip("Distancia al jugador a la que arranca el casteo del ataque.")]
    [SerializeField, Min(0.3f)] private float meleeRange = 1.6f;

    [Header("Juggernaut · Telegraph + Lunge")]
    [Tooltip("Duración del casteo antes del lunge. Más largo = más tiempo para reaccionar.")]
    [SerializeField, Min(0.1f)] private float telegraphDuration = 0.8f;
    [Tooltip("Duración del lunge committed (no gira durante este tiempo).")]
    [SerializeField, Min(0.1f)] private float lungeDuration = 0.45f;
    [SerializeField, Min(1f)] private float lungeSpeed = 4f;
    [FormerlySerializedAs("damageDistance")]
    [Tooltip("Radio al jugador durante el lunge. Si está dentro, daño.")]
    [SerializeField, Min(0.1f)] private float hitRadius = 1f;
    [FormerlySerializedAs("contactDamage")]
    [SerializeField, Min(0)] private int attackDamage = 1;

    [Header("Juggernaut · Escudo")]
    [Tooltip("Tamaño del arco vulnerable en grados, centrado en el ángulo actual.")]
    [SerializeField, Range(15f, 180f)] private float vulnerableArcDegrees = 60f;
    [Tooltip("Tiempo entre rotaciones aleatorias de la zona vulnerable.")]
    [SerializeField, Min(0.5f)] private float zoneRotationInterval = 5f;
    [Tooltip("Indicador visual del lado vulnerable. Se rota alrededor del cuerpo para apuntar a la zona actual.")]
    [SerializeField] private Transform vulnerableIndicator;
    [SerializeField, Min(0f)] private float indicatorRadialOffset = 0.35f;
    [SerializeField] private float indicatorVerticalOffset = 0.1f;
    [SerializeField] private Color indicatorColor = new Color(1f, 0.2f, 0.1f);

    public override CarpinchoType Type => CarpinchoType.Juggernaut;

    private NavMeshAgent _agent;
    private State _state;
    private float _stateTimer;
    private float _vulnerableAngle;
    private float _zoneTimer;
    private float _repathTimer;
    private Vector3 _lungeDirection;
    private MaterialPropertyBlock _propertyBlock;

    protected override void Awake()
    {
        base.Awake();
        _agent = GetComponent<NavMeshAgent>();
    }

    public override void OnSpawned(Vector3 position, Quaternion rotation)
    {
        base.OnSpawned(position, rotation);

        if (_agent != null)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }
            _agent.speed = chaseSpeed;
        }

        _vulnerableAngle = Random.Range(-180f, 180f);
        _zoneTimer = zoneRotationInterval;

        ApplyIndicatorColor();
        UpdateIndicatorTransform();
        EnterChasing();
    }

    public override void OnDespawned()
    {
        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.ResetPath();
        }
        base.OnDespawned();
    }

    private void Update()
    {
        if (IsDead)
        {
            return;
        }

        TickZoneRotation();
        UpdateIndicatorTransform();

        _stateTimer -= Time.deltaTime;

        switch (_state)
        {
            case State.Chasing:
                TickChasing();
                break;
            case State.Telegraphing:
                TickTelegraphing();
                break;
            case State.Attacking:
                TickAttacking();
                break;
        }
    }

    private void EnterChasing()
    {
        _state = State.Chasing;
        if (_agent != null)
        {
            _agent.isStopped = false;
            _agent.updateRotation = true;
            _agent.speed = chaseSpeed;
        }
        _repathTimer = 0f;
    }

    private void TickChasing()
    {
        if (_agent == null || !_agent.isOnNavMesh)
        {
            return;
        }

        if (!PlayerTarget.TryGetPosition(out Vector3 playerPos))
        {
            return;
        }

        _repathTimer -= Time.deltaTime;
        if (_repathTimer <= 0f)
        {
            _agent.SetDestination(playerPos);
            _repathTimer = repathInterval;
        }

        float sqr = PlayerTarget.HorizontalSqrDistance(transform.position, playerPos);
        if (sqr <= meleeRange * meleeRange)
        {
            EnterTelegraphing();
        }
    }

    private void TickZoneRotation()
    {
        _zoneTimer -= Time.deltaTime;
        if (_zoneTimer > 0f)
        {
            return;
        }
        _zoneTimer = zoneRotationInterval;

        float offset = Random.Range(90f, 270f);
        _vulnerableAngle = NormalizeAngle(_vulnerableAngle + offset);
    }

    private void EnterTelegraphing()
    {
        _state = State.Telegraphing;
        _stateTimer = telegraphDuration;
        if (_agent != null)
        {
            _agent.ResetPath();
            _agent.isStopped = true;
            _agent.updateRotation = false;
        }
    }

    private void TickTelegraphing()
    {
        if (PlayerTarget.TryGetPosition(out Vector3 playerPos))
        {
            Vector3 look = playerPos - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(look);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 10f);
            }
        }

        if (_stateTimer <= 0f)
        {
            EnterAttacking();
        }
    }

    private void EnterAttacking()
    {
        _state = State.Attacking;
        _stateTimer = lungeDuration;

        if (PlayerTarget.TryGetPosition(out Vector3 playerPos))
        {
            Vector3 dir = playerPos - transform.position;
            dir.y = 0f;
            _lungeDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        }
        else
        {
            _lungeDirection = transform.forward;
        }

        if (_lungeDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(_lungeDirection);
        }
    }

    private void TickAttacking()
    {
        Vector3 step = _lungeDirection * lungeSpeed * Time.deltaTime;
        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.Move(step);
        }
        else
        {
            transform.position += step;
        }

        if (PlayerTarget.TryGetPosition(out Vector3 playerPos))
        {
            float sqr = PlayerTarget.HorizontalSqrDistance(transform.position, playerPos);
            if (sqr <= hitRadius * hitRadius)
            {
                // TODO: enganchar con PlayerHealth (GDD §9 TBD).
                Debug.Log($"[Juggernaut] Lunge impactó al jugador ({attackDamage}).", this);
                EnterNextAttackCycle(playerPos);
                return;
            }
        }

        if (_stateTimer <= 0f)
        {
            if (PlayerTarget.TryGetPosition(out Vector3 currentPlayerPos))
            {
                EnterNextAttackCycle(currentPlayerPos);
            }
            else
            {
                EnterChasing();
            }
        }
    }

    private void EnterNextAttackCycle(Vector3 playerPos)
    {
        float sqr = PlayerTarget.HorizontalSqrDistance(transform.position, playerPos);
        if (sqr <= meleeRange * meleeRange)
        {
            EnterTelegraphing();
            return;
        }

        EnterChasing();
    }

    private void UpdateIndicatorTransform()
    {
        if (vulnerableIndicator == null)
        {
            return;
        }

        Quaternion localRotation = Quaternion.Euler(0f, _vulnerableAngle, 0f);
        Vector3 localDir = localRotation * Vector3.forward;
        vulnerableIndicator.localPosition = localDir * indicatorRadialOffset + Vector3.up * indicatorVerticalOffset;
        vulnerableIndicator.localRotation = localRotation;
    }

    private void ApplyIndicatorColor()
    {
        if (vulnerableIndicator == null)
        {
            return;
        }

        Renderer renderer = vulnerableIndicator.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        _propertyBlock ??= new MaterialPropertyBlock();
        _propertyBlock.SetColor("_BaseColor", indicatorColor);
        _propertyBlock.SetColor("_Color", indicatorColor);
        renderer.SetPropertyBlock(_propertyBlock);
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        if (IsDead)
        {
            return;
        }

        if (!collision.gameObject.CompareTag("Weapon"))
        {
            return;
        }

        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : collision.collider.ClosestPoint(transform.position);

        if (!IsHitInVulnerableArc(hitPoint))
        {
            return;
        }

        HammerDamageDealer hammerDamageDealer = collision.gameObject.GetComponentInParent<HammerDamageDealer>();
        if (hammerDamageDealer != null)
        {
            hammerDamageDealer.TryDamage(this, hitPoint);
            return;
        }

        base.OnCollisionEnter(collision);
    }

    public override bool TryReceiveDamage(HammerDamageDealer damageDealer, Vector3 hitPoint)
    {
        if (!IsHitInVulnerableArc(hitPoint))
        {
            return false;
        }

        return base.TryReceiveDamage(damageDealer, hitPoint);
    }

    private bool IsHitInVulnerableArc(Vector3 hitPoint)
    {
        Vector3 toHit = hitPoint - transform.position;
        toHit.y = 0f;
        if (toHit.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        float hitAngleFromForward = Vector3.SignedAngle(transform.forward, toHit.normalized, Vector3.up);
        float diff = Mathf.Abs(Mathf.DeltaAngle(hitAngleFromForward, _vulnerableAngle));
        return diff <= vulnerableArcDegrees * 0.5f;
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }
        while (angle <= -180f)
        {
            angle += 360f;
        }
        return angle;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        float halfArc = vulnerableArcDegrees * 0.5f;
        Vector3 origin = transform.position + Vector3.up * indicatorVerticalOffset;
        Quaternion bodyRot = transform.rotation;
        Quaternion centerRot = bodyRot * Quaternion.Euler(0f, _vulnerableAngle, 0f);

        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.9f);
        Gizmos.DrawRay(origin, centerRot * Vector3.forward * indicatorRadialOffset * 2f);
        Gizmos.DrawRay(origin, centerRot * Quaternion.Euler(0f, halfArc, 0f) * Vector3.forward * indicatorRadialOffset * 1.5f);
        Gizmos.DrawRay(origin, centerRot * Quaternion.Euler(0f, -halfArc, 0f) * Vector3.forward * indicatorRadialOffset * 1.5f);
    }
}
