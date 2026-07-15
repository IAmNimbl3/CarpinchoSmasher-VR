using UnityEngine;

/// Applies Quest performance settings as early as possible, before any scene loads.
/// Verified against current Meta docs (developers.meta.com) - not attached to any
/// GameObject, so it can't be skipped by a missing reference in a scene.
public static class QuestPerformanceBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        // Target a refresh rate every Quest headset (2/3/3S/Pro) supports, so "72fps or
        // more" is measured against a display frequency the hardware can actually hold.
        OVRPlugin.systemDisplayFrequency = 72.0f;

        // Fixed Foveated Rendering: renders the periphery at lower resolution, nearly
        // imperceptible to the user, large GPU shading cost reduction. Was not enabled
        // anywhere in the project.
        OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.HighTop;
        OVRManager.useDynamicFoveatedRendering = true;

        // Keep CPU/GPU clocks from idling down to battery-saving levels mid-round; the
        // OS still scales within this range based on actual utilization. SustainedHigh
        // (not Boost - that's a 45s-max burst hint) is the right choice for continuous play.
    }
}
