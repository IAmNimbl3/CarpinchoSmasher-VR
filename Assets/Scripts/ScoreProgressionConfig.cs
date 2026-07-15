using UnityEngine;

public enum TrophyId
{
    Velocista,
    Sniper,
    Paracaidista,
    Juggernaut,
    Silver,
    Platinum
}

[CreateAssetMenu(fileName = "ScoreProgressionConfig", menuName = "Carpincho Smasher/Score Progression Config")]
public class ScoreProgressionConfig : ScriptableObject
{
    [Header("Scoring")]
    [SerializeField, Min(0)] private int defaultScorePerKill = 10;

    [Header("Kill trophies")]
    [SerializeField, Min(1)] private int velocistaKillsRequired = 10;
    [SerializeField, Min(1)] private int sniperKillsRequired = 10;
    [SerializeField, Min(1)] private int paracaidistaKillsRequired = 10;
    [SerializeField, Min(1)] private int juggernautKillsRequired = 10;

    [Header("Score trophies")]
    [SerializeField, Min(1)] private int silverScoreRequired = 500;
    [SerializeField, Min(1)] private int platinumScoreRequired = 1000;

    public int DefaultScorePerKill => defaultScorePerKill;
    public int SilverScoreRequired => silverScoreRequired;
    public int PlatinumScoreRequired => platinumScoreRequired;

    public int GetKillRequirement(CarpinchoType type)
    {
        return type switch
        {
            CarpinchoType.Velocista => velocistaKillsRequired,
            CarpinchoType.Sniper => sniperKillsRequired,
            CarpinchoType.Paracaidista => paracaidistaKillsRequired,
            CarpinchoType.Juggernaut => juggernautKillsRequired,
            _ => int.MaxValue
        };
    }
}
