using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CarpinchoVelocista : Enemy
{
    private enum State
    {
        Chasing,
        Telegraphing,
        Attacking,
        Vulnerable,
        Recovering
    }

    [Header("Velocista · Chase")]
    [SerializeField, Min(1f)] private float chaseSpeed = 1f;
    [SerializeField, Min(0.1f)] private float repathInterval = 0.2f;
    [Tooltip("Distancia al jugador a la que arranca el telegraph del ataque.")]
    [SerializeField, Min(0.3f)] private float meleeRange = 1.5f;

    [Header("Velocista · Telegraph + Lunge")]
    [Tooltip("Duración del telegraph antes del lunge. Más largo = más fácil esquivar.")]
    [SerializeField, Min(0.1f)] private float telegraphDuration = 0.5f;
    [Tooltip("Duración del lunge committed (no gira durante este tiempo).")]
    [SerializeField, Min(0.1f)] private float lungeDuration = 0.4f;
    [SerializeField, Min(1f)] private float lungeSpeed = 6f;
    [Tooltip("Radio al jugador durante el lunge. Si está dentro, daño. Si nunca entró, esquivó.")]
    [SerializeField, Min(0.1f)] private float hitRadius = 0.7f;
    [SerializeField, Min(0)] private int attackDamage = 1;

    [Header("Velocista · Vulnerable")]
    [Tooltip("Ventana en la que el jugador puede matarlo con golpe directo.")]
    [SerializeField, Min(0.1f)] private float vulnerableDuration = 1.2f;
    [SerializeField, Min(0.1f)] private float recoveryDuration = 0.5f;

    [Header("Velocista · Visuals")]
    [Tooltip("Indicador de estado encima de la cabeza. Cambia de color en Telegraph y Vulnerable.")]
    [SerializeField] private GameObject stateIndicator;
    [SerializeField] private Color telegraphColor = new Color(1f, 0.9f, 0f);
    [SerializeField] private Color vulnerableColor = new Color(0.2f, 0.7f, 1f);

    public override CarpinchoType Type => CarpinchoType.Velocista;

    private NavMeshAgent _agent;
    private State _state;
    private float _stateTimer;
    private float _repathTimer;
    private Vector3 _lungeDirection;
    private bool _hitDuringLunge;
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

        EnterChasing();
    }

    public override void OnDespawned()
    {
        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.ResetPath();
        }
        HideIndicator();
        base.OnDespawned();
    }

    private void Update()
    {
        if (IsDead)
        {
            return;
        }

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
            case State.Vulnerable:
                TickVulnerable();
                break;
            case State.Recovering:
                TickRecovering();
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
        HideIndicator();
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

        float sqr = (transform.position - playerPos).sqrMagnitude;
        if (sqr <= meleeRange * meleeRange)
        {
            EnterTelegraphing();
        }
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
        ShowIndicator(telegraphColor);
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
        _hitDuringLunge = false;

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

        HideIndicator();
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
            float sqr = (transform.position - playerPos).sqrMagnitude;
            if (sqr <= hitRadius * hitRadius)
            {
                _hitDuringLunge = true;
                // TODO: enganchar con PlayerHealth (GDD §9 TBD).
                Debug.Log($"[Velocista] Lunge impactó al jugador ({attackDamage}).", this);
                EnterRecovering();
                return;
            }
        }

        if (_stateTimer <= 0f)
        {
            EnterVulnerable();
        }
    }

    private void EnterVulnerable()
    {
        _state = State.Vulnerable;
        _stateTimer = vulnerableDuration;
        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.ResetPath();
            _agent.isStopped = true;
            _agent.updateRotation = false;
        }
        ShowIndicator(vulnerableColor);
    }

    private void TickVulnerable()
    {
        if (_stateTimer <= 0f)
        {
            EnterRecovering();
        }
    }

    private void EnterRecovering()
    {
        _state = State.Recovering;
        _stateTimer = recoveryDuration;
        HideIndicator();
    }

    private void TickRecovering()
    {
        if (_stateTimer <= 0f)
        {
            EnterChasing();
        }
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        // Eliminable únicamente durante la ventana vulnerable (GDD §7.2).
        if (_state != State.Vulnerable)
        {
            return;
        }
        base.OnCollisionEnter(collision);
    }

    private void ShowIndicator(Color color)
    {
        if (stateIndicator == null)
        {
            return;
        }

        if (!stateIndicator.activeSelf)
        {
            stateIndicator.SetActive(true);
        }

        Renderer renderer = stateIndicator.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        _propertyBlock ??= new MaterialPropertyBlock();
        _propertyBlock.SetColor("_BaseColor", color);
        _propertyBlock.SetColor("_Color", color);
        renderer.SetPropertyBlock(_propertyBlock);
    }

    private void HideIndicator()
    {
        if (stateIndicator != null && stateIndicator.activeSelf)
        {
            stateIndicator.SetActive(false);
        }
    }
}
