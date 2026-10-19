using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerPause : MonoBehaviour
{
    public static PlayerPause Instance;
    private InputSystem_Actions _playerInput;
    private InputSystem_Actions.PlayerActions _playerActions;
    private InputAction _playerPauseAction;
    [SerializeField] private GameObject _pausePanel;
    public bool IsPaused { get; set; }

    private void Awake()
    {
        Instance = this;
        _playerInput = new InputSystem_Actions();
        _playerActions = _playerInput.Player;
        _pausePanel.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        _playerInput.Enable();
        _playerPauseAction = _playerActions.Pause;
        _playerPauseAction.performed += Pause;
        _playerPauseAction.Enable();
    }

    private void OnDisable()
    {
        _playerInput.Disable();
        _playerPauseAction.performed -= Pause;
        _playerPauseAction.Disable();
    }

    private void Pause(InputAction.CallbackContext context)
    {
        if (IsPaused)
        {
            UIManager.Instance.Resume();
        }
        else
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
            IsPaused = true;
            _pausePanel.gameObject.SetActive(true);
        }
    }
}
