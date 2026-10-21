using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private GameObject _controlsPanel;
    [SerializeField] private GameObject _pausePanel;

    private void Awake()
    {
        Instance = this;
        if (_controlsPanel)
        {
            _controlsPanel.SetActive(false);
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene("Level");
    }

    public void MainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
        SceneManager.LoadScene("MainMenu");
    }

    public void Controls()
    {
        _menuPanel.SetActive(false);
        _controlsPanel.SetActive(true);
    }

    public void Back()
    {
        _menuPanel.SetActive(true);
        _controlsPanel.SetActive(false);
    }

    public void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void Resume()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        PlayerPause.Instance.IsPaused = false;
        _pausePanel.gameObject.SetActive(false);
    }
}
