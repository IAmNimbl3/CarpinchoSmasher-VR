using System.Collections;
using Oculus.Interaction;
using UnityEngine;

public class HammerRespawnLifecycle : MonoBehaviour
{
    [Header("Respawn")]
    [SerializeField] private GameObject hammerPrefab;
    [SerializeField] private float respawnDelay = 1.5f;

    [Header("Cleanup")]
    [SerializeField] private float despawnDelayAfterSettled = 5f;
    [SerializeField] private float settledLinearSpeed = 0.05f;
    [SerializeField] private float settledAngularSpeed = 0.2f;
    [SerializeField] private float settledDuration = 0.75f;

    private Rigidbody _rigidbody;
    private Grabbable _grabbable;
    private Collider[] _colliders;
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;
    private bool _wasGrabbed;
    private bool _wasReleased;
    private bool _hasTouchedSurface;
    private bool _collisionDisabled;
    private float _settledTimer;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _grabbable = GetComponent<Grabbable>();
        _colliders = GetComponentsInChildren<Collider>(true);
        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;
    }

    private void OnEnable()
    {
        if (_grabbable != null)
        {
            _grabbable.WhenPointerEventRaised += HandlePointerEventRaised;
        }
    }

    private void OnDisable()
    {
        if (_grabbable != null)
        {
            _grabbable.WhenPointerEventRaised -= HandlePointerEventRaised;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_wasReleased || collision.gameObject.CompareTag("Weapon"))
        {
            return;
        }

        _hasTouchedSurface = true;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!_wasReleased || collision.gameObject.CompareTag("Weapon"))
        {
            return;
        }

        _hasTouchedSurface = true;
    }

    private void Update()
    {
        if (!_wasReleased || _collisionDisabled || !_hasTouchedSurface || _rigidbody == null)
        {
            return;
        }

        bool isSettled = _rigidbody.IsSleeping()
            || (_rigidbody.linearVelocity.sqrMagnitude <= settledLinearSpeed * settledLinearSpeed
                && _rigidbody.angularVelocity.sqrMagnitude <= settledAngularSpeed * settledAngularSpeed);

        _settledTimer = isSettled ? _settledTimer + Time.deltaTime : 0f;

        if (_settledTimer >= settledDuration)
        {
            DisableCollisionAndDespawn();
        }
    }

    private void HandlePointerEventRaised(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
        {
            _wasGrabbed = true;
            return;
        }

        if (evt.Type == PointerEventType.Unselect && _wasGrabbed && !_wasReleased && _grabbable.SelectingPointsCount == 0)
        {
            _wasReleased = true;
            StartRespawnTimer();
        }
    }

    public void NotifyManualRelease()
    {
        if (_wasReleased)
        {
            return;
        }

        _wasGrabbed = true;
        _wasReleased = true;
        StartRespawnTimer();
    }

    private void StartRespawnTimer()
    {
        if (HammerRespawnSpawner.Instance != null)
        {
            HammerRespawnSpawner.Instance.RequestRespawn(_spawnPosition, _spawnRotation, respawnDelay);
            return;
        }

        GameObject prefab = hammerPrefab != null ? hammerPrefab : gameObject;
        RespawnRunner.Instance.StartCoroutine(SpawnReplacementAfterDelay(prefab, _spawnPosition, _spawnRotation, respawnDelay));
    }

    private static IEnumerator SpawnReplacementAfterDelay(GameObject prefab, Vector3 position, Quaternion rotation, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (prefab != null)
        {
            Instantiate(prefab, position, rotation);
        }
    }

    private void DisableCollisionAndDespawn()
    {
        _collisionDisabled = true;

        foreach (Collider hammerCollider in _colliders)
        {
            if (hammerCollider != null)
            {
                hammerCollider.enabled = false;
            }
        }

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
        }

        Destroy(gameObject, despawnDelayAfterSettled);
    }

    private class RespawnRunner : MonoBehaviour
    {
        private static RespawnRunner _instance;

        public static RespawnRunner Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                GameObject runnerObject = new GameObject("Hammer Respawn Runner");
                _instance = runnerObject.AddComponent<RespawnRunner>();
                return _instance;
            }
        }
    }
}
