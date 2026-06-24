using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameRoundManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CarpinchoSpawner spawner;
    [SerializeField] private OVRCameraRig cameraRig;

    [Header("Round UI")]
    [SerializeField] private bool createRoundUiOnAwake = true;
    [SerializeField, Min(0.25f)] private float uiDistanceFromCamera = 1.4f;
    [SerializeField] private Vector3 uiOffset = new Vector3(0f, -0.04f, 0f);
    [SerializeField] private Vector2 uiSize = new Vector2(700f, 340f);
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.58f);
    [SerializeField] private Color accentColor = new Color(1f, 0.82f, 0.15f, 0.95f);

    [Header("Scoring")]
    [SerializeField, Min(0)] private int defaultScorePerKill = 10;

    public int CurrentRoundIndex { get; private set; }
    public int Score { get; private set; }
    public bool WaitingForNextRound { get; private set; }

    private readonly Dictionary<CarpinchoType, int> _killsByType = new Dictionary<CarpinchoType, int>();
    private readonly HashSet<Enemy> _subscribedEnemies = new HashSet<Enemy>();

    private Canvas _roundCanvas;
    private RectTransform _roundCanvasRect;
    private Text _titleText;
    private Text _bodyText;
    private Button _continueButton;
    private Transform _cameraAnchor;

    public IReadOnlyDictionary<CarpinchoType, int> KillsByType => _killsByType;

    private void Awake()
    {
        ResolveReferences();
        InitializeKillCounters();

        if (createRoundUiOnAwake)
        {
            CreateRoundUi();
        }

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
    }

    private void OnDisable()
    {
        if (spawner != null)
        {
            spawner.PhaseChanged -= HandlePhaseChanged;
            spawner.EnemySpawned -= HandleEnemySpawned;
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
        if (!WaitingForNextRound)
        {
            return;
        }

        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger, OVRInput.Controller.RTouch)
            || OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
        {
            ContinueToNextRound();
        }
    }

    private void LateUpdate()
    {
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
        HideRoundUi();

        if (spawner != null)
        {
            spawner.ResumeSpawning();
        }
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
        WaitingForNextRound = true;

        if (spawner != null)
        {
            spawner.PauseSpawning();
        }

        ShowRoundCompleteUi(phaseIndex);
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

        _roundCanvas.gameObject.SetActive(true);
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
        RectTransform buttonRect = _continueButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.25f, 0.09f);
        buttonRect.anchorMax = new Vector2(0.75f, 0.27f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        _continueButton.onClick.AddListener(ContinueToNextRound);
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
