using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DiegeticStartMenu : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private string gameplaySceneName = "SampleScene";

    public bool HasStarted { get; private set; }

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

    public void ShowOptionsPlaceholder()
    {
        Debug.Log("[DiegeticStartMenu] Options button pressed. Placeholder only.");
    }

    public void ExitPlaceholder()
    {
        Debug.Log("[DiegeticStartMenu] Exit button pressed. Placeholder only.");
    }
}
