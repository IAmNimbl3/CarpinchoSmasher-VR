using UnityEngine;

public class HammerHandTrackingGrab : MonoBehaviour
{
    [Header("Meta XR")]
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private OVRHand leftHand;
    [SerializeField] private OVRHand rightHand;
    [SerializeField] private OVRSkeleton leftSkeleton;
    [SerializeField] private OVRSkeleton rightSkeleton;
    [SerializeField] private Transform leftHandAnchor;
    [SerializeField] private Transform rightHandAnchor;

    [Header("Grab")]
    [SerializeField] private float grabRadius = 0.18f;
    [SerializeField] private float pinchGrabThreshold = 0.72f;
    [SerializeField] private float pinchReleaseThreshold = 0.35f;
    [SerializeField] private float fistFingerThreshold = 0.70f;
    [SerializeField] private int requiredFistFingers = 3;
    [SerializeField] private float throwVelocityMultiplier = 1.15f;
    [SerializeField] private float maxThrowVelocity = 8f;

    [Header("Debug")]
    [SerializeField] private bool showDebugPanel = true;
    [SerializeField] private Vector3 debugPanelOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private float debugPanelCharacterSize = 0.035f;
    [SerializeField] private float debugUpdateInterval = 0.1f;

    private Rigidbody _rigidbody;
    private HammerRespawnLifecycle _lifecycle;
    private Collider[] _hammerColliders;
    private OVRHand _grabHand;
    private Vector3 _localGrabPosition;
    private Quaternion _localGrabRotation;
    private Vector3 _previousGrabPosition;
    private Vector3 _grabVelocity;
    private bool _isGrabbed;
    private TextMesh _debugText;
    private float _nextDebugUpdate;
    private string _leftDebug = "L: no sample";
    private string _rightDebug = "R: no sample";
    private string _lastEvent = "Idle";

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _lifecycle = GetComponent<HammerRespawnLifecycle>();
        _hammerColliders = GetComponentsInChildren<Collider>(true);
        ResolveReferences();
        EnsureDebugPanel();
    }

    private void Update()
    {
        ResolveReferences();

        if (_isGrabbed)
        {
            UpdateGrabbedHammer();
            return;
        }

        TryBeginGrab(leftHand, leftHandAnchor);
        if (!_isGrabbed)
        {
            TryBeginGrab(rightHand, rightHandAnchor);
        }

        UpdateDebugPanel();
    }

    private void ResolveReferences()
    {
        if (cameraRig == null)
        {
            cameraRig = FindFirstObjectByType<OVRCameraRig>();
        }

        if (cameraRig != null)
        {
            leftHandAnchor ??= cameraRig.leftHandAnchor;
            rightHandAnchor ??= cameraRig.rightHandAnchor;
        }

        OVRHand[] hands = FindObjectsByType<OVRHand>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (OVRHand hand in hands)
        {
            if (hand == null || !hand.isActiveAndEnabled)
            {
                continue;
            }

            if (hand.GetHand() == OVRPlugin.Hand.HandLeft && IsBetterHandCandidate(hand, leftHand))
            {
                leftHand = hand;
            }
            else if (hand.GetHand() == OVRPlugin.Hand.HandRight && IsBetterHandCandidate(hand, rightHand))
            {
                rightHand = hand;
            }
        }

        OVRSkeleton[] skeletons = FindObjectsByType<OVRSkeleton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (OVRSkeleton skeleton in skeletons)
        {
            if (skeleton == null || !skeleton.isActiveAndEnabled)
            {
                continue;
            }

            if (skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.HandLeft && IsBetterSkeletonCandidate(skeleton, leftSkeleton))
            {
                leftSkeleton = skeleton;
            }
            else if (skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.HandRight && IsBetterSkeletonCandidate(skeleton, rightSkeleton))
            {
                rightSkeleton = skeleton;
            }
        }
    }

    private void TryBeginGrab(OVRHand hand, Transform anchor)
    {
        OVRSkeleton skeleton = GetSkeletonForHand(hand);
        bool hasUsableHand = HasUsableHand(hand);
        bool hasPose = TryGetHandPose(hand, skeleton, anchor, out Vector3 handPosition, out Quaternion handRotation, out string poseSource);
        bool gestureActive = hasUsableHand && IsGrabGestureActive(hand);
        float distance = hasPose ? GetDistanceToHammerColliders(handPosition) : -1f;
        bool inRange = hasPose && distance <= grabRadius;

        SetHandDebug(hand, skeleton, hasUsableHand, hasPose, poseSource, gestureActive, distance, inRange);

        if (!hasUsableHand || !hasPose || !gestureActive)
        {
            return;
        }

        if (!inRange)
        {
            return;
        }

        _isGrabbed = true;
        _grabHand = hand;
        _localGrabPosition = Quaternion.Inverse(handRotation) * (transform.position - handPosition);
        _localGrabRotation = Quaternion.Inverse(handRotation) * transform.rotation;
        _previousGrabPosition = handPosition;
        _grabVelocity = Vector3.zero;
        _lastEvent = $"Grabbed {GetHandLabel(hand)} via {poseSource}";
        Debug.Log($"[HammerHandTrackingGrab] {_lastEvent}. Distance={distance:F2}");

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
        }
    }

    private void UpdateGrabbedHammer()
    {
        if (_grabHand == null)
        {
            ReleaseGrab();
            return;
        }

        Transform fallbackAnchor = _grabHand.GetHand() == OVRPlugin.Hand.HandLeft ? leftHandAnchor : rightHandAnchor;
        OVRSkeleton skeleton = GetSkeletonForHand(_grabHand);
        if (!TryGetHandPose(_grabHand, skeleton, fallbackAnchor, out Vector3 handPosition, out Quaternion handRotation, out string poseSource))
        {
            _lastEvent = $"Released {GetHandLabel(_grabHand)}: pose lost";
            ReleaseGrab();
            return;
        }

        if (Time.deltaTime > 0f)
        {
            _grabVelocity = (handPosition - _previousGrabPosition) / Time.deltaTime;
        }

        _previousGrabPosition = handPosition;
        transform.SetPositionAndRotation(
            handPosition + handRotation * _localGrabPosition,
            handRotation * _localGrabRotation);

        if (!HasUsableHand(_grabHand) || IsGrabGestureReleased(_grabHand))
        {
            _lastEvent = $"Released {GetHandLabel(_grabHand)}: gesture open";
            ReleaseGrab();
        }

        UpdateDebugPanel();
    }

    private void ReleaseGrab()
    {
        _isGrabbed = false;
        _grabHand = null;

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = Vector3.ClampMagnitude(_grabVelocity * throwVelocityMultiplier, maxThrowVelocity);
        }

        _lifecycle?.NotifyManualRelease();
    }

    private bool HasUsableHand(OVRHand hand)
    {
        return hand != null && hand.isActiveAndEnabled && hand.IsTracked && hand.IsDataValid;
    }

    private bool TryGetHandPose(
        OVRHand hand,
        OVRSkeleton skeleton,
        Transform fallbackAnchor,
        out Vector3 position,
        out Quaternion rotation,
        out string source)
    {
        Transform wrist = FindBone(skeleton, OVRSkeleton.BoneId.Hand_WristRoot);
        if (wrist != null)
        {
            position = wrist.position;
            rotation = wrist.rotation;
            source = "wrist";
            return true;
        }

        if (fallbackAnchor != null)
        {
            position = fallbackAnchor.position;
            rotation = fallbackAnchor.rotation;
            source = "anchor";
            return true;
        }

        position = default;
        rotation = default;
        source = "none";
        return false;
    }

    private bool IsGrabGestureActive(OVRHand hand)
    {
        OVRSkeleton skeleton = GetSkeletonForHand(hand);
        return hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) >= pinchGrabThreshold
            || CountClosedFingers(hand, skeleton) >= requiredFistFingers;
    }

    private bool IsGrabGestureReleased(OVRHand hand)
    {
        OVRSkeleton skeleton = GetSkeletonForHand(hand);
        return hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) <= pinchReleaseThreshold
            && CountClosedFingers(hand, skeleton) < requiredFistFingers;
    }

    private int CountClosedFingers(OVRHand hand, OVRSkeleton skeleton)
    {
        int skeletonCount = CountClosedFingersFromSkeleton(skeleton);
        if (skeletonCount > 0)
        {
            return skeletonCount;
        }

        int closed = 0;
        if (hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) >= fistFingerThreshold) closed++;
        if (hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle) >= fistFingerThreshold) closed++;
        if (hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring) >= fistFingerThreshold) closed++;
        if (hand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky) >= fistFingerThreshold) closed++;
        return closed;
    }

    private bool IsBetterHandCandidate(OVRHand candidate, OVRHand current)
    {
        if (current == null)
        {
            return true;
        }

        return GetHandCandidateScore(candidate) > GetHandCandidateScore(current);
    }

    private int GetHandCandidateScore(OVRHand hand)
    {
        if (hand == null)
        {
            return -1;
        }

        int score = 0;
        if (HasUsableHand(hand)) score += 100;
        if (hand.name.Contains("DataSource")) score += 30;
        if (hand.IsPointerPoseValid && hand.PointerPose != null) score += 20;
        if (hand.GetComponent<OVRSkeleton>() != null) score += 5;
        return score;
    }

    private OVRSkeleton GetSkeletonForHand(OVRHand hand)
    {
        if (hand == null)
        {
            return null;
        }

        return hand.GetHand() == OVRPlugin.Hand.HandLeft ? leftSkeleton : rightSkeleton;
    }

    private bool IsBetterSkeletonCandidate(OVRSkeleton candidate, OVRSkeleton current)
    {
        if (current == null)
        {
            return true;
        }

        return GetSkeletonCandidateScore(candidate) > GetSkeletonCandidateScore(current);
    }

    private int GetSkeletonCandidateScore(OVRSkeleton skeleton)
    {
        if (skeleton == null)
        {
            return -1;
        }

        int score = 0;
        if (skeleton.IsInitialized) score += 20;
        if (skeleton.IsDataValid) score += 50;
        if (skeleton.Bones != null && skeleton.Bones.Count > 0) score += 10;
        return score;
    }

    private int CountClosedFingersFromSkeleton(OVRSkeleton skeleton)
    {
        Transform wrist = FindBone(skeleton, OVRSkeleton.BoneId.Hand_WristRoot);
        if (wrist == null)
        {
            return 0;
        }

        int closed = 0;
        if (IsFingerClosed(skeleton, wrist, OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.Hand_IndexTip)) closed++;
        if (IsFingerClosed(skeleton, wrist, OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.Hand_MiddleTip)) closed++;
        if (IsFingerClosed(skeleton, wrist, OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.Hand_RingTip)) closed++;
        if (IsFingerClosed(skeleton, wrist, OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.Hand_PinkyTip)) closed++;
        return closed;
    }

    private bool IsFingerClosed(
        OVRSkeleton skeleton,
        Transform wrist,
        OVRSkeleton.BoneId baseBoneId,
        OVRSkeleton.BoneId tipBoneId)
    {
        Transform baseBone = FindBone(skeleton, baseBoneId);
        Transform tipBone = FindBone(skeleton, tipBoneId);
        if (baseBone == null || tipBone == null)
        {
            return false;
        }

        float baseDistance = Vector3.Distance(baseBone.position, wrist.position);
        float tipDistance = Vector3.Distance(tipBone.position, wrist.position);
        return baseDistance > 0.001f && tipDistance / baseDistance <= 1.7f;
    }

    private Transform FindBone(OVRSkeleton skeleton, OVRSkeleton.BoneId boneId)
    {
        if (skeleton == null || skeleton.Bones == null)
        {
            return null;
        }

        foreach (OVRBone bone in skeleton.Bones)
        {
            if (bone.Id == boneId)
            {
                return bone.Transform;
            }
        }

        return null;
    }

    private void EnsureDebugPanel()
    {
        if (!showDebugPanel || _debugText != null)
        {
            return;
        }

        GameObject debugObject = new GameObject("Hammer Hand Debug");
        debugObject.transform.SetParent(transform, false);
        debugObject.transform.localPosition = debugPanelOffset;
        _debugText = debugObject.AddComponent<TextMesh>();
        _debugText.anchor = TextAnchor.MiddleCenter;
        _debugText.alignment = TextAlignment.Center;
        _debugText.characterSize = debugPanelCharacterSize;
        _debugText.fontSize = 48;
        _debugText.color = Color.yellow;
        _debugText.text = "Hammer hand debug";
    }

    private void UpdateDebugPanel()
    {
        if (!showDebugPanel)
        {
            if (_debugText != null)
            {
                _debugText.gameObject.SetActive(false);
            }

            return;
        }

        EnsureDebugPanel();
        if (_debugText == null || Time.time < _nextDebugUpdate)
        {
            return;
        }

        _nextDebugUpdate = Time.time + debugUpdateInterval;
        _debugText.gameObject.SetActive(true);
        if (cameraRig != null && cameraRig.centerEyeAnchor != null)
        {
            Vector3 lookDirection = _debugText.transform.position - cameraRig.centerEyeAnchor.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                _debugText.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }

        _debugText.text =
            $"Hammer hand debug\n" +
            $"state: {(_isGrabbed ? "GRABBED" : "waiting")} | {_lastEvent}\n" +
            $"{_leftDebug}\n" +
            $"{_rightDebug}\n" +
            $"radius: {grabRadius:F2}";
    }

    private float GetDistanceToHammerColliders(Vector3 point)
    {
        if (_hammerColliders == null || _hammerColliders.Length == 0)
        {
            return Vector3.Distance(point, transform.position);
        }

        float closestDistance = float.PositiveInfinity;
        foreach (Collider hammerCollider in _hammerColliders)
        {
            if (hammerCollider == null || !hammerCollider.enabled)
            {
                continue;
            }

            Vector3 closestPoint = hammerCollider.ClosestPoint(point);
            float distance = Vector3.Distance(point, closestPoint);
            if (distance < closestDistance)
            {
                closestDistance = distance;
            }
        }

        return float.IsPositiveInfinity(closestDistance)
            ? Vector3.Distance(point, transform.position)
            : closestDistance;
    }

    private void SetHandDebug(
        OVRHand hand,
        OVRSkeleton skeleton,
        bool hasUsableHand,
        bool hasPose,
        string poseSource,
        bool gestureActive,
        float distance,
        bool inRange)
    {
        string label = GetHandLabel(hand);
        string handName = hand != null ? hand.name : "null";
        float indexPinch = hand != null ? hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) : 0f;
        int closedFingers = hand != null ? CountClosedFingers(hand, skeleton) : 0;
        string message =
            $"{label}: {handName} tracked={hasUsableHand} pose={hasPose}:{poseSource} " +
            $"gesture={gestureActive} pinch={indexPinch:F2} closed={closedFingers} " +
            $"colliderDist={(distance >= 0f ? distance.ToString("F2") : "--")} inRange={inRange}";

        if (hand != null && hand.GetHand() == OVRPlugin.Hand.HandLeft)
        {
            _leftDebug = message;
        }
        else if (hand != null && hand.GetHand() == OVRPlugin.Hand.HandRight)
        {
            _rightDebug = message;
        }
    }

    private string GetHandLabel(OVRHand hand)
    {
        if (hand == null)
        {
            return "?";
        }

        return hand.GetHand() == OVRPlugin.Hand.HandLeft ? "L" : "R";
    }
}
