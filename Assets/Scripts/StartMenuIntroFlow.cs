using System.Collections;
using UnityEngine;

public class StartMenuIntroFlow : MonoBehaviour
{
    [Header("Rig")]
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private Transform centerEyeAnchor;
    [SerializeField] private PlayerBounds playerBounds;

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
        ConfigurePlayerBounds();

        if (tvMenuRoot != null)
        {
            tvMenuRoot.SetActive(false);
        }

        yield return null;

        ResolveRigReferences();
        ConfigurePlayerBounds();
        MoveRigTo(initialSpawnPoint);
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

        if (playerBounds == null && cameraRig != null)
        {
            playerBounds = cameraRig.GetComponent<PlayerBounds>();
        }
    }

    private void ConfigurePlayerBounds()
    {
        if (playerBounds != null && initialSpawnPoint != null)
        {
            playerBounds.SetSpawnPoint(initialSpawnPoint);
        }
    }

    private void MoveRigTo(Transform target)
    {
        if (cameraRig == null || target == null)
        {
            return;
        }

        Transform rigTransform = cameraRig.transform;
        Vector3 targetForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (targetForward.sqrMagnitude < 0.001f)
        {
            targetForward = Vector3.forward;
        }

        rigTransform.SetPositionAndRotation(
            target.position,
            Quaternion.LookRotation(targetForward, Vector3.up));
    }
}
