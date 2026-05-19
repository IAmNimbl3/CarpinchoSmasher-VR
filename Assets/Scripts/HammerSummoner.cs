using UnityEngine;

public class HammerSummoner : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Transform del controller anchor donde aparece el martillo.")]
    [SerializeField] private Transform handAnchor;
    [SerializeField] private GameObject hammerPrefab;
    [Tooltip("Controller que dispara el summon/throw.")]
    [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.RTouch;

    [Header("Hold offset")]
    [Tooltip("Offset local del martillo cuando aparece en la mano. El default empuja el grip a la palma y la cabeza al frente.")]
    [SerializeField] private Vector3 holdLocalPosition = new Vector3(0f, 0f, 0.24f);
    [Tooltip("Rotación local del martillo cuando aparece en la mano. (90,0,0) alinea el eje del martillo con el forward de la mano.")]
    [SerializeField] private Vector3 holdLocalEulerAngles = new Vector3(90f, 0f, 0f);

    [Header("Timing")]
    [Tooltip("Cooldown desde el momento del throw hasta que se puede summonear de nuevo.")]
    [SerializeField, Min(0f)] private float cooldown = 1.5f;

    [Header("Throw")]
    [Tooltip("Velocidad máxima a la que se puede lanzar el martillo.")]
    [SerializeField, Min(1f)] private float maxThrowSpeed = 15f;
    [Tooltip("Velocidad mínima para considerar que es un throw (debajo: el martillo solo se suelta sin fuerza).")]
    [SerializeField, Min(0f)] private float minThrowSpeed = 0.5f;

    private const int VelocityHistorySize = 6;
    private readonly Vector3[] _velocityHistory = new Vector3[VelocityHistorySize];
    private int _historyIndex;

    private GameObject _currentHammer;
    private ThrownHammer _currentHammerComponent;
    private float _cooldownRemaining;
    private Vector3 _lastHandPosition;
    private bool _hasLastPosition;

    private void OnEnable()
    {
        _hasLastPosition = false;
        for (int i = 0; i < VelocityHistorySize; i++)
        {
            _velocityHistory[i] = Vector3.zero;
        }
    }

    private void Update()
    {
        UpdateHandVelocity();

        if (_cooldownRemaining > 0f)
        {
            _cooldownRemaining -= Time.deltaTime;
        }

        if (handAnchor == null || hammerPrefab == null)
        {
            return;
        }

        bool triggerDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller);
        bool triggerUp = OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, controller);

        if (triggerDown && _currentHammer == null && _cooldownRemaining <= 0f)
        {
            SummonHammer();
        }
        else if (triggerUp && _currentHammer != null)
        {
            ThrowHammer();
        }
    }

    private void UpdateHandVelocity()
    {
        if (handAnchor == null)
        {
            return;
        }

        Vector3 current = handAnchor.position;
        if (_hasLastPosition && Time.deltaTime > 0f)
        {
            Vector3 instant = (current - _lastHandPosition) / Time.deltaTime;
            _velocityHistory[_historyIndex] = instant;
            _historyIndex = (_historyIndex + 1) % VelocityHistorySize;
        }
        _lastHandPosition = current;
        _hasLastPosition = true;
    }

    private Vector3 GetThrowVelocity()
    {
        Vector3 best = Vector3.zero;
        float bestSqr = 0f;
        for (int i = 0; i < VelocityHistorySize; i++)
        {
            float m = _velocityHistory[i].sqrMagnitude;
            if (m > bestSqr)
            {
                bestSqr = m;
                best = _velocityHistory[i];
            }
        }
        return Vector3.ClampMagnitude(best, maxThrowSpeed);
    }

    private void SummonHammer()
    {
        _currentHammer = Instantiate(hammerPrefab, handAnchor);
        _currentHammer.transform.localPosition = holdLocalPosition;
        _currentHammer.transform.localRotation = Quaternion.Euler(holdLocalEulerAngles);
        _currentHammerComponent = _currentHammer.GetComponent<ThrownHammer>();
        if (_currentHammerComponent != null)
        {
            _currentHammerComponent.BeginSummon();
        }
    }

    private void ThrowHammer()
    {
        if (_currentHammerComponent != null)
        {
            Vector3 v = GetThrowVelocity();
            if (v.magnitude < minThrowSpeed)
            {
                v = Vector3.zero;
            }
            _currentHammerComponent.Throw(v);
        }
        _currentHammer = null;
        _currentHammerComponent = null;
        _cooldownRemaining = cooldown;
    }
}
