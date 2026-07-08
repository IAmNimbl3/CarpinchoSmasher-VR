using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HammerAimAssist : MonoBehaviour
{
    [Header("Assist")]
    [SerializeField] private bool assistEnabled = true;
    [Tooltip("Radio de la esfera virtual alrededor del carpincho. Si el trayecto del martillo entra en este radio, se corrige hacia ese enemigo.")]
    [SerializeField, Min(0f)] private float assistRadius = 0.55f;
    [Tooltip("Distancia maxima desde el martillo para buscar enemigos candidatos.")]
    [SerializeField, Min(0.1f)] private float maxCandidateDistance = 4f;
    [Tooltip("Altura extra sobre el pivot del enemigo a la que apunta el martillo.")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.35f, 0f);
    [Tooltip("Cuanto tiempo mantiene el lock despues de detectar un candidato.")]
    [SerializeField, Min(0f)] private float lockDuration = 0.35f;

    [Header("Velocity")]
    [SerializeField, Min(0f)] private float minRedirectSpeed = 5f;
    [SerializeField, Min(0f)] private float maxRedirectSpeed = 16f;
    [SerializeField, Min(0f)] private float redirectSpeedMultiplier = 1.15f;
    [Tooltip("Si esta activo, permite volver a corregir hacia otro enemigo mientras el martillo sigue lanzado.")]
    [SerializeField] private bool allowRetargetAfterLock;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos;
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.9f, 1f, 0.35f);

    private Rigidbody _rigidbody;
    private ThrownHammer _hammer;
    private Enemy _lockedTarget;
    private Vector3 _previousPosition;
    private float _lockTimer;
    private bool _wasLaunched;

    private void Awake()
    {
        CacheReferences();
        _previousPosition = transform.position;
    }

    private void OnEnable()
    {
        CacheReferences();
        ResetAssist();
    }

    private void FixedUpdate()
    {
        if (!assistEnabled || _hammer == null || !_hammer.IsLaunched || _rigidbody == null || _rigidbody.isKinematic)
        {
            ResetAssist();
            _previousPosition = transform.position;
            return;
        }

        if (!_wasLaunched)
        {
            _wasLaunched = true;
            _previousPosition = transform.position;
        }

        if (_lockedTarget != null && !_lockedTarget.IsDead)
        {
            _lockTimer -= Time.fixedDeltaTime;
            RedirectVelocityTo(_lockedTarget);

            if (_lockTimer > 0f || !allowRetargetAfterLock)
            {
                _previousPosition = transform.position;
                return;
            }
        }

        Enemy candidate = FindCandidateOnTrajectory(_previousPosition, transform.position);
        if (candidate != null)
        {
            _lockedTarget = candidate;
            _lockTimer = lockDuration;
            RedirectVelocityTo(_lockedTarget);
        }

        _previousPosition = transform.position;
    }

    private void CacheReferences()
    {
        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        if (_hammer == null)
        {
            _hammer = GetComponent<ThrownHammer>();
        }
    }

    private void ResetAssist()
    {
        _lockedTarget = null;
        _lockTimer = 0f;
        _wasLaunched = false;
    }

    private Enemy FindCandidateOnTrajectory(Vector3 from, Vector3 to)
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Enemy best = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < enemies.Length; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 targetPoint = GetTargetPoint(enemy);
            float distanceFromHammer = Vector3.Distance(transform.position, targetPoint);
            if (distanceFromHammer > maxCandidateDistance)
            {
                continue;
            }

            float trajectoryDistance = DistancePointToSegment(targetPoint, from, to);
            if (trajectoryDistance > assistRadius || trajectoryDistance >= bestDistance)
            {
                continue;
            }

            bestDistance = trajectoryDistance;
            best = enemy;
        }

        return best;
    }

    private void RedirectVelocityTo(Enemy target)
    {
        Vector3 targetPoint = GetTargetPoint(target);
        Vector3 direction = targetPoint - transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float currentSpeed = _rigidbody.linearVelocity.magnitude;
        float redirectedSpeed = Mathf.Clamp(
            currentSpeed * redirectSpeedMultiplier,
            minRedirectSpeed,
            maxRedirectSpeed > 0f ? maxRedirectSpeed : float.PositiveInfinity);

        _rigidbody.linearVelocity = direction.normalized * redirectedSpeed;
    }

    private Vector3 GetTargetPoint(Enemy enemy)
    {
        Collider targetCollider = enemy.GetComponentInChildren<Collider>();
        if (targetCollider != null)
        {
            return targetCollider.bounds.center + targetOffset;
        }

        return enemy.transform.position + targetOffset;
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 segment = b - a;
        float segmentSqrMagnitude = segment.sqrMagnitude;
        if (segmentSqrMagnitude <= 0.0001f)
        {
            return Vector3.Distance(point, a);
        }

        float t = Mathf.Clamp01(Vector3.Dot(point - a, segment) / segmentSqrMagnitude);
        Vector3 closestPoint = a + segment * t;
        return Vector3.Distance(point, closestPoint);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, assistRadius);
    }
}
