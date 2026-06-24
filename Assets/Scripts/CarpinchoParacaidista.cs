using System;
using UnityEngine;

public class CarpinchoParacaidista : Enemy
{
    [Header("CaÃ­da")]
    [Tooltip("Velocidad vertical de caÃ­da en m/s (paracaÃ­das, suave).")]
    [SerializeField, Min(0f)] private float fallSpeed = 2f;
    [Tooltip("Velocidad de tracking horizontal hacia el jugador. Si es muy alta, no se puede esquivar.")]
    [SerializeField, Min(0f)] private float horizontalSpeed = 1.5f;
    [Tooltip("Si estÃ¡ activo, gira para mirar al jugador durante la caÃ­da.")]
    [SerializeField] private bool facePlayer = true;

    [Header("Zona de caida")]
    [Tooltip("Si esta activo, el paracaidista elige una zona fija delante o detras del jugador al spawnear en vez de caer sobre su cabeza.")]
    [SerializeField] private bool usePlayerRelativeLandingZones = true;
    [Tooltip("Distancia horizontal desde el jugador hasta el centro de la zona de caida.")]
    [SerializeField, Min(0f)] private float landingZoneDistance = 1.65f;
    [Tooltip("Variacion lateral aleatoria de la zona de caida.")]
    [SerializeField, Min(0f)] private float landingZoneLateralRadius = 0.6f;
    [Tooltip("Variacion aleatoria adelante/atras alrededor del centro de la zona.")]
    [SerializeField, Min(0f)] private float landingZoneForwardRadius = 0.35f;
    [Tooltip("Si esta activo, el punto de spawn se reposiciona sobre la zona elegida usando Landing Spawn Height.")]
    [SerializeField] private bool spawnAboveLandingZone = true;
    [Tooltip("Altura desde la que aparece sobre el suelo cuando usa zonas de caida.")]
    [SerializeField, Min(0.5f)] private float landingSpawnHeight = 5.5f;

