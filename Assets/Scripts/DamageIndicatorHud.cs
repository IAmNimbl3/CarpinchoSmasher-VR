using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DamageIndicatorHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private CarpinchoSpawner spawner;

    [Header("Sprites")]
    [SerializeField] private Sprite leftIndicator;
    [SerializeField] private Sprite rightIndicator;
    [SerializeField] private Sprite parachuteIndicator;

    [Header("Canvas")]
    [SerializeField] private bool createCanvasOnAwake = true;
    [SerializeField, Min(0.1f)] private float distanceFromFace = 0.55f;
    [SerializeField] private Vector2 canvasSize = new Vector2(1000f, 700f);
    [SerializeField, Min(0.0001f)] private float canvasScale = 0.00115f;
    [SerializeField] private int sortingOrder = 120;

    [Header("Layout")]
    [SerializeField] private Vector2 leftAnchoredPosition = new Vector2(-315f, -30f);
    [SerializeField] private Vector2 rightAnchoredPosition = new Vector2(315f, -30f);
    [SerializeField] private Vector2 parachuteAnchoredPosition = new Vector2(0f, 185f);
    [SerializeField] private Vector2 sideIndicatorSize = new Vector2(135f, 210f);
    [SerializeField] private Vector2 parachuteIndicatorSize = new Vector2(245f, 115f);

    [Header("Visibility")]
    [Tooltip("Margen del viewport que cuenta como fuera de camara. Subilo si queres que el aviso aparezca antes de salir completamente.")]
    [SerializeField, Range(0f, 0.25f)] private float viewportPadding = 0.03f;
    [Tooltip("No muestra indicadores para enemigos mas lejos que esta distancia. En 0, no hay limite.")]
    [SerializeField, Min(0f)] private float maxEnemyDistance = 0f;
    [SerializeField, Min(0.01f)] private float refreshInterval = 0.05f;

    private readonly List<Enemy> _enemyBuffer = new List<Enemy>();

    private Canvas _canvas;
    private RectTransform _canvasRect;
    private Image _leftImage;
    private Image _rightImage;
    private Image _parachuteImage;
    private Transform _cameraAnchor;
    private float _nextRefreshTime;
    private bool _showLeft;
    private bool _showRight;
    private bool _showParachute;

    private void Awake()
    {
        ResolveReferences();

        if (createCanvasOnAwake)
        {
            CreateCanvas();
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshIndicators();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshTime)
        {
            return;
        }

        _nextRefreshTime = Time.unscaledTime + refreshInterval;
        RefreshIndicators();
    }

    private void LateUpdate()
    {
        UpdateCanvasTransform();
        ApplyIndicatorVisibility();
    }

    private void ResolveReferences()
    {
        if (cameraRig == null)
        {
            cameraRig = FindAnyObjectByType<OVRCameraRig>();
        }

        _cameraAnchor = cameraRig != null && cameraRig.centerEyeAnchor != null
            ? cameraRig.centerEyeAnchor
            : null;

        if (targetCamera == null)
        {
            if (_cameraAnchor != null)
            {
                targetCamera = _cameraAnchor.GetComponent<Camera>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        if (_cameraAnchor == null && targetCamera != null)
        {
            _cameraAnchor = targetCamera.transform;
        }

        if (spawner == null)
        {
            spawner = CarpinchoSpawner.Instance != null
                ? CarpinchoSpawner.Instance
                : FindAnyObjectByType<CarpinchoSpawner>();
        }
    }

    private void CreateCanvas()
    {
        if (_canvas != null)
        {
            return;
        }

        ResolveReferences();

        GameObject canvasObject = new GameObject("Damage_Indicator_HUD");
        canvasObject.transform.SetParent(_cameraAnchor != null ? _cameraAnchor : transform, false);

        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = sortingOrder;
        _canvas.worldCamera = targetCamera;

        _canvasRect = canvasObject.GetComponent<RectTransform>();
        _canvasRect.sizeDelta = canvasSize;
        _canvasRect.localScale = Vector3.one * canvasScale;

        _leftImage = CreateIndicatorImage("Indicator_Left", leftIndicator, leftAnchoredPosition, sideIndicatorSize);
        _rightImage = CreateIndicatorImage("Indicator_Right", rightIndicator, rightAnchoredPosition, sideIndicatorSize);
        _parachuteImage = CreateIndicatorImage("Indicator_Parachute", parachuteIndicator, parachuteAnchoredPosition, parachuteIndicatorSize);

        ApplyIndicatorVisibility();
        UpdateCanvasTransform();
    }

    private Image CreateIndicatorImage(string objectName, Sprite sprite, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(_canvasRect, false);

        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;

        RectTransform rectTransform = image.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        return image;
    }

    private void UpdateCanvasTransform()
    {
        if (_canvas == null)
        {
            return;
        }

        ResolveReferences();
        if (_cameraAnchor == null)
        {
            return;
        }

        Transform canvasTransform = _canvas.transform;
        if (canvasTransform.parent != _cameraAnchor)
        {
            canvasTransform.SetParent(_cameraAnchor, false);
        }

        canvasTransform.localPosition = Vector3.forward * distanceFromFace;
        canvasTransform.localRotation = Quaternion.identity;
        canvasTransform.localScale = Vector3.one * canvasScale;
    }

    private void RefreshIndicators()
    {
        ResolveReferences();

        _showLeft = false;
        _showRight = false;
        _showParachute = false;

        if (targetCamera == null)
        {
            return;
        }

        FillEnemyBuffer();

        for (int i = 0; i < _enemyBuffer.Count; i++)
        {
            Enemy enemy = _enemyBuffer[i];
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 enemyPosition = GetIndicatorTargetPosition(enemy);
            if (maxEnemyDistance > 0f
                && (enemyPosition - targetCamera.transform.position).sqrMagnitude > maxEnemyDistance * maxEnemyDistance)
            {
                continue;
            }

            if (IsInsideCamera(enemyPosition))
            {
                continue;
            }

            if (enemy.Type == CarpinchoType.Paracaidista)
            {
                _showParachute = true;
                continue;
            }

            Vector3 localToCamera = targetCamera.transform.InverseTransformPoint(enemyPosition);
            if (localToCamera.x < 0f)
            {
                _showLeft = true;
            }
            else
            {
                _showRight = true;
            }
        }
    }

    private void FillEnemyBuffer()
    {
        _enemyBuffer.Clear();

        if (spawner != null)
        {
            IReadOnlyList<Enemy> aliveEnemies = spawner.AliveEnemies;
            for (int i = 0; i < aliveEnemies.Count; i++)
            {
                _enemyBuffer.Add(aliveEnemies[i]);
            }

            return;
        }

        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            _enemyBuffer.Add(enemies[i]);
        }
    }

    private Vector3 GetIndicatorTargetPosition(Enemy enemy)
    {
        Renderer renderer = enemy.GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.bounds.center : enemy.transform.position;
    }

    private bool IsInsideCamera(Vector3 worldPosition)
    {
        Vector3 viewportPoint = targetCamera.WorldToViewportPoint(worldPosition);
        return viewportPoint.z > targetCamera.nearClipPlane
            && viewportPoint.x >= viewportPadding
            && viewportPoint.x <= 1f - viewportPadding
            && viewportPoint.y >= viewportPadding
            && viewportPoint.y <= 1f - viewportPadding;
    }

    private void ApplyIndicatorVisibility()
    {
        SetImageVisible(_leftImage, _showLeft);
        SetImageVisible(_rightImage, _showRight);
        SetImageVisible(_parachuteImage, _showParachute);
    }

    private void SetImageVisible(Image image, bool visible)
    {
        if (image != null && image.enabled != visible)
        {
            image.enabled = visible;
        }
    }
}
