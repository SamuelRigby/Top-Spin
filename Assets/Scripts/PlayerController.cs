using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CustomGravity))]
public class PlayerController : MonoBehaviour
{
    public InputActionAsset inputActions;

    [SerializeField] private float playerWalkSpeed = 5f;
    [SerializeField] private float rotationSharpness = 12f;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform cameraUp;

    private InputActionMap playerActionMap;
    private InputAction playerMoveAction;

    private Rigidbody playerRigidbody;
    private CustomGravity customGravity;
    private Vector2 playerMoveAmount;

    private Vector3 LocalUp
    {
        get
        {
            Vector3 gravityDirection = customGravity.GetGravityDirection();

            return gravityDirection.sqrMagnitude > 0.0001f
                ? -gravityDirection.normalized
                : transform.up;
        }
    }

    private void Awake()
    {
        playerActionMap = inputActions.FindActionMap("Player", true);
        playerMoveAction = playerActionMap.FindAction("Move", true);

        playerRigidbody = GetComponent<Rigidbody>();
        customGravity = GetComponent<CustomGravity>();

        SetupRigidbody();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        playerActionMap.Enable();
    }

    private void OnDisable()
    {
        playerActionMap.Disable();
        playerMoveAmount = Vector2.zero;
    }

    private void Update()
    {
        playerMoveAmount = playerMoveAction.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        PlayerMoveAndRotate();
    }

    private void PlayerMoveAndRotate()
    {
        Vector3 localUp = LocalUp;
        Vector3 moveDirection = GetMoveDirection(localUp);

        ApplyMovement(moveDirection, localUp);
        RotatePlayer(moveDirection, localUp);
    }

    private void SetupRigidbody()
    {
        playerRigidbody.constraints |= RigidbodyConstraints.FreezeRotation;
        playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        playerRigidbody.useGravity = false;
    }

    private Vector3 GetCameraForward(Vector3 localUp)
    {
        Vector3 cameraForward =
            Vector3.ProjectOnPlane(cameraTransform.forward, localUp);

        if (cameraForward.sqrMagnitude < 0.0001f)
        {
            cameraForward =
                Vector3.ProjectOnPlane(cameraTransform.up, localUp);
        }

        return cameraForward.normalized;
    }

    private Vector3 GetMoveDirection(Vector3 localUp)
    {
        Vector3 cameraForward = GetCameraForward(localUp);
        Vector3 cameraRight =
            Vector3.Cross(localUp, cameraForward).normalized;
        Vector2 input = Vector2.ClampMagnitude(playerMoveAmount, 1f);

        return cameraForward * input.y + cameraRight * input.x;
    }

    private void ApplyMovement(Vector3 moveDirection, Vector3 localUp)
    {
        Vector3 radialVelocity =
            Vector3.Project(playerRigidbody.linearVelocity, localUp);

        playerRigidbody.linearVelocity =
            radialVelocity + moveDirection * playerWalkSpeed;
    }

    private void RotatePlayer(Vector3 moveDirection, Vector3 localUp)
    {
        Vector3 facingDirection = Vector3.ProjectOnPlane(
            playerRigidbody.rotation * Vector3.forward,
            localUp
        );

        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            facingDirection = moveDirection;
        }
        else if (facingDirection.sqrMagnitude < 0.0001f)
        {
            facingDirection = GetCameraForward(localUp);
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(facingDirection.normalized, localUp);

        float smoothing =
            1f - Mathf.Exp(-rotationSharpness * Time.fixedDeltaTime);

        playerRigidbody.MoveRotation(
            Quaternion.Slerp(
                playerRigidbody.rotation,
                targetRotation,
                smoothing
            )
        );
    }

    private void LateUpdate()
    {
        UpdateCameraUp();
    }

    private void UpdateCameraUp()
    {
        if (cameraUp == null)
        {
            return;
        }

        cameraUp.rotation =
            Quaternion.FromToRotation(cameraUp.up, LocalUp)
            * cameraUp.rotation;
    }
}