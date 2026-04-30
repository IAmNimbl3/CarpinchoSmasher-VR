using UnityEngine;

public class PlayerBounds : MonoBehaviour
{
    [SerializeField] private float minY = -2f;
    [SerializeField] private float maxHorizontalDistance = 9.5f;
    [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 0f, -0.411f);
    [SerializeField] private float checkInterval = 0.25f;

    private float _nextCheck;

    private void Update()
    {
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + checkInterval;

        Vector3 p = transform.position;
        bool fell = p.y < minY;
        bool outOfBounds = new Vector2(p.x, p.z).magnitude > maxHorizontalDistance;
        if (fell || outOfBounds)
        {
            transform.position = spawnPosition;
        }
    }
}
