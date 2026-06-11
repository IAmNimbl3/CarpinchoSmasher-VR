using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CarpinchoJuggernaut : Enemy
{
    [Header("Juggernaut · Movement")]
    [Tooltip("Intervalo entre llamadas a SetDestination. Más alto = menos recálculos de path.")]
    [SerializeField, Min(0.1f)] private float repathInterval = 0.5f;

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

    [Header("Juggernaut · Daño por contacto")]
    [Tooltip("Distancia al HMD a la que se aplica daño por contacto.")]
    [SerializeField, Min(0.1f)] private float damageDistance = 0.8f;
    [SerializeField, Min(0)] private int contactDamage = 1;
    [SerializeField, Min(0.1f)] private float damageCooldown = 1f;

    public override CarpinchoType Type => CarpinchoType.Juggernaut;

    private NavMeshAgent _agent;
    private float _vulnerableAngle;
    private float _zoneTimer;
    private float _repathTimer;
    private float _lastDamageTime;
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
            _agent.isStopped = false;
        }

        _vulnerableAngle = Random.Range(-180f, 180f);
        _zoneTimer = zoneRotationInterval;
        _repathTimer = 0f;
        _lastDamageTime = -damageCooldown;

        ApplyIndicatorColor();
        UpdateIndicatorTransform();
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

        TickChase();
        TickZoneRotation();
        UpdateIndicatorTransform();
        TickContactDamage();
    }

    private void TickChase()
    {
        _repathTimer -= Time.deltaTime;
        if (_repathTimer > 0f)
        {
            return;
        }
        _repathTimer = repathInterval;

        if (_agent == null || !_agent.isOnNavMesh)
        {
            return;
        }

        if (PlayerTarget.TryGetPosition(out Vector3 playerPos))
        {
            _agent.SetDestination(playerPos);
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

    private void TickContactDamage()
    {
        if (!PlayerTarget.TryGetPosition(out Vector3 playerPos))
        {
            return;
        }

        float sqr = PlayerTarget.HorizontalSqrDistance(transform.position, playerPos);
        if (sqr > damageDistance * damageDistance)
        {
            return;
        }

        if (Time.time < _lastDamageTime + damageCooldown)
        {
            return;
        }

        _lastDamageTime = Time.time;
        // TODO: enganchar con PlayerHealth (GDD §9 TBD).
        Debug.Log($"[Juggernaut] Daño por contacto al jugador ({contactDamage}).", this);
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

        if (!IsHitInVulnerableArc(collision))
        {
            return;
        }

        base.OnCollisionEnter(collision);
    }

    private bool IsHitInVulnerableArc(Collision collision)
    {
        if (collision.contactCount == 0)
        {
            return false;
        }

        Vector3 hitPoint = collision.GetContact(0).point;
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
