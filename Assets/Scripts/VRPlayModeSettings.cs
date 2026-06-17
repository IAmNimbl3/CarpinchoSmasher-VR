public static class VRPlayModeSettings
{
    public static bool SeatedMode { get; private set; } = true;
    public static VRPlayModeConfig SeatedConfig { get; private set; }
    public static VRPlayModeConfig StandingConfig { get; private set; }
    public static VRPlayModeConfig ActiveConfig => SeatedMode ? SeatedConfig : StandingConfig;

    public static void RegisterConfigs(VRPlayModeConfig seatedConfig, VRPlayModeConfig standingConfig)
    {
        if (seatedConfig != null)
        {
            SeatedConfig = seatedConfig;
        }

        if (standingConfig != null)
        {
            StandingConfig = standingConfig;
        }
    }

    public static void SetSeatedMode(bool seatedMode)
    {
        SeatedMode = seatedMode;
    }

    public static void ToggleSeatedMode()
    {
        SeatedMode = !SeatedMode;
    }
}
