using UnityEngine;

/// Applies Quest performance settings as early as possible, before any scene loads.
/// Verified against current Meta docs (developers.meta.com) - not attached to any
/// GameObject, so it can't be skipped by a missing reference in a scene.
public static class QuestPerformanceBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Apply()
    {
        // Uncap the refresh rate: the compositor always paces to a fixed display
        // frequency (there's no true "unlimited" fps in VR), so instead of pinning to
        // 72Hz, use whichever rate is highest among what this specific headset reports.
        float[] availableFrequencies = OVRManager.display != null
            ? OVRManager.display.displayFrequenciesAvailable
            : null;
        if (availableFrequencies != null && availableFrequencies.Length > 0)
        {
            float maxFrequency = availableFrequencies[0];
            for (int i = 1; i < availableFrequencies.Length; i++)
            {
                maxFrequency = Mathf.Max(maxFrequency, availableFrequencies[i]);
            }
            OVRPlugin.systemDisplayFrequency = maxFrequency;
        }

        // Fixed Foveated Rendering: renders the periphery at lower resolution, nearly
        // imperceptible to the user, large GPU shading cost reduction. Was not enabled
        // anywhere in the project.
        OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.HighTop;
        OVRManager.useDynamicFoveatedRendering = true;
    }
}
