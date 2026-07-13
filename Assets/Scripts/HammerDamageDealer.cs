using System.Collections.Generic;
using UnityEngine;

public class HammerDamageDealer : MonoBehaviour
{
    [SerializeField] private bool damageEnabled;
    [Tooltip("Cuando el martillo fue lanzado, un impacto valido consume el martillo para evitar matar varios enemigos con el mismo throw.")]
    [SerializeField] private bool consumeLaunchedHammerOnHit = true;

    private readonly HashSet<Enemy> _hitEnemies = new HashSet<Enemy>();
    private ThrownHammer _hammer;
    private bool _launchedHitConsumed;

    private void Awake()
    {
        _hammer = GetComponentInParent<ThrownHammer>();
    }

    public void SetDamageEnabled(bool enabled)
    {
        damageEnabled = enabled;
    }

    public void ResetHitCache()
    {
        _hitEnemies.Clear();
        _launchedHitConsumed = false;
    }

    public bool TryDamage(Enemy enemy)
    {
        return TryDamage(enemy, transform.position);
    }

    public bool TryDamage(Enemy enemy, Vector3 hitPoint)
    {
        if (!damageEnabled || _launchedHitConsumed || enemy == null || enemy.IsDead || _hitEnemies.Contains(enemy))
        {
            return false;
        }

        if (!enemy.TryReceiveDamage(this, hitPoint))
        {
            return false;
        }

        _hitEnemies.Add(enemy);

        if (consumeLaunchedHammerOnHit && _hammer != null && _hammer.IsLaunched)
        {
            _launchedHitConsumed = true;
            damageEnabled = false;
            _hammer.DestroyAfterLaunchedHit();
        }

        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Enemy enemy = other.GetComponentInParent<Enemy>();
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        TryDamage(enemy, hitPoint);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Enemy enemy = collision.collider.GetComponentInParent<Enemy>();
        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : collision.collider.ClosestPoint(transform.position);
        TryDamage(enemy, hitPoint);
    }
}
