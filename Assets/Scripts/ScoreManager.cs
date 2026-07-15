using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class ScoreManager : MonoBehaviour
{
    private const string SaveKey = "CarpinchoSmasher.ScoreProgress.v1";
    private const string ConfigResourceName = "ScoreProgressionConfig";

    [Serializable]
    private class ProgressSaveData
    {
        public int lifetimeScore;
        public int sniperKills;
        public int paracaidistaKills;
        public int velocistaKills;
        public int juggernautKills;
    }

    private static ScoreManager _instance;

    [SerializeField] private ScoreProgressionConfig progressionConfig;

    private readonly Dictionary<CarpinchoType, int> _lifetimeKills = new Dictionary<CarpinchoType, int>();
    private readonly Dictionary<CarpinchoType, int> _currentRunKills = new Dictionary<CarpinchoType, int>();

    public static ScoreManager Instance
    {
        get
        {
            EnsureInstance();
            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    public ScoreProgressionConfig ProgressionConfig => progressionConfig;
    public int LifetimeScore { get; private set; }
    public int CurrentRunScore { get; private set; }
    public IReadOnlyDictionary<CarpinchoType, int> LifetimeKills => _lifetimeKills;
    public IReadOnlyDictionary<CarpinchoType, int> CurrentRunKills => _currentRunKills;

    public event Action ProgressChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (_instance != null)
        {
            return;
        }

        _instance = FindAnyObjectByType<ScoreManager>();
        if (_instance != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("ScoreManager");
        _instance = managerObject.AddComponent<ScoreManager>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        ResolveConfig();
        InitializeCounters(_lifetimeKills);
        InitializeCounters(_currentRunKills);
        LoadProgress();
    }

    public void BeginNewRun()
    {
        CurrentRunScore = 0;
        InitializeCounters(_currentRunKills);
        ProgressChanged?.Invoke();
    }

    public void RegisterKill(CarpinchoType type, int scoreValue)
    {
        int fallbackScore = progressionConfig != null ? progressionConfig.DefaultScorePerKill : 10;
        int awardedScore = scoreValue > 0 ? scoreValue : fallbackScore;

        CurrentRunScore += awardedScore;
        LifetimeScore += awardedScore;
        _currentRunKills[type] = GetCurrentRunKills(type) + 1;
        _lifetimeKills[type] = GetLifetimeKills(type) + 1;

        SaveProgress();
        ProgressChanged?.Invoke();
    }

    public int GetCurrentRunKills(CarpinchoType type)
    {
        return _currentRunKills.TryGetValue(type, out int value) ? value : 0;
    }

    public int GetLifetimeKills(CarpinchoType type)
    {
        return _lifetimeKills.TryGetValue(type, out int value) ? value : 0;
    }

    public bool IsTrophyUnlocked(TrophyId trophy)
    {
        ResolveConfig();
        if (progressionConfig == null)
        {
            return false;
        }

        return trophy switch
        {
            TrophyId.Velocista => GetLifetimeKills(CarpinchoType.Velocista) >= progressionConfig.GetKillRequirement(CarpinchoType.Velocista),
            TrophyId.Sniper => GetLifetimeKills(CarpinchoType.Sniper) >= progressionConfig.GetKillRequirement(CarpinchoType.Sniper),
            TrophyId.Paracaidista => GetLifetimeKills(CarpinchoType.Paracaidista) >= progressionConfig.GetKillRequirement(CarpinchoType.Paracaidista),
            TrophyId.Juggernaut => GetLifetimeKills(CarpinchoType.Juggernaut) >= progressionConfig.GetKillRequirement(CarpinchoType.Juggernaut),
            TrophyId.Silver => LifetimeScore >= progressionConfig.SilverScoreRequired,
            TrophyId.Platinum => LifetimeScore >= progressionConfig.PlatinumScoreRequired,
            _ => false
        };
    }

    [ContextMenu("Reset Persistent Progress")]
    public void ResetPersistentProgress()
    {
        LifetimeScore = 0;
        CurrentRunScore = 0;
        InitializeCounters(_lifetimeKills);
        InitializeCounters(_currentRunKills);
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        ProgressChanged?.Invoke();
    }

    private void ResolveConfig()
    {
        if (progressionConfig != null)
        {
            return;
        }

        progressionConfig = Resources.Load<ScoreProgressionConfig>(ConfigResourceName);
        if (progressionConfig == null)
        {
            progressionConfig = ScriptableObject.CreateInstance<ScoreProgressionConfig>();
            progressionConfig.name = "Runtime Score Progression Config";
            progressionConfig.hideFlags = HideFlags.DontSave;
            Debug.LogWarning("[ScoreManager] ScoreProgressionConfig was not found in Resources; using defaults.", this);
        }
    }

    private void LoadProgress()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            return;
        }

        try
        {
            ProgressSaveData data = JsonUtility.FromJson<ProgressSaveData>(PlayerPrefs.GetString(SaveKey));
            if (data == null)
            {
                return;
            }

            LifetimeScore = Mathf.Max(0, data.lifetimeScore);
            _lifetimeKills[CarpinchoType.Sniper] = Mathf.Max(0, data.sniperKills);
            _lifetimeKills[CarpinchoType.Paracaidista] = Mathf.Max(0, data.paracaidistaKills);
            _lifetimeKills[CarpinchoType.Velocista] = Mathf.Max(0, data.velocistaKills);
            _lifetimeKills[CarpinchoType.Juggernaut] = Mathf.Max(0, data.juggernautKills);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[ScoreManager] Could not load saved progress: {exception.Message}", this);
        }
    }

    private void SaveProgress()
    {
        ProgressSaveData data = new ProgressSaveData
        {
            lifetimeScore = LifetimeScore,
            sniperKills = GetLifetimeKills(CarpinchoType.Sniper),
            paracaidistaKills = GetLifetimeKills(CarpinchoType.Paracaidista),
            velocistaKills = GetLifetimeKills(CarpinchoType.Velocista),
            juggernautKills = GetLifetimeKills(CarpinchoType.Juggernaut)
        };

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private static void InitializeCounters(Dictionary<CarpinchoType, int> counters)
    {
        counters.Clear();
        foreach (CarpinchoType type in Enum.GetValues(typeof(CarpinchoType)))
        {
            counters[type] = 0;
        }
    }
}
