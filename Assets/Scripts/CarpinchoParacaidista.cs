using UnityEngine;

public class CarpinchoParacaidista : Enemy
{
    [Header("Caída")]
    [Tooltip("Velocidad vertical de caída en m/s (paracaídas, suave).")]
    [SerializeField, Min(0f)] private float fallSpeed = 2f;
    [Tooltip("Velocidad de tracking horizontal hacia el jugador. Si es muy alta, no se puede esquivar.")]
    [SerializeField, Min(0f)] private float horizontalSpeed = 1.5f;
    [Tooltip("Si está activo, gira para mirar al jugador durante la caída.")]
    [SerializeField] private bool facePlayer = true;

    [Header("Impacto en suelo")]
    [Tooltip("Y del suelo de la arena.")]
    [SerializeField] private float groundY = 0f;
    [Tooltip("Distancia sobre el suelo a la que explota.")]
    [SerializeField, Min(0f)] private float explodeAtHeight = 0.2f;

    [Header("Explosión")]
    [Tooltip("Radio en el que el jugador recibe daño al impactar el suelo.")]
    [SerializeField, Min(0f)] private float explosionRadius = 2f;
    [SerializeField, Min(0)] private int explosionDamage = 1;
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

        Vector3 position = transform.position;
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
                // TODO: enganchar con PlayerHealth cuando se decida sistema de daño (GDD §9 TBD).
                Debug.Log($"[Paracaidista] Player dentro del radio de explosión ({distance:F2}m / {explosionRadius:F2}m). Daño: {explosionDamage}.", this);
            }
        }

        Die();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
