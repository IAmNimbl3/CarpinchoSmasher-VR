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
        if (!damageEnabled || enemy == null || enemy.IsDead || _hitEnemies.Contains(enemy))
        {
            return false;
        }

        _hitEnemies.Add(enemy);
        enemy.Die();
        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDamage(other.GetComponentInParent<Enemy>());
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryDamage(collision.collider.GetComponentInParent<Enemy>());
    }
}
