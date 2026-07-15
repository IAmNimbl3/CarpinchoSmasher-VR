using System;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

public class TrophyShelfDisplay : MonoBehaviour
{
    [Serializable]
    private class TrophySlot
    {
        public TrophyId trophy;
        public GameObject model;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        [Min(0.01f)] public float targetHeight = 0.28f;
    }

    [SerializeField] private TrophySlot[] trophySlots = Array.Empty<TrophySlot>();

    [Header("Trophy interaction")]
    [SerializeField] private bool makeTrophiesGrabbable = true;
    [SerializeField, Min(0.01f)] private float trophyMass = 0.25f;
    [SerializeField, Min(0f)] private float colliderPadding = 0.025f;
    [SerializeField, Min(0f)] private float snapRearmDelay = 0.35f;
    [SerializeField] private Color outlineColor = new Color(1f, 0.82f, 0.15f, 0.65f);
    [SerializeField, Min(0f)] private float outlineWidth = 0.02f;

    private readonly Dictionary<TrophyId, GameObject> _spawnedTrophies = new Dictionary<TrophyId, GameObject>();

    private void OnEnable()
    {
        ScoreManager.Instance.ProgressChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (ScoreManager.HasInstance)
        {
            ScoreManager.Instance.ProgressChanged -= Refresh;
        }
    }

    public void Refresh()
    {
        for (int i = 0; i < trophySlots.Length; i++)
        {
            TrophySlot slot = trophySlots[i];
            if (slot == null)
            {
                continue;
            }

            bool unlocked = ScoreManager.Instance.IsTrophyUnlocked(slot.trophy);
            bool isSpawned = _spawnedTrophies.TryGetValue(slot.trophy, out GameObject spawned) && spawned != null;

            if (unlocked && !isSpawned)
            {
                SpawnTrophy(slot);
            }
            else if (!unlocked && isSpawned)
            {
                Destroy(spawned);
                _spawnedTrophies.Remove(slot.trophy);
            }
        }
    }

    private void SpawnTrophy(TrophySlot slot)
    {
        if (slot.model == null)
        {
            Debug.LogWarning($"[TrophyShelfDisplay] No model assigned for {slot.trophy}.", this);
            return;
        }

        GameObject instance = Instantiate(slot.model, transform);
        instance.name = $"Trophy_{slot.trophy}";
        Transform instanceTransform = instance.transform;
        instanceTransform.localPosition = slot.localPosition;
        instanceTransform.localRotation = Quaternion.Euler(slot.localEulerAngles);

        NormalizeHeightAndPlaceOnShelf(instance, slot.localPosition, slot.targetHeight);
        if (makeTrophiesGrabbable)
        {
            ConfigureInteraction(instance);
        }
        else
        {
            DisablePhysics(instance);
        }

        _spawnedTrophies[slot.trophy] = instance;
    }

    private void ConfigureInteraction(GameObject trophy)
    {
        DisableChildPhysics(trophy);

        BoxCollider grabCollider = trophy.GetComponent<BoxCollider>();
        if (grabCollider == null)
        {
            grabCollider = trophy.AddComponent<BoxCollider>();
        }

        CalculateLocalBounds(trophy, out Bounds localBounds);
        grabCollider.center = localBounds.center;
        grabCollider.size = localBounds.size + Vector3.one * (colliderPadding * 2f);
        grabCollider.isTrigger = false;

        Rigidbody rigidbody = trophy.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = trophy.AddComponent<Rigidbody>();
        }

        rigidbody.mass = trophyMass;
        rigidbody.useGravity = false;
        rigidbody.isKinematic = false;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.constraints = RigidbodyConstraints.FreezeAll;

        Transform grabAnchor = new GameObject("TrophyGrabAnchor").transform;
        grabAnchor.SetParent(trophy.transform, false);
        grabAnchor.localPosition = localBounds.center;

        GrabFreeTransformer transformer = trophy.AddComponent<GrabFreeTransformer>();
        Grabbable grabbable = trophy.AddComponent<Grabbable>();
        grabbable.InjectOptionalTargetTransform(trophy.transform);
        grabbable.InjectOptionalRigidbody(rigidbody);
        grabbable.InjectOptionalOneGrabTransformer(transformer);
        grabbable.InjectOptionalTwoGrabTransformer(transformer);
        grabbable.InjectOptionalThrowWhenUnselected(true);
        grabbable.InjectOptionalKinematicWhileSelected(true);
        grabbable.MaxGrabPoints = 1;
        grabbable.TransferOnSecondSelection = true;

        GrabInteractable grabInteractable = trophy.AddComponent<GrabInteractable>();
        grabInteractable.InjectAllGrabInteractable(rigidbody);
        grabInteractable.InjectOptionalPointableElement(grabbable);
        grabInteractable.InjectOptionalGrabSource(grabAnchor);
        grabInteractable.UseClosestPointAsGrabSource = false;
        grabInteractable.ResetGrabOnGrabsUpdated = true;

        MaterialOutlineHighlighter highlighter = trophy.AddComponent<MaterialOutlineHighlighter>();
        highlighter.Configure(outlineColor, outlineWidth);

        TrophyGrabController controller = trophy.AddComponent<TrophyGrabController>();
        controller.Initialize(grabInteractable, grabbable, rigidbody, highlighter, snapRearmDelay);
    }

    private void NormalizeHeightAndPlaceOnShelf(GameObject instance, Vector3 shelfPosition, float targetHeight)
    {
        if (!TryGetRendererBounds(instance, out Bounds bounds) || bounds.size.y <= 0.0001f)
        {
            return;
        }

        float scaleFactor = targetHeight / bounds.size.y;
        instance.transform.localScale *= scaleFactor;

        if (!TryGetRendererBounds(instance, out bounds))
        {
            return;
        }

        float desiredBottom = transform.TransformPoint(shelfPosition).y;
        instance.transform.position += Vector3.up * (desiredBottom - bounds.min.y);
    }

    private static bool TryGetRendererBounds(GameObject target, out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

    private static void CalculateLocalBounds(GameObject target, out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
            return;
        }

        Transform root = target.transform;
        bool initialized = false;
        bounds = default;
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Bounds worldBounds = renderers[rendererIndex].bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldCorner = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 localCorner = root.InverseTransformPoint(worldCorner);
                if (!initialized)
                {
                    bounds = new Bounds(localCorner, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(localCorner);
                }
            }
        }
    }

    private static void DisableChildPhysics(GameObject trophy)
    {
        Rigidbody[] rigidbodies = trophy.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            if (rigidbodies[i].gameObject != trophy)
            {
                rigidbodies[i].isKinematic = true;
                rigidbodies[i].detectCollisions = false;
            }
        }

        Collider[] colliders = trophy.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].gameObject != trophy)
            {
                colliders[i].enabled = false;
            }
        }
    }

    private static void DisablePhysics(GameObject trophy)
    {
        Rigidbody[] rigidbodies = trophy.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
        }

        Collider[] colliders = trophy.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }
}
