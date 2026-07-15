using System;
using System.Collections.Generic;
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
        DisablePhysics(instance);
        _spawnedTrophies[slot.trophy] = instance;
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
