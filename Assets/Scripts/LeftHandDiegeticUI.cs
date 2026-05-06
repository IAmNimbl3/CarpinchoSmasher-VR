using UnityEngine;
using UnityEngine.UI;

public class LeftHandDiegeticUI : MonoBehaviour
{
    public enum TrackedHand
    {
        Left,
        Right
    }

    public enum ActivationMode
    {
        Raised,
        Fist,
        RaisedAndFist
    }

    [Header("Placeholder Values")]
    [SerializeField] private int health = 100;
    [SerializeField] private int coins = 0;

    [Header("Meta XR Anchors")]
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private TrackedHand trackedHand = TrackedHand.Left;
    [SerializeField] private ActivationMode activationMode = ActivationMode.Fist;
    [SerializeField] private Transform leftHandAnchor;
    [SerializeField] private Transform leftControllerAnchor;
    [SerializeField] private Transform rightHandAnchor;
    [SerializeField] private Transform rightControllerAnchor;
    [SerializeField] private Transform centerEyeAnchor;
    [SerializeField] private OVRHand leftHand;
    [SerializeField] private OVRHand rightHand;
    [SerializeField] private OVRSkeleton leftSkeleton;
    [SerializeField] private OVRSkeleton rightSkeleton;

    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text healthText;
    [SerializeField] private Text coinsText;
    [SerializeField] private Vector3 panelOffset = new Vector3(0.08f, 0.1f, 0.22f);
    [SerializeField] private float raisedHeightFromHead = -0.25f;
    [SerializeField] private float fistFingerStrengthThreshold = 0.72f;
    [SerializeField] private float fistTipToPalmRatio = 1.8f;
    [SerializeField, Range(1, 4)] private int requiredClosedFingers = 3;
    [SerializeField] private bool controllerGripCountsAsFist = true;
    [SerializeField] private float fadeSpeed = 10f;

    public int Health
    {
        get => health;
        set
        {
            health = value;
            RefreshText();
        }
    }

    public int Coins
    {
        get => coins;
        set
        {
            coins = value;
            RefreshText();
        }
    }

    private void Awake()
    {
        ResolveReferences();
        RefreshText();
        SetCanvasVisible(false, true);
    }

    private void LateUpdate()
    {
        ResolveReferences();

        Transform activeHandAnchor = GetActiveHandAnchor();
        if (activeHandAnchor == null || centerEyeAnchor == null)
        {
            SetCanvasVisible(false, false);
            return;
        }

        bool isRaised = activeHandAnchor.position.y > centerEyeAnchor.position.y + raisedHeightFromHead;
        bool isFist = IsSelectedHandFist();
        bool shouldShow = ShouldShow(isRaised, isFist);
        SetCanvasVisible(shouldShow, false);

        if (!shouldShow)
        {
            return;
        }

        PositionPanel(activeHandAnchor);
    }

    private void ResolveReferences()
    {
        if (cameraRig == null)
        {
            cameraRig = FindFirstObjectByType<OVRCameraRig>();
        }

        if (cameraRig == null)
        {
            return;
        }

        if (leftHandAnchor == null)
        {
            leftHandAnchor = cameraRig.leftHandAnchor;
        }

        if (leftControllerAnchor == null)
        {
            leftControllerAnchor = cameraRig.leftControllerAnchor;
        }

        if (rightHandAnchor == null)
        {
            rightHandAnchor = cameraRig.rightHandAnchor;
        }

        if (rightControllerAnchor == null)
        {
            rightControllerAnchor = cameraRig.rightControllerAnchor;
        }

        if (centerEyeAnchor == null)
        {
            centerEyeAnchor = cameraRig.centerEyeAnchor;
        }

        ResolveHands();
    }

    private void ResolveHands()
    {
        if (HasUsableHand(leftHand) && HasUsableHand(rightHand)
            && IsUsableSkeleton(leftSkeleton) && IsUsableSkeleton(rightSkeleton))
        {
            return;
        }

        OVRHand[] hands = FindObjectsByType<OVRHand>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (OVRHand hand in hands)
        {
            if (hand == null || !hand.isActiveAndEnabled)
            {
                continue;
            }

            OVRPlugin.Hand handType = hand.GetHand();
            OVRSkeleton skeleton = hand.GetComponent<OVRSkeleton>();
            if (handType == OVRPlugin.Hand.HandLeft && IsBetterHandCandidate(hand, leftHand))
            {
                leftHand = hand;
                leftSkeleton = skeleton;
            }
            else if (handType == OVRPlugin.Hand.HandRight && IsBetterHandCandidate(hand, rightHand))
            {
                rightHand = hand;
                rightSkeleton = skeleton;
            }
        }
    }

    private Transform GetActiveHandAnchor()
    {
        if (trackedHand == TrackedHand.Left)
        {
            if (HasUsableHand(leftHand) && leftHandAnchor != null)
            {
                return leftHandAnchor;
            }

            if (leftControllerAnchor != null)
            {
                return leftControllerAnchor;
            }

            return leftHandAnchor != null ? leftHandAnchor : leftControllerAnchor;
        }

        if (HasUsableHand(rightHand) && rightHandAnchor != null)
        {
            return rightHandAnchor;
        }

        if (rightControllerAnchor != null)
        {
            return rightControllerAnchor;
        }

        return rightHandAnchor != null ? rightHandAnchor : rightControllerAnchor;
    }

