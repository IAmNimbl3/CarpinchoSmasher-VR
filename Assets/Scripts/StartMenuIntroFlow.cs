using System.Collections;
using UnityEngine;

public class StartMenuIntroFlow : MonoBehaviour
{
    [Header("Rig")]
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private Transform centerEyeAnchor;

    [Header("Intro")]
    [SerializeField] private Transform initialSpawnPoint;
    [SerializeField] private GameObject introLogoRoot;

    [Header("Menu Activation")]
    [SerializeField] private Transform tvTeleportPoint;
    [SerializeField] private GameObject tvMenuRoot;
    [SerializeField, Min(0.05f)] private float activationRadius = 0.8f;
    [SerializeField] private bool hideIntroLogoWhenMenuActivates = true;

    private bool _menuActivated;

    private IEnumerator Start()
    {
        ResolveRigReferences();

        if (tvMenuRoot != null)
        {
            tvMenuRoot.SetActive(false);
        }

        yield return null;

        ResolveRigReferences();
        MoveRigEyeTo(initialSpawnPoint);
    }

    private void Update()
    {
        if (_menuActivated || tvTeleportPoint == null || tvMenuRoot == null)
        {
            return;
        }

        ResolveRigReferences();

        if (centerEyeAnchor == null)
        {
            return;
        }

        Vector2 eyePosition = new Vector2(centerEyeAnchor.position.x, centerEyeAnchor.position.z);
        Vector2 targetPosition = new Vector2(tvTeleportPoint.position.x, tvTeleportPoint.position.z);

        if (Vector2.Distance(eyePosition, targetPosition) <= activationRadius)
        {
            ActivateTvMenu();
        }
    }

    private void ActivateTvMenu()
    {
        _menuActivated = true;
        tvMenuRoot.SetActive(true);
        GameAudioEvents.RaiseMenuTeleported(tvTeleportPoint.position);

        if (hideIntroLogoWhenMenuActivates && introLogoRoot != null)
        {
            introLogoRoot.SetActive(false);
        }
    }

    private void ResolveRigReferences()
    {
        if (cameraRig == null)
        {
            cameraRig = FindAnyObjectByType<OVRCameraRig>();
        }

        if (centerEyeAnchor == null && cameraRig != null)
        {
            centerEyeAnchor = cameraRig.centerEyeAnchor;
        }
    }

    private void MoveRigEyeTo(Transform target)
    {
        if (cameraRig == null || centerEyeAnchor == null || target == null)
        {
            return;
        }

        Transform rigTransform = cameraRig.transform;
        Vector3 eyeForward = Vector3.ProjectOnPlane(centerEyeAnchor.forward, Vector3.up).normalized;
        Vector3 targetForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;

        if (eyeForward.sqrMagnitude > 0.001f && targetForward.sqrMagnitude > 0.001f)
        {
            float yawDelta = Vector3.SignedAngle(eyeForward, targetForward, Vector3.up);
            rigTransform.RotateAround(centerEyeAnchor.position, Vector3.up, yawDelta);
        }

        Vector3 eyeOffsetFromRig = centerEyeAnchor.position - rigTransform.position;
        rigTransform.position = target.position - eyeOffsetFromRig;
    }
}
