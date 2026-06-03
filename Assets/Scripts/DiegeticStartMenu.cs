using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DiegeticStartMenu : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private string gameplaySceneName = "SceneMapaVR";

    public bool HasStarted { get; private set; }

    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            ClickSelectedOrStart();
        }
    }

    private void Awake()
    {
        menuRoot ??= gameObject;

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
        Debug.Log("[DiegeticStartMenu] Options button pressed. Placeholder only.");
    }

    public void ExitPlaceholder()
    {
        Debug.Log("[DiegeticStartMenu] Exit button pressed. Placeholder only.");
    }
}
