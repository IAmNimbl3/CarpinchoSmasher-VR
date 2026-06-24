using UnityEngine;

public class TeleportFloorVisualProjector : MonoBehaviour
{
    [SerializeField] private Transform teleportTarget;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private LayerMask floorMask = ~0;
    [SerializeField, Min(0.05f)] private float raycastUpOffset = 5f;
    [SerializeField, Min(0.05f)] private float raycastDownDistance = 20f;
    [SerializeField, Min(0f)] private float floorOffset = 0.01f;
    [SerializeField] private bool alignVisualToFloorNormal;
    [SerializeField] private bool projectOnEnable = true;
    [SerializeField] private bool projectEveryFrame;

    private void OnEnable()
    {
        if (projectOnEnable)
        {
            ProjectVisualToFloor();
        }
    }

    private void LateUpdate()
    {
        if (projectEveryFrame)
        {
            ProjectVisualToFloor();
        }
    }

    public void Configure(Transform target, Transform visual, float offset)
    {
        teleportTarget = target;
        visualRoot = visual;
        floorOffset = offset;
    }

    [ContextMenu("Project Visual To Floor")]
    public void ProjectVisualToFloor()
    {
        Transform target = teleportTarget != null ? teleportTarget : transform;

        if (visualRoot == null)
        {
            visualRoot = transform.Find("FloorVisual");
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        Vector3 origin = target.position + Vector3.up * raycastUpOffset;
        float maxDistance = raycastUpOffset + raycastDownDistance;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, floorMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        visualRoot.position = hit.point + hit.normal * floorOffset;

        if (alignVisualToFloorNormal)
        {
            Vector3 projectedForward = Vector3.ProjectOnPlane(target.forward, hit.normal);
            if (projectedForward.sqrMagnitude < 0.0001f)
            {
                projectedForward = Vector3.ProjectOnPlane(Vector3.forward, hit.normal);
            }

            visualRoot.rotation = Quaternion.LookRotation(projectedForward.normalized, hit.normal);
        }
        else
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.0001f)
            {
                flatForward = Vector3.forward;
            }

            visualRoot.rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        }
    }
}