    [Header("Impacto en suelo")]
    [Tooltip("Y del suelo de la arena.")]
    [SerializeField] private float groundY = 0f;
    [Tooltip("Distancia sobre el suelo a la que explota.")]
    [SerializeField, Min(0f)] private float explodeAtHeight = 0.2f;
    [Tooltip("Capas consideradas suelo para detectar el impacto. Por defecto incluye todo y filtra su propio collider.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0.1f)] private float groundProbeHeight = 1.5f;
    [SerializeField, Min(0f)] private float groundProbeExtraDistance = 0.5f;

    [Header("ExplosiÃ³n")]
    [Tooltip("Radio en el que el jugador recibe daÃ±o al impactar el suelo.")]
    [SerializeField, Min(0f)] private float explosionRadius = 2f;
    [SerializeField, Min(0)] private int explosionDamage = 20;
    [SerializeField] private GameObject explosionVfx;

    private bool _exploded;
    private Vector3 _landingTarget;
    private Vector3 _landingFacingDirection;
    private bool _hasLandingTarget;

    public override CarpinchoType Type => CarpinchoType.Paracaidista;

    public override void OnSpawned(Vector3 position, Quaternion rotation)
    {
        base.OnSpawned(position, rotation);
        _exploded = false;
        PickLandingTarget();

        if (spawnAboveLandingZone && _hasLandingTarget)
        {
            transform.position = new Vector3(_landingTarget.x, groundY + landingSpawnHeight, _landingTarget.z);
        }

        if (_hasLandingTarget && _landingFacingDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(_landingFacingDirection);
        }
    }

    private void Update()
    {
        if (IsDead || _exploded)
        {
            return;
        }

        Vector3 previousPosition = transform.position;
        Vector3 position = previousPosition;
        position.y -= fallSpeed * Time.deltaTime;

        if (_hasLandingTarget)
        {
            Vector3 currentHorizontal = new Vector3(position.x, 0f, position.z);
            Vector3 targetHorizontal = new Vector3(_landingTarget.x, 0f, _landingTarget.z);
            Vector3 nextHorizontal = Vector3.MoveTowards(currentHorizontal, targetHorizontal, horizontalSpeed * Time.deltaTime);
            position.x = nextHorizontal.x;
            position.z = nextHorizontal.z;
        }
        else if (PlayerTarget.TryGetPosition(out Vector3 playerPos))
        {
            Vector3 currentHorizontal = new Vector3(position.x, 0f, position.z);
            Vector3 targetHorizontal = new Vector3(playerPos.x, 0f, playerPos.z);
            Vector3 nextHorizontal = Vector3.MoveTowards(currentHorizontal, targetHorizontal, horizontalSpeed * Time.deltaTime);
            position.x = nextHorizontal.x;
            position.z = nextHorizontal.z;

            if (facePlayer)
            {
                Vector3 lookDir = new Vector3(playerPos.x - position.x, 0f, playerPos.z - position.z);
                if (lookDir.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
        }

        if (facePlayer && _hasLandingTarget && _landingFacingDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(_landingFacingDirection);
        }

        transform.position = position;

        if (TryGetGroundImpact(previousPosition, position, out RaycastHit groundHit))
        {
            transform.position = groundHit.point;
            Explode();
            return;
        }

        if (position.y <= groundY + explodeAtHeight)
        {
            Explode();
        }
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        if (_exploded)
        {
            return;
        }

        if (collision.gameObject.CompareTag("Weapon"))
        {
            base.OnCollisionEnter(collision);
            return;
        }

        Explode();
    }

    private void Explode()
    {
        if (_exploded || IsDead)
        {
            return;
        }

        _exploded = true;

        if (explosionVfx != null)
        {
            Instantiate(explosionVfx, transform.position, Quaternion.identity);
        }

        PlayerHealth.TryDamage(explosionDamage, this);
        Debug.Log($"[Paracaidista] Impacto en suelo. Dano al jugador: {explosionDamage}.", this);

        Die();
    }

    private void PickLandingTarget()
    {
        _hasLandingTarget = false;
        _landingFacingDirection = Vector3.forward;

        if (!usePlayerRelativeLandingZones || !PlayerTarget.TryGetPosition(out Vector3 playerPos))
        {
            return;
        }

        Vector3 forward = ResolvePlayerForward();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float frontOrBack = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float forwardJitter = UnityEngine.Random.Range(-landingZoneForwardRadius, landingZoneForwardRadius);
        float lateralJitter = UnityEngine.Random.Range(-landingZoneLateralRadius, landingZoneLateralRadius);

        Vector3 offset = forward * (frontOrBack * landingZoneDistance + forwardJitter)
            + right * lateralJitter;
        _landingTarget = new Vector3(playerPos.x + offset.x, groundY, playerPos.z + offset.z);
        _landingFacingDirection = (forward * frontOrBack).normalized;
        _hasLandingTarget = true;
    }

    private Vector3 ResolvePlayerForward()
    {
        Transform target = PlayerTarget.Transform;
        Vector3 forward = target != null ? target.forward : Vector3.forward;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up);

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        return forward.normalized;
    }

    private bool TryGetGroundImpact(Vector3 previousPosition, Vector3 nextPosition, out RaycastHit groundHit)
    {
        float topY = Mathf.Max(previousPosition.y, nextPosition.y) + groundProbeHeight;
        Vector3 origin = new Vector3(nextPosition.x, topY, nextPosition.z);
        float castDistance = groundProbeHeight
            + Mathf.Abs(previousPosition.y - nextPosition.y)
            + explodeAtHeight
            + groundProbeExtraDistance;

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, castDistance, groundMask, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            bool crossedGround = previousPosition.y >= hit.point.y + explodeAtHeight
                && nextPosition.y <= hit.point.y + explodeAtHeight;
            bool alreadyTouching = nextPosition.y <= hit.point.y + explodeAtHeight;
            if (crossedGround || alreadyTouching)
            {
                groundHit = hit;
                return true;
            }
        }

        groundHit = default;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        if (_hasLandingTarget)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.7f);
            Gizmos.DrawWireSphere(_landingTarget, 0.25f);
        }
    }
}
