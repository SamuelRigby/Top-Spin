using UnityEngine;
using UnityEngine.InputSystem;
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public InputActionAsset inputActions;
    public InputActionMap playerActionMap;

    private InputAction playerMoveAction;
    private InputAction playerJumpAction;

    private float playerWalkSpeed = 5f;
    private float playerRotateDampening = 0.1f;
    private float turnSmoothingVelocity;

    private float verticalVelocity = 0f;
    private float grav = -10f;
    private float jumpHeight = 2f;

    private Vector2 playerMoveAmount;

    private Rigidbody playerRigidbody;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    private void OnEnable()
    {
        playerActionMap = inputActions.FindActionMap("Player");
        playerActionMap.Enable();
    }
    private void OnDisable()
    {
        playerActionMap.Disable();
    }

    private void Awake()
    {
        playerActionMap = inputActions.FindActionMap("Player");
        playerMoveAction = playerActionMap.FindAction("Move");
        playerJumpAction = playerActionMap.FindAction("Jump");
        playerRigidbody = GetComponent<Rigidbody>();
        playerRigidbody.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; //freezes rotations
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    void Start()
    {
        
    }

    void Update()
    {
        playerMoveAmount = playerMoveAction.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        PlayerMoveAndRotate();
    }

    private void PlayerMoveAndRotate()
    {
        Vector3 playerDirection = new Vector3(playerMoveAmount.x, 0f, playerMoveAmount.y).normalized;
        Vector3 verticalMove = new Vector3(0f, verticalVelocity, 0f);

        if (playerDirection.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(playerDirection.x, playerDirection.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothingVelocity, playerRotateDampening);
            playerRigidbody.MoveRotation(Quaternion.Euler(0f, angle, 0f));
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            playerRigidbody.MovePosition(playerRigidbody.position + moveDir.normalized * playerWalkSpeed * Time.fixedDeltaTime);
        }
    }
}
