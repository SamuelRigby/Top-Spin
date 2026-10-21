using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class PlayerRepair : MonoBehaviour
{
    private InputSystem_Actions _playerInput;
    private InputSystem_Actions.PlayerActions _playerActions;
    private InputAction _playerInteractAction;
    public static event Action InteractEvent = delegate { };

    private void Awake()
    {
        _playerInput = new InputSystem_Actions();
        _playerActions = _playerInput.Player;
    }

    private void OnEnable()
    {
        _playerInput.Enable();
        _playerInteractAction = _playerActions.Interact;
        _playerInteractAction.performed += Interact;
        _playerInteractAction.Enable();
    }

    private void OnDisable()
    {
        _playerInput.Disable();
        _playerInteractAction.performed -= Interact;
        _playerInteractAction.Disable();
    }

    private void Interact(InputAction.CallbackContext context)
    {
        InteractEvent.Invoke();
    }
    
   
}
