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

    public override CarpinchoType Type => CarpinchoType.Paracaidista;

    public override void OnSpawned(Vector3 position, Quaternion rotation)
    {
        base.OnSpawned(position, rotation);
        _exploded = false;
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

        if (PlayerTarget.TryGetPosition(out Vector3 playerPos))
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

        if (PlayerTarget.TryGetPosition(out Vector3 playerPos))
        {
            float distance = Vector3.Distance(transform.position, playerPos);
            if (distance <= explosionRadius)
            {
                PlayerHealth.TryDamage(explosionDamage, this);
                // TODO: enganchar con PlayerHealth cuando se decida sistema de daÃ±o (GDD Â§9 TBD).
                Debug.Log($"[Paracaidista] Player dentro del radio de explosiÃ³n ({distance:F2}m / {explosionRadius:F2}m). DaÃ±o: {explosionDamage}.", this);
            }
        }

        Die();
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
    }
}
