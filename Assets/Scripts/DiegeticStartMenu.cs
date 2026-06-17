using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DiegeticStartMenu : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Text optionsButtonLabel;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private string gameplaySceneName = "SceneMapaVR";
    [SerializeField] private VRTrackingOriginApplier trackingOriginApplier;

    public bool HasStarted { get; private set; }

    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            ClickSelectedOrStart();
        }
    }

    private void Awake()
    {
        menuRoot ??= gameObject;
        trackingOriginApplier ??= FindAnyObjectByType<VRTrackingOriginApplier>();
        if (optionsButtonLabel == null && optionsButton != null)
        {
            optionsButtonLabel = optionsButton.GetComponentInChildren<Text>(true);
        }

        ApplySeatedMode();

        if (startButton != null)
        {
            startButton.onClick.AddListener(StartGame);
        }

        if (optionsButton != null)
        {
            optionsButton.onClick.AddListener(ShowOptionsPlaceholder);
        }

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(ExitPlaceholder);
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
        }

        if (optionsButton != null)
        {
            optionsButton.onClick.RemoveListener(ShowOptionsPlaceholder);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ExitPlaceholder);
        }
    }

    public void StartGame()
    {
        HasStarted = true;

        if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }

        SceneManager.LoadScene(gameplaySceneName);
    }

    private void ClickSelectedOrStart()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && selected.TryGetComponent(out Button selectedButton) && selectedButton.interactable)
        {
            selectedButton.onClick.Invoke();
            return;
        }

        if (startButton != null && startButton.interactable)
        {
            startButton.onClick.Invoke();
        }
    }

    public void ShowOptionsPlaceholder()
    {
        VRPlayModeSettings.ToggleSeatedMode();
        ApplySeatedMode();
        Debug.Log("[DiegeticStartMenu] Play mode: " + (VRPlayModeSettings.SeatedMode ? "Seated" : "Standing"));
    }

    public void ExitPlaceholder()
    {
        Debug.Log("[DiegeticStartMenu] Exit button pressed. Placeholder only.");
    }

    private void ApplySeatedMode()
    {
        if (trackingOriginApplier != null)
        {
            trackingOriginApplier.Apply();
        }

        if (optionsButtonLabel != null)
        {
            VRPlayModeConfig activeConfig = VRPlayModeSettings.ActiveConfig;
            string modeName = activeConfig != null
                ? activeConfig.DisplayName
                : (VRPlayModeSettings.SeatedMode ? "Sentado" : "Parado");
            optionsButtonLabel.text = "Modo: " + modeName;
        }
    }
}
