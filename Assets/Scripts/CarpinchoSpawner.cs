using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class CarpinchoSpawner : MonoBehaviour
{
    [Serializable]
    public class PrefabEntry
    {
        [Tooltip("Prefab del carpincho. El tipo se lee desde el componente Enemy del prefab (subclase).")]
        public Enemy prefab;
        [Min(0)] public int prewarm = 2;
        [Min(1)] public int defaultCapacity = 4;
        [Min(1)] public int maxSize = 16;
    }

    [Serializable]
    public class SpawnPhase
    {
        public string label = "Phase";
        [Tooltip("Tiempo (segundos desde BeginSpawning) en el que arranca esta fase.")]
        [Min(0f)] public float startTime;
        [Tooltip("Intervalo entre spawns dentro de la fase.")]
        [Min(0.05f)] public float spawnInterval = 2f;
        [Tooltip("Máximo de enemigos vivos simultáneos durante la fase.")]
        [Min(1)] public int maxAlive = 6;
        [Tooltip("Tipos permitidos durante la fase. El spawner elige uno al azar.")]
        public CarpinchoType[] allowedTypes = new CarpinchoType[0];
    }

    [Header("Pools")]
    [SerializeField] private PrefabEntry[] prefabs;
    [Tooltip("Padre para los enemigos instanciados. Si es null, se crea uno automáticamente.")]
    [SerializeField] private Transform pooledRoot;

    [Header("Spawn points")]
    [Tooltip("Si está vacío, se buscan automáticamente componentes SpawnPoint en la escena.")]
    [SerializeField] private SpawnPoint[] spawnPoints;

    [Header("Phases (ver GDD §11)")]
    [SerializeField]
    private SpawnPhase[] phases = new SpawnPhase[]
    {
        new SpawnPhase
        {
            label = "1 · Intro (0-30s)",
            startTime = 0f,
            spawnInterval = 3f,
            maxAlive = 3,
            allowedTypes = new[] { CarpinchoType.Sniper, CarpinchoType.Velocista }
        },
        new SpawnPhase
        {
            label = "2 · Aire (30s-2m)",
            startTime = 30f,
            spawnInterval = 2f,
            maxAlive = 5,
            allowedTypes = new[] { CarpinchoType.Sniper, CarpinchoType.Velocista, CarpinchoType.Paracaidista }
        },
        new SpawnPhase
        {
            label = "3 · Tanque (2m-3:30)",
            startTime = 120f,
            spawnInterval = 1.5f,
            maxAlive = 7,
            allowedTypes = new[] { CarpinchoType.Sniper, CarpinchoType.Velocista, CarpinchoType.Paracaidista, CarpinchoType.Juggernaut }
        },
        new SpawnPhase
        {
            label = "4 · Caos (3:30+)",
            startTime = 210f,
            spawnInterval = 1f,
            maxAlive = 10,
            allowedTypes = new[] { CarpinchoType.Sniper, CarpinchoType.Velocista, CarpinchoType.Paracaidista, CarpinchoType.Juggernaut }
        }
    };

    [Header("Lifecycle")]
    [SerializeField] private bool autoStart = true;
    [Tooltip("Duracion de la ultima fase antes de cerrar la ronda final. Al llegar a este tiempo deja de spawnear y espera a que mueran los vivos.")]
    [SerializeField, Min(0f)] private float finalPhaseDuration = 60f;

    public static CarpinchoSpawner Instance { get; private set; }

    public event Action<Enemy> EnemySpawned;
    public event Action<int> PhaseChanged;

    public IReadOnlyList<Enemy> AliveEnemies => _aliveEnemies;
    public int CurrentPhaseIndex => _phaseIndex;
    public float ElapsedTime => _elapsedTime;
    public bool IsRunning => _running;
    public bool IsPaused => _paused;
    public int PhaseCount => phases != null ? phases.Length : 0;

    private readonly Dictionary<CarpinchoType, EnemyPool> _poolsByType = new Dictionary<CarpinchoType, EnemyPool>();
    private readonly List<Enemy> _aliveEnemies = new List<Enemy>();
    private readonly List<SpawnPoint> _spawnPointBuffer = new List<SpawnPoint>();

    private float _elapsedTime;
    private float _nextSpawnAt;
    private bool _running;
    private bool _paused;
    private int _phaseIndex = -1;

    private void Awake()
    {
        Instance = this;

        if (pooledRoot == null)
        {
            var root = new GameObject("CarpinchoPool");
            root.transform.SetParent(transform, false);
            pooledRoot = root.transform;
        }

        BuildPools();
        EnsureSpawnPoints();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (autoStart)
        {
            BeginSpawning();
        }
    }

    public void BeginSpawning()
    {
        _running = true;
        _paused = false;
        _elapsedTime = 0f;
        _nextSpawnAt = 0f;
        _phaseIndex = -1;
    }

    public void StopSpawning()
    {
        _running = false;
        _paused = false;
    }

    public void PauseSpawning()
    {
        if (!_running)
        {
            return;
        }

        _paused = true;
    }

    public void ResumeSpawning()
    {
        if (!_running)
        {
            return;
        }

        _paused = false;
        _nextSpawnAt = Mathf.Max(_nextSpawnAt, _elapsedTime);
    }

    public void RestartCurrentPhase()
    {
        int phaseIndex = Mathf.Max(0, _phaseIndex);
        RestartPhase(phaseIndex);
    }

    public void RestartPhase(int phaseIndex)
    {
        if (phases == null || phases.Length == 0)
        {
            BeginSpawning();
            return;
        }

        phaseIndex = Mathf.Clamp(phaseIndex, 0, phases.Length - 1);
        DespawnAll();

        _running = true;
        _paused = false;
        _phaseIndex = phaseIndex;
        _elapsedTime = phases[phaseIndex] != null ? phases[phaseIndex].startTime : 0f;
        _nextSpawnAt = _elapsedTime;
    }

    public string GetPhaseLabel(int phaseIndex)
    {
        if (phases == null || phaseIndex < 0 || phaseIndex >= phases.Length)
        {
            return string.Empty;
        }

        return phases[phaseIndex] != null ? phases[phaseIndex].label : string.Empty;
    }

    public void DespawnAll()
    {
        for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = _aliveEnemies[i];
            if (enemy != null && _poolsByType.TryGetValue(enemy.Type, out EnemyPool pool))
            {
                pool.Release(enemy);
            }
        }

        _aliveEnemies.Clear();
    }

    private void Update()
    {
        if (!_running || _paused)
        {
            return;
        }

        _elapsedTime += Time.deltaTime;
        UpdatePhase();

        if (_paused)
        {
            return;
        }

        SpawnPhase phase = CurrentPhase;
        if (phase == null)
        {
            return;
        }

        PruneDeadEnemies();

        if (_aliveEnemies.Count >= phase.maxAlive)
        {
            return;
        }

        if (_elapsedTime >= _nextSpawnAt)
        {
            TrySpawn(phase);
            _nextSpawnAt = _elapsedTime + phase.spawnInterval;
        }
    }

    private SpawnPhase CurrentPhase =>
        (_phaseIndex >= 0 && phases != null && _phaseIndex < phases.Length) ? phases[_phaseIndex] : null;

    private void UpdatePhase()
    {
        if (phases == null || phases.Length == 0)
        {
            return;
        }

        int newIndex = -1;
        for (int i = 0; i < phases.Length; i++)
        {
            if (_elapsedTime >= phases[i].startTime)
            {
                newIndex = i;
            }
            else
            {
                break;
            }
        }

        if (finalPhaseDuration > 0f
            && newIndex == phases.Length - 1
            && _elapsedTime >= phases[newIndex].startTime + finalPhaseDuration)
        {
            newIndex = phases.Length;
        }

        if (newIndex != _phaseIndex)
        {
            _phaseIndex = newIndex;
            _nextSpawnAt = _elapsedTime;
            PhaseChanged?.Invoke(_phaseIndex);
        }
    }

    private void PruneDeadEnemies()
    {
        for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = _aliveEnemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.IsDead)
            {
                _aliveEnemies.RemoveAt(i);
            }
        }
    }

    private void TrySpawn(SpawnPhase phase)
    {
        if (phase.allowedTypes == null || phase.allowedTypes.Length == 0)
        {
            return;
        }

        CarpinchoType type = phase.allowedTypes[Random.Range(0, phase.allowedTypes.Length)];

        if (!_poolsByType.TryGetValue(type, out EnemyPool pool))
        {
            return;
        }

        SpawnPoint point = PickSpawnPoint(type);
        if (point == null)
        {
            return;
        }

        Enemy enemy = pool.Get(point.GetRandomPosition(), point.transform.rotation);
        _aliveEnemies.Add(enemy);
        EnemySpawned?.Invoke(enemy);
    }

    private SpawnPoint PickSpawnPoint(CarpinchoType type)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }

        _spawnPointBuffer.Clear();
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            SpawnPoint candidate = spawnPoints[i];
            if (candidate != null && candidate.Accepts(type))
            {
                _spawnPointBuffer.Add(candidate);
            }
        }

        if (_spawnPointBuffer.Count == 0)
        {
            return null;
        }

        return _spawnPointBuffer[Random.Range(0, _spawnPointBuffer.Count)];
    }

    private void BuildPools()
    {
        _poolsByType.Clear();

        if (prefabs == null)
        {
            return;
        }

        for (int i = 0; i < prefabs.Length; i++)
        {
            PrefabEntry entry = prefabs[i];
            if (entry == null || entry.prefab == null)
            {
                continue;
            }

            CarpinchoType type = entry.prefab.Type;
            if (_poolsByType.ContainsKey(type))
            {
                Debug.LogWarning($"[CarpinchoSpawner] Tipo duplicado en prefabs: {type}. Se ignora la entrada repetida.", this);
                continue;
            }

            var pool = new EnemyPool(entry.prefab, pooledRoot, entry.defaultCapacity, entry.maxSize);
            pool.Prewarm(entry.prewarm);
            _poolsByType.Add(type, pool);
        }
    }

    private void EnsureSpawnPoints()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            return;
        }

        spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
    }
}
