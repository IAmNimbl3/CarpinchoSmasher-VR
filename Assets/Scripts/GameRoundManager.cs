using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameRoundManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CarpinchoSpawner spawner;
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Round UI")]
    [SerializeField] private bool createRoundUiOnAwake = true;
    [SerializeField, Min(0.25f)] private float uiDistanceFromCamera = 1.4f;
    [SerializeField] private Vector3 uiOffset = new Vector3(0f, -0.04f, 0f);
    [SerializeField] private Vector2 uiSize = new Vector2(700f, 340f);
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.58f);
    [SerializeField] private Color accentColor = new Color(1f, 0.82f, 0.15f, 0.95f);
    [SerializeField] private bool useControllerRayForRoundUi = true;
    [SerializeField, Min(0.1f)] private float roundUiRayLength = 8f;

    [Header("Scoring")]
    [SerializeField, Min(0)] private int defaultScorePerKill = 10;

    [Header("Defeat")]
    [SerializeField] private string menuSceneName = "Sample Optimizada";

    [Header("Health HUD")]
    [SerializeField] private bool createHealthHudOnAwake = true;
    [SerializeField] private Vector2 healthHudSize = new Vector2(320f, 72f);
    [SerializeField, Min(0f)] private float healthHudBottomOffset = 140f;
    [SerializeField] private Color healthHudColor = new Color(0f, 0f, 0f, 0.55f);

    public int CurrentRoundIndex { get; private set; }
    public int Score { get; private set; }
    public bool WaitingForNextRound { get; private set; }

    private readonly Dictionary<CarpinchoType, int> _killsByType = new Dictionary<CarpinchoType, int>();
    private readonly HashSet<Enemy> _subscribedEnemies = new HashSet<Enemy>();

    private bool _roundEndPending;
    private int _pendingNextPhaseIndex = -1;
    private bool _defeatActive;
    private bool _victoryActive;

    private Canvas _roundCanvas;
    private RectTransform _roundCanvasRect;
    private Text _titleText;
    private Text _bodyText;
    private Button _continueButton;
    private Button _secondaryButton;
    private Transform _cameraAnchor;

    private Canvas _healthCanvas;
    private Text _healthText;

    public IReadOnlyDictionary<CarpinchoType, int> KillsByType => _killsByType;

    private void Awake()
    {
        ResolveReferences();
        InitializeKillCounters();

        if (createRoundUiOnAwake)
        {
            CreateRoundUi();
        }

        if (createHealthHudOnAwake)
        {
            CreateHealthHud();
        }

        UpdateHealthHud();
        HideRoundUi();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (spawner != null)
        {
            spawner.PhaseChanged += HandlePhaseChanged;
            spawner.EnemySpawned += HandleEnemySpawned;
        }

        if (playerHealth != null)
        {
            playerHealth.HealthChanged += HandleHealthChanged;
            playerHealth.Died += HandlePlayerDied;
            UpdateHealthHud();
        }
    }

    private void OnDisable()
    {
        if (spawner != null)
        {
            spawner.PhaseChanged -= HandlePhaseChanged;
            spawner.EnemySpawned -= HandleEnemySpawned;
        }

        if (playerHealth != null)
        {
            playerHealth.HealthChanged -= HandleHealthChanged;
            playerHealth.Died -= HandlePlayerDied;
        }

        foreach (Enemy enemy in _subscribedEnemies)
        {
            if (enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
            }
        }

        _subscribedEnemies.Clear();
    }

    private void Update()
    {
        if (_defeatActive)
        {
            TryClickRoundUiWithControllerRay();
            return;
        }

        if (_victoryActive)
        {
            TryClickRoundUiWithControllerRay();
            return;
        }

        if (!WaitingForNextRound)
        {
            return;
        }

        TryClickRoundUiWithControllerRay();
    }

    private void LateUpdate()
    {
        if (_healthCanvas != null
            && _healthCanvas.renderMode == RenderMode.ScreenSpaceCamera
            && _healthCanvas.worldCamera == null)
        {
            _healthCanvas.worldCamera = ResolveHudCamera();
        }

        if (_roundCanvas == null || !_roundCanvas.gameObject.activeSelf)
        {
            return;
        }

        ResolveCameraAnchor();
        if (_cameraAnchor == null)
        {
            return;
        }

        Transform canvasTransform = _roundCanvas.transform;
        canvasTransform.position = _cameraAnchor.position
            + _cameraAnchor.forward * uiDistanceFromCamera
            + _cameraAnchor.TransformVector(uiOffset);
        canvasTransform.rotation = Quaternion.LookRotation(canvasTransform.position - _cameraAnchor.position, Vector3.up);
    }

    public int GetKills(CarpinchoType type)
    {
        return _killsByType.TryGetValue(type, out int value) ? value : 0;
    }

    public void ContinueToNextRound()
    {
        if (!WaitingForNextRound)
        {
            return;
        }

        WaitingForNextRound = false;
        _roundEndPending = false;
        _pendingNextPhaseIndex = -1;
        _victoryActive = false;
        HideRoundUi();

        if (spawner != null)
        {
            spawner.ResumeSpawning();
        }
    }

    public void RetryCurrentPhase()
    {
        _defeatActive = false;
        _victoryActive = false;
        WaitingForNextRound = false;
        _roundEndPending = false;
        _pendingNextPhaseIndex = -1;
        HideRoundUi();
        ClearTrackedEnemies();

        if (playerHealth != null)
        {
            playerHealth.ResetHealth();
        }

        if (spawner != null)
        {
            spawner.RestartCurrentPhase();
        }
    }

    public void ReturnToMenu()
    {
        if (string.IsNullOrEmpty(menuSceneName))
        {
            Debug.LogWarning("[GameRoundManager] Menu scene name is empty.", this);
            return;
        }

        SceneManager.LoadScene(menuSceneName);
    }

    private void ResolveReferences()
    {
        if (spawner == null)
        {
            spawner = CarpinchoSpawner.Instance != null
                ? CarpinchoSpawner.Instance
                : FindAnyObjectByType<CarpinchoSpawner>();
        }

        if (cameraRig == null)
        {
            cameraRig = FindAnyObjectByType<OVRCameraRig>();
        }

        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            playerHealth = gameObject.AddComponent<PlayerHealth>();
        }

        ResolveCameraAnchor();
    }

    private void ResolveCameraAnchor()
    {
        if (cameraRig == null)
        {
            cameraRig = FindAnyObjectByType<OVRCameraRig>();
        }

        _cameraAnchor = cameraRig != null && cameraRig.centerEyeAnchor != null
            ? cameraRig.centerEyeAnchor
            : Camera.main != null ? Camera.main.transform : null;
    }

    private void InitializeKillCounters()
    {
        foreach (CarpinchoType type in Enum.GetValues(typeof(CarpinchoType)))
        {
            _killsByType[type] = 0;
        }
    }

    private void HandlePhaseChanged(int phaseIndex)
    {
        if (phaseIndex <= 0)
        {
            CurrentRoundIndex = Mathf.Max(0, phaseIndex);
            return;
        }

        CurrentRoundIndex = phaseIndex;
        _pendingNextPhaseIndex = phaseIndex;
        _roundEndPending = true;

        if (spawner != null)
        {
            spawner.PauseSpawning();
        }

        TryShowPendingRoundComplete();
    }

    private void HandleEnemySpawned(Enemy enemy)
    {
        if (enemy == null || _subscribedEnemies.Contains(enemy))
        {
            return;
        }

        enemy.Died += HandleEnemyDied;
        _subscribedEnemies.Add(enemy);
    }

    private void HandleEnemyDied(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        enemy.Died -= HandleEnemyDied;
        _subscribedEnemies.Remove(enemy);

        Score += enemy.ScoreValue > 0 ? enemy.ScoreValue : defaultScorePerKill;
        _killsByType[enemy.Type] = GetKills(enemy.Type) + 1;

        TryShowPendingRoundComplete();
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        UpdateHealthHud(currentHealth, maxHealth);
    }

    private void HandlePlayerDied()
    {
        if (_defeatActive)
        {
            return;
        }

        _defeatActive = true;
        _victoryActive = false;
        WaitingForNextRound = false;
        _roundEndPending = false;

        if (spawner != null)
        {
            spawner.PauseSpawning();
        }

        ShowDefeatUi();
    }

    private void ClearTrackedEnemies()
    {
        foreach (Enemy enemy in _subscribedEnemies)
        {
            if (enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
            }
        }

        _subscribedEnemies.Clear();
    }

    private void TryShowPendingRoundComplete()
    {
        if (!_roundEndPending || WaitingForNextRound)
        {
            return;
        }

        if (_subscribedEnemies.Count > 0)
        {
            return;
        }

        _roundEndPending = false;

        if (IsVictoryPhaseIndex(_pendingNextPhaseIndex))
        {
            _victoryActive = true;
            WaitingForNextRound = false;
            ShowVictoryUi();
            return;
        }

        WaitingForNextRound = true;
        ShowRoundCompleteUi(_pendingNextPhaseIndex);
    }

    private void ShowRoundCompleteUi(int nextPhaseIndex)
    {
        if (_roundCanvas == null)
        {
            CreateRoundUi();
        }

        if (_roundCanvas == null)
        {
            return;
        }

        int completedRound = Mathf.Max(1, nextPhaseIndex);
        int nextRound = nextPhaseIndex + 1;
        string nextLabel = spawner != null ? spawner.GetPhaseLabel(nextPhaseIndex) : string.Empty;

        _titleText.text = $"Ronda {completedRound} terminada";
        _bodyText.text = string.IsNullOrEmpty(nextLabel)
            ? $"Puntaje: {Score}\nPreparado para la ronda {nextRound}"
            : $"Puntaje: {Score}\nSiguiente: {nextLabel}";

        ConfigureButton(_continueButton, "Iniciar siguiente ronda", ContinueToNextRound);
        SetButtonAnchors(_continueButton, new Vector2(0.25f, 0.09f), new Vector2(0.75f, 0.27f));
        if (_secondaryButton != null)
        {
            _secondaryButton.gameObject.SetActive(false);
        }

        _roundCanvas.gameObject.SetActive(true);
    }

    private void ShowDefeatUi()
    {
        if (_roundCanvas == null)
        {
            CreateRoundUi();
        }

        if (_roundCanvas == null)
        {
            return;
        }

        int health = playerHealth != null ? playerHealth.CurrentHealth : 0;
        _titleText.text = "Perdiste";
        _bodyText.text = $"Vida: {health}\nPuntaje: {Score}";

        ConfigureButton(_continueButton, "Reintentar fase", RetryCurrentPhase);
        SetButtonAnchors(_continueButton, new Vector2(0.08f, 0.09f), new Vector2(0.48f, 0.27f));

        if (_secondaryButton != null)
        {
            _secondaryButton.gameObject.SetActive(true);
            ConfigureButton(_secondaryButton, "Volver al menu", ReturnToMenu);
            SetButtonAnchors(_secondaryButton, new Vector2(0.52f, 0.09f), new Vector2(0.92f, 0.27f));
        }

        _roundCanvas.gameObject.SetActive(true);
    }

    private void ShowVictoryUi()
    {
        if (_roundCanvas == null)
        {
            CreateRoundUi();
        }

        if (_roundCanvas == null)
        {
            return;
        }

        _titleText.text = "GANASTE";
        _bodyText.text = $"Puntaje: {Score}\n{BuildKillsSummary()}";

        ConfigureButton(_continueButton, "Volver al menu", ReturnToMenu);
        SetButtonAnchors(_continueButton, new Vector2(0.25f, 0.09f), new Vector2(0.75f, 0.27f));

        if (_secondaryButton != null)
        {
            _secondaryButton.gameObject.SetActive(false);
        }

        _roundCanvas.gameObject.SetActive(true);
    }

    private bool IsVictoryPhaseIndex(int phaseIndex)
    {
        return spawner != null && spawner.PhaseCount > 0 && phaseIndex >= spawner.PhaseCount;
    }

    private string BuildKillsSummary()
    {
        return $"Velocistas: {GetKills(CarpinchoType.Velocista)} | Juggernauts: {GetKills(CarpinchoType.Juggernaut)}\n"
            + $"Paracaidistas: {GetKills(CarpinchoType.Paracaidista)} | Snipers: {GetKills(CarpinchoType.Sniper)}";
    }

    private void HideRoundUi()
    {
        if (_roundCanvas != null)
        {
            _roundCanvas.gameObject.SetActive(false);
        }
    }

    private void CreateRoundUi()
    {
        if (_roundCanvas != null)
        {
            return;
        }

        EnsureEventSystem();

        GameObject canvasObject = new GameObject("Round_Complete_DiegeticCanvas");
        canvasObject.transform.SetParent(transform, false);

        _roundCanvas = canvasObject.AddComponent<Canvas>();
        _roundCanvas.renderMode = RenderMode.WorldSpace;
        _roundCanvas.sortingOrder = 20;
        _roundCanvas.worldCamera = ResolveHudCamera();

        _roundCanvasRect = canvasObject.GetComponent<RectTransform>();
        _roundCanvasRect.sizeDelta = uiSize;
        _roundCanvasRect.localScale = Vector3.one * 0.0016f;

        canvasObject.AddComponent<GraphicRaycaster>();

        Image panel = CreateImage("Panel", _roundCanvasRect, panelColor);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        _titleText = CreateText("Title", _roundCanvasRect, "Ronda terminada", 42, TextAnchor.MiddleCenter);
        RectTransform titleRect = _titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.08f, 0.62f);
        titleRect.anchorMax = new Vector2(0.92f, 0.88f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        _bodyText = CreateText("Body", _roundCanvasRect, "Puntaje: 0", 24, TextAnchor.MiddleCenter);
        RectTransform bodyRect = _bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0.08f, 0.34f);
        bodyRect.anchorMax = new Vector2(0.92f, 0.62f);
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;

        _continueButton = CreateButton("Button_NextRound", _roundCanvasRect, "Iniciar siguiente ronda");
        SetButtonAnchors(_continueButton, new Vector2(0.25f, 0.09f), new Vector2(0.75f, 0.27f));
        ConfigureButton(_continueButton, "Iniciar siguiente ronda", ContinueToNextRound);

        _secondaryButton = CreateButton("Button_ReturnMenu", _roundCanvasRect, "Volver al menu");
        SetButtonAnchors(_secondaryButton, new Vector2(0.52f, 0.09f), new Vector2(0.92f, 0.27f));
        _secondaryButton.gameObject.SetActive(false);
    }

    private void CreateHealthHud()
    {
        if (_healthCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Gameplay_Health_HUD");
        canvasObject.transform.SetParent(transform, false);

        _healthCanvas = canvasObject.AddComponent<Canvas>();
        _healthCanvas.sortingOrder = 100;

        Camera hudCamera = ResolveHudCamera();
        if (hudCamera != null)
        {
            _healthCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            _healthCanvas.worldCamera = hudCamera;
            _healthCanvas.planeDistance = 0.5f;
        }
        else
        {
            _healthCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image panel = CreateImage("Panel", canvasObject.transform, healthHudColor);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.sizeDelta = healthHudSize;
        panelRect.anchoredPosition = new Vector2(0f, healthHudBottomOffset);

        _healthText = CreateText("HealthText", panelRect, "Vida: 100 / 100", 30, TextAnchor.MiddleCenter);
        RectTransform textRect = _healthText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void UpdateHealthHud()
    {
        if (playerHealth != null)
        {
            UpdateHealthHud(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
        else
        {
            UpdateHealthHud(100, 100);
        }
    }

    private void UpdateHealthHud(int currentHealth, int maxHealth)
    {
        if (_healthText == null)
        {
            return;
        }

        _healthText.text = $"Vida: {currentHealth} / {maxHealth}";
    }

    private Camera ResolveHudCamera()
    {
        ResolveCameraAnchor();

        if (_cameraAnchor != null && _cameraAnchor.TryGetComponent(out Camera anchorCamera))
        {
            return anchorCamera;
        }

        return Camera.main;
    }

    private bool TryClickRoundUiWithControllerRay()
    {
        if (!useControllerRayForRoundUi || _roundCanvas == null || !_roundCanvas.gameObject.activeSelf)
        {
            return false;
        }

        if (TryClickRoundUiWithController(OVRInput.Controller.RTouch, OVRInput.Button.SecondaryIndexTrigger, GetRightControllerRayTransform()))
        {
            return true;
        }

        return TryClickRoundUiWithController(OVRInput.Controller.LTouch, OVRInput.Button.PrimaryIndexTrigger, GetLeftControllerRayTransform());
    }

    private bool TryClickRoundUiWithController(OVRInput.Controller controller, OVRInput.Button button, Transform rayTransform)
    {
        if (rayTransform == null || !OVRInput.GetDown(button, controller))
        {
            return false;
        }

        Button targetButton = FindRoundButtonUnderRay(rayTransform.position, rayTransform.forward);
        if (targetButton == null || !targetButton.IsInteractable())
        {
            return false;
        }

        targetButton.onClick.Invoke();
        return true;
    }

    private Button FindRoundButtonUnderRay(Vector3 origin, Vector3 direction)
    {
        if (_roundCanvasRect == null || direction.sqrMagnitude <= 0.0001f)
        {
            return null;
        }

        Plane canvasPlane = new Plane(_roundCanvas.transform.forward, _roundCanvas.transform.position);
        Ray ray = new Ray(origin, direction.normalized);
        if (!canvasPlane.Raycast(ray, out float distance) || distance < 0f || distance > roundUiRayLength)
        {
            return null;
        }

        Vector3 hitPoint = ray.GetPoint(distance);
        Button firstButton = null;
        Button[] buttons = _roundCanvas.GetComponentsInChildren<Button>(false);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button candidate = buttons[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            RectTransform rectTransform = candidate.transform as RectTransform;
            if (rectTransform == null)
            {
                continue;
            }

            Vector3 localPoint = rectTransform.InverseTransformPoint(hitPoint);
            if (rectTransform.rect.Contains(new Vector2(localPoint.x, localPoint.y)))
            {
                firstButton = candidate;
                break;
            }
        }

        return firstButton;
    }

    private Transform GetRightControllerRayTransform()
    {
        ResolveReferences();
        return cameraRig != null && cameraRig.rightHandAnchor != null
            ? cameraRig.rightHandAnchor
            : _cameraAnchor;
    }

    private Transform GetLeftControllerRayTransform()
    {
        ResolveReferences();
        return cameraRig != null && cameraRig.leftHandAnchor != null
            ? cameraRig.leftHandAnchor
            : _cameraAnchor;
    }

    private void ConfigureButton(Button button, string label, UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        SetButtonLabel(button, label);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        Text labelText = button.GetComponentInChildren<Text>();
        if (labelText != null)
        {
            labelText.text = label;
        }
    }

    private void SetButtonAnchors(Button button, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (button == null)
        {
            return;
        }

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchorMin;
        buttonRect.anchorMax = anchorMax;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(string objectName, Transform parent, string text, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        Text textComponent = textObject.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = GetBuiltinFont();
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = Color.white;
        textComponent.raycastTarget = false;
        return textComponent;
    }

    private Button CreateButton(string objectName, Transform parent, string label)
    {
        Image buttonImage = CreateImage(objectName, parent, accentColor);
        Button button = buttonImage.gameObject.AddComponent<Button>();

        Text labelText = CreateText("Label", buttonImage.transform, label, 24, TextAnchor.MiddleCenter);
        labelText.color = Color.black;
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    private Font GetBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }
}