    private bool ShouldShow(bool isRaised, bool isFist)
    {
        switch (activationMode)
        {
            case ActivationMode.Raised:
                return isRaised;
            case ActivationMode.Fist:
                return isFist;
            case ActivationMode.RaisedAndFist:
                return isRaised && isFist;
            default:
                return false;
        }
    }

    private bool IsSelectedHandFist()
    {
        OVRHand hand = trackedHand == TrackedHand.Left ? leftHand : rightHand;
        OVRSkeleton skeleton = trackedHand == TrackedHand.Left ? leftSkeleton : rightSkeleton;
        if (HasUsableHand(hand))
        {
            int closedFingers = CountClosedFingersFromSkeleton(skeleton);
            if (closedFingers >= requiredClosedFingers)
            {
                return true;
            }

            return CountClosedFingersFromPinch(hand) >= requiredClosedFingers;
        }

        if (!controllerGripCountsAsFist)
        {
            return false;
        }

        OVRInput.Controller controller = trackedHand == TrackedHand.Left
            ? OVRInput.Controller.LTouch
            : OVRInput.Controller.RTouch;

        return OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, controller) >= fistFingerStrengthThreshold;
    }

    private bool HasUsableHand(OVRHand hand)
    {
        return hand != null && hand.isActiveAndEnabled && hand.IsTracked && hand.IsDataValid;
    }

    private bool IsUsableSkeleton(OVRSkeleton skeleton)
    {
        return skeleton != null && skeleton.isActiveAndEnabled && skeleton.IsInitialized && skeleton.IsDataValid;
    }

    private bool IsBetterHandCandidate(OVRHand candidate, OVRHand current)
    {
        if (current == null)
        {
            return true;
        }

        if (HasUsableHand(candidate) && !HasUsableHand(current))
        {
            return true;
        }

        return candidate.GetComponent<OVRSkeleton>() != null && current.GetComponent<OVRSkeleton>() == null;
    }

    private int CountClosedFingersFromSkeleton(OVRSkeleton skeleton)
    {
        if (!IsUsableSkeleton(skeleton))
        {
            return 0;
        }

        Transform wrist = FindBone(skeleton, OVRSkeleton.BoneId.Hand_WristRoot);
        if (wrist == null)
        {
            return 0;
        }

        int closedFingers = 0;
        if (IsFingerClosed(skeleton, wrist, OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.Hand_IndexTip))
        {
            closedFingers++;
        }

        if (IsFingerClosed(skeleton, wrist, OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.Hand_MiddleTip))
        {
            closedFingers++;
        }

        if (IsFingerClosed(skeleton, wrist, OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.Hand_RingTip))
        {
            closedFingers++;
        }

        if (IsFingerClosed(skeleton, wrist, OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.Hand_PinkyTip))
        {
            closedFingers++;
        }

        return closedFingers;
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
        if (baseDistance <= 0.001f)
        {
            return false;
        }

        return tipDistance / baseDistance <= fistTipToPalmRatio;
    }

    private Transform FindBone(OVRSkeleton skeleton, OVRSkeleton.BoneId boneId)
    {
        if (skeleton.Bones == null)
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

    private int CountClosedFingersFromPinch(OVRHand hand)
    {
        int closedFingers = 0;
        if (hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) >= fistFingerStrengthThreshold)
        {
            closedFingers++;
        }

        if (hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle) >= fistFingerStrengthThreshold)
        {
            closedFingers++;
        }

        if (hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring) >= fistFingerStrengthThreshold)
        {
            closedFingers++;
        }

        if (hand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky) >= fistFingerStrengthThreshold)
        {
            closedFingers++;
        }

        return closedFingers;
    }

    private void PositionPanel(Transform activeLeftAnchor)
    {
        Transform panel = transform;
        panel.position = activeLeftAnchor.position
            + centerEyeAnchor.right * panelOffset.x
            + Vector3.up * panelOffset.y
            + centerEyeAnchor.forward * panelOffset.z;

        Vector3 lookDirection = panel.position - centerEyeAnchor.position;
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            panel.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }
    }

    private void SetCanvasVisible(bool visible, bool instant)
    {
        if (canvasGroup == null)
        {
            return;
        }

        float targetAlpha = visible ? 1f : 0f;
        canvasGroup.alpha = instant
            ? targetAlpha
            : Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);

        bool interactable = canvasGroup.alpha > 0.95f;
        canvasGroup.interactable = interactable;
        canvasGroup.blocksRaycasts = interactable;
    }

    private void RefreshText()
    {
        if (healthText != null)
        {
            healthText.text = "Vida: " + health.ToString();
        }

        if (coinsText != null)
        {
            coinsText.text = "Monedas: " + coins.ToString();
        }
    }
}
