using System.Collections;
using UnityEngine;

public class HammerRespawnSpawner : MonoBehaviour
{
    [SerializeField] private GameObject hammerPrefab;

    public static HammerRespawnSpawner Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RequestRespawn(Vector3 position, Quaternion rotation, float delay)
    {
        StartCoroutine(SpawnAfterDelay(position, rotation, delay));
    }

    private IEnumerator SpawnAfterDelay(Vector3 position, Quaternion rotation, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (hammerPrefab != null)
        {
            Instantiate(hammerPrefab, position, rotation);
        }
    }
}
