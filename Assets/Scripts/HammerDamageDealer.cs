using System.Collections.Generic;
using UnityEngine;

public class HammerDamageDealer : MonoBehaviour
{
    [SerializeField] private bool damageEnabled;

    private readonly HashSet<Enemy> _hitEnemies = new HashSet<Enemy>();

    public void SetDamageEnabled(bool enabled)
    {
        damageEnabled = enabled;
    }

    public void ResetHitCache()
    {
        _hitEnemies.Clear();
    }

    public bool TryDamage(Enemy enemy)
    {
        return TryDamage(enemy, transform.position);
    }

    public bool TryDamage(Enemy enemy, Vector3 hitPoint)
    {
        if (!damageEnabled || enemy == null || enemy.IsDead || _hitEnemies.Contains(enemy))
        {
            return false;
        }

        if (!enemy.TryReceiveDamage(this, hitPoint))
        {
            return false;
        }

        _hitEnemies.Add(enemy);
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
