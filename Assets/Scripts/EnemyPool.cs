using UnityEngine;
using UnityEngine.Pool;

public class EnemyPool
{
    private readonly Enemy _prefab;
    private readonly Transform _parent;
    private readonly IObjectPool<Enemy> _pool;

    public EnemyPool(Enemy prefab, Transform parent, int defaultCapacity = 4, int maxSize = 16)
    {
        _prefab = prefab;
        _parent = parent;
        _pool = new ObjectPool<Enemy>(
            createFunc: CreateInstance,
            actionOnGet: null,
            actionOnRelease: HandleRelease,
            actionOnDestroy: DestroyInstance,
            collectionCheck: false,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize);
    }

    public Enemy Get(Vector3 position, Quaternion rotation)
    {
        Enemy enemy = _pool.Get();
        enemy.OnSpawned(position, rotation);
        return enemy;
    }

    public void Release(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        _pool.Release(enemy);
    }

    public void Prewarm(int count)
    {
        if (count <= 0)
        {
            return;
        }

        var buffer = new Enemy[count];
        for (int i = 0; i < count; i++)
        {
            buffer[i] = _pool.Get();
        }
        for (int i = 0; i < count; i++)
        {
            _pool.Release(buffer[i]);
        }
    }

    public void Clear() => _pool.Clear();

    private Enemy CreateInstance()
    {
        Enemy enemy = Object.Instantiate(_prefab, _parent);
        enemy.gameObject.SetActive(false);
        enemy.Died += HandleDied;
        return enemy;
    }

    private void HandleRelease(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        enemy.OnDespawned();
    }

    private void DestroyInstance(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        enemy.Died -= HandleDied;
        Object.Destroy(enemy.gameObject);
    }

    private void HandleDied(Enemy enemy)
    {
        Release(enemy);
    }
}
