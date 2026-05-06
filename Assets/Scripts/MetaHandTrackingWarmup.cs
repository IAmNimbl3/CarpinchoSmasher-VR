using System.Collections;
using UnityEngine;

public class MetaHandTrackingWarmup : MonoBehaviour
{
    [SerializeField] private float warmupDuration = 3f;
    [SerializeField] private float retryInterval = 0.25f;
    [SerializeField] private bool logWarmup = true;

    private Coroutine _warmupRoutine;

    private void OnEnable()
    {
        OVRManager.InputFocusAcquired += HandleInputFocusAcquired;
        StartWarmup("startup");
    }

    private void OnDisable()
    {
        OVRManager.InputFocusAcquired -= HandleInputFocusAcquired;
    }

    private void HandleInputFocusAcquired()
    {
        StartWarmup("focus acquired");
    }

    private void StartWarmup(string reason)
    {
        if (_warmupRoutine != null)
        {
            StopCoroutine(_warmupRoutine);
        }

        _warmupRoutine = StartCoroutine(WarmupHands(reason));
    }

    private IEnumerator WarmupHands(string reason)
    {
        float endTime = Time.realtimeSinceStartup + warmupDuration;
        int attempts = 0;
        bool leftValid = false;
        bool rightValid = false;

        while (Time.realtimeSinceStartup < endTime)
        {
            attempts++;
            bool enabledMultimodal = OVRInput.EnableSimultaneousHandsAndControllers();
            leftValid |= TryReadHand(OVRPlugin.Hand.HandLeft);
            rightValid |= TryReadHand(OVRPlugin.Hand.HandRight);

            if (logWarmup)
            {
                Debug.Log(
                    $"[MetaHandTrackingWarmup] {reason} attempt={attempts} " +
                    $"multimodal={enabledMultimodal} leftValid={leftValid} rightValid={rightValid} " +
                    $"inputFocus={OVRManager.hasInputFocus}");
            }

            if (leftValid || rightValid)
            {
                break;
            }

            yield return new WaitForSecondsRealtime(retryInterval);
        }

        _warmupRoutine = null;
    }

    private bool TryReadHand(OVRPlugin.Hand hand)
    {
        OVRPlugin.HandState handState = default;
        if (!OVRPlugin.GetHandState(OVRPlugin.Step.Render, hand, ref handState))
        {
            return false;
        }

        return (handState.Status & OVRPlugin.HandStatus.HandTracked) != 0;
    }
}
