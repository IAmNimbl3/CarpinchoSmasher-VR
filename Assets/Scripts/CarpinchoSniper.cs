using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CarpinchoSniper : Enemy
{
    private enum State
    {
        Repositioning,
        Aiming,
        Cooldown
    }

    [Header("Sniper · Proyectil")]
    [SerializeField] private Projectile projectilePrefab;
    [Tooltip("Punto de origen del disparo. Si es null, se usa la posición del carpincho con offset.")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private Vector3 muzzleFallbackOffset = new Vector3(0f, 0.3f, 0.4f);

    [Header("Sniper · Posicionamiento")]
    [Tooltip("Distancia ideal al jugador al elegir nueva posición.")]
    [SerializeField, Min(0.5f)] private float idealRange = 6f;
    [SerializeField, Min(0.5f)] private float minRange = 4f;
    [SerializeField, Min(1f)] private float maxRange = 10f;
    [Tooltip("Radio de búsqueda NavMesh cuando se samplea una nueva posición.")]
    [SerializeField, Min(0.1f)] private float sampleRadius = 2f;

    [Header("Sniper · Tempo")]
    [SerializeField, Min(0.1f)] private float aimDuration = 1f;
    [SerializeField, Min(0.1f)] private float shotCooldown = 1.5f;
    [SerializeField, Min(1)] private int shotsBeforeReposition = 2;
    [SerializeField, Min(1f)] private float aimRotationSpeed = 6f;

    [Header("Sniper · LOS")]
    [Tooltip("Layers que bloquean la línea de visión.")]
    [SerializeField] private LayerMask losBlockMask = ~0;

    public override CarpinchoType Type => CarpinchoType.Sniper;

    private NavMeshAgent _agent;
    private State _state;
    private float _stateTimer;
    private int _shotsFired;

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
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, sampleRadius * 4f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }
            _agent.isStopped = false;
            _agent.updateRotation = true;
        }

        _shotsFired = 0;
        EnterRepositioning();
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

        switch (_state)
        {
            case State.Repositioning:
                TickRepositioning();
                break;
            case State.Aiming:
                TickAiming();
                break;
            case State.Cooldown:
                TickCooldown();
                break;
        }
    }

    private void EnterRepositioning()
    {
        _state = State.Repositioning;
        if (_agent != null)
        {
            _agent.isStopped = false;
            _agent.updateRotation = true;
        }
        TryPickNewPosition();
    }

    private void TickRepositioning()
    {
        if (!PlayerTarget.TryGetPosition(out Vector3 playerPos))
        {
            return;
        }

        float sqr = (transform.position - playerPos).sqrMagnitude;
        bool inRange = sqr >= minRange * minRange && sqr <= maxRange * maxRange;
        if (inRange && HasLineOfSight(playerPos))
        {
            EnterAiming();
            return;
        }

        if (_agent == null || !_agent.isOnNavMesh)
        {
            return;
        }

        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
        {
            TryPickNewPosition();
        }
    }

    private void TryPickNewPosition()
    {
        if (_agent == null || !_agent.isOnNavMesh)
        {
            return;
        }

        if (!PlayerTarget.TryGetPosition(out Vector3 playerPos))
        {
            return;
        }

        for (int i = 0; i < 6; i++)
        {
            Vector2 r = Random.insideUnitCircle.normalized;
            Vector3 candidate = playerPos + new Vector3(r.x, 0f, r.y) * idealRange;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            {
                continue;
            }

            if (!HasLineOfSightFrom(hit.position, playerPos))
            {
                continue;
            }

            _agent.SetDestination(hit.position);
            return;
        }
    }

    private void EnterAiming()
    {
        _state = State.Aiming;
        _stateTimer = aimDuration;
        if (_agent != null)
        {
            _agent.ResetPath();
            _agent.isStopped = true;
            _agent.updateRotation = false;
        }
    }

    private void TickAiming()
    {
        if (!PlayerTarget.TryGetPosition(out Vector3 playerPos))
        {
            EnterRepositioning();
            return;
        }

        if (!HasLineOfSight(playerPos))
        {
            EnterRepositioning();
            return;
        }

        Vector3 look = playerPos - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
        {
            Quaternion target = Quaternion.LookRotation(look);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * aimRotationSpeed);
        }

        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f)
        {
            Shoot(playerPos);
        }
    }

    private void Shoot(Vector3 playerPos)
    {
        if (projectilePrefab != null)
        {
            Vector3 origin = GetMuzzlePosition();
            Vector3 direction = (playerPos - origin).normalized;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction)
                : Quaternion.identity;
            Projectile p = Instantiate(projectilePrefab, origin, rotation);
            p.Launch(origin, direction, this);
        }

        _shotsFired++;
        _state = State.Cooldown;
        _stateTimer = shotCooldown;
    }

    private void TickCooldown()
    {
        _stateTimer -= Time.deltaTime;
        if (_stateTimer > 0f)
        {
            return;
        }

        if (_shotsFired >= shotsBeforeReposition)
        {
            _shotsFired = 0;
            EnterRepositioning();
        }
        else
        {
            EnterAiming();
        }
    }

    private Vector3 GetMuzzlePosition()
    {
        if (muzzle != null)
        {
            return muzzle.position;
        }
        return transform.TransformPoint(muzzleFallbackOffset);
    }

    private bool HasLineOfSight(Vector3 playerPos)
    {
        return HasLineOfSightFrom(GetMuzzlePosition(), playerPos);
    }

    private bool HasLineOfSightFrom(Vector3 origin, Vector3 playerPos)
    {
        Vector3 dir = playerPos - origin;
        float distance = dir.magnitude;
        if (distance < 0.01f)
        {
            return true;
        }

        if (!Physics.Raycast(origin, dir / distance, out RaycastHit hit, distance, losBlockMask, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        Transform player = PlayerTarget.Transform;
        return player != null && (hit.transform == player || hit.transform.IsChildOf(player));
    }
}
