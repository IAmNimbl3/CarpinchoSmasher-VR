using UnityEngine;

public class PlayerBounds : MonoBehaviour
{
    [SerializeField] private float minY = -2f;
    [SerializeField] private float maxHorizontalDistance = 9.5f;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 0f, -0.411f);
    [SerializeField] private float checkInterval = 0.25f;

    private float _nextCheck;

    public void SetSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;

        if (spawnPoint != null)
        {
            spawnPosition = spawnPoint.position;
        }
    }

    private void Update()
    {
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + checkInterval;

        if (spawnPoint != null)
        {
            spawnPosition = spawnPoint.position;
        }

        Vector3 p = transform.position;
        bool fell = p.y < minY;
        Vector2 horizontalOffset = new Vector2(p.x - spawnPosition.x, p.z - spawnPosition.z);
        bool outOfBounds = horizontalOffset.magnitude > maxHorizontalDistance;
        if (fell || outOfBounds)
        {
            transform.SetPositionAndRotation(
                spawnPosition,
                spawnPoint != null ? spawnPoint.rotation : transform.rotation);
        }
    }
}
