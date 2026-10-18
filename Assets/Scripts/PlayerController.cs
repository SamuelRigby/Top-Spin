using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CustomGravity))]
[RequireComponent(typeof(GroundCheck))]
public class PlayerController : MonoBehaviour
{
    public InputActionAsset inputActions;

    [SerializeField] private float playerWalkSpeed = 5f;
    [SerializeField] private float rotationSharpness = 12f;
    [SerializeField] private float jumpSpeed = 5f;

    [Header("Moving Ground")]
    [SerializeField] private CylinderRotation cylinder;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform cameraUp;

    private InputActionMap playerActionMap;
    private InputAction playerMoveAction;
    private InputAction playerJumpAction;

    private Rigidbody playerRigidbody;
    private CustomGravity customGravity;
    private GroundCheck groundCheck;
    private bool jumpRequested;
    private bool hasJumped;
    private Vector2 playerMoveAmount;
    private readonly Dictionary<Collider, ContactPoint[]> movementContacts =
        new Dictionary<Collider, ContactPoint[]>();

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
        playerJumpAction = playerActionMap.FindAction("Jump", true);

        playerRigidbody = GetComponent<Rigidbody>();
        customGravity = GetComponent<CustomGravity>();
        groundCheck = GetComponent<GroundCheck>();

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
        jumpRequested = false;
        movementContacts.Clear();
    }

    private void Update()
    {
        playerMoveAmount = playerMoveAction.ReadValue<Vector2>();
        jumpRequested |= playerJumpAction.WasPressedThisFrame();
    }

    private void FixedUpdate()
    {
        PlayerMoveAndRotate();
        PlayerJump();
    }

    private void PlayerMoveAndRotate()
    {
        Vector3 localUp = LocalUp;
        Vector3 moveDirection = GetMoveDirection(localUp);

        ApplyMovement(moveDirection, localUp);
        RotatePlayer(moveDirection, localUp);
    }

    private void PlayerJump()
    {
        bool isGrounded = groundCheck.IsGrounded;

        if (!isGrounded)
        {
            hasJumped = false;
        }

        if (jumpRequested && isGrounded && !hasJumped)
        {
            Vector3 localUp = LocalUp;
            Vector3 surfaceVelocity =
                Vector3.ProjectOnPlane(playerRigidbody.linearVelocity, localUp);

            playerRigidbody.linearVelocity = surfaceVelocity + localUp * jumpSpeed;
            hasJumped = true;
        }

        jumpRequested = false;
    }

    private void SetupRigidbody()
    {
        playerRigidbody.constraints |= RigidbodyConstraints.FreezeRotation;
        playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        playerRigidbody.useGravity = false;
        playerRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
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
        Vector3 groundVelocity = Vector3.zero;

        if (cylinder != null && groundCheck.IsGrounded && !hasJumped)
        {
            groundVelocity = cylinder.GetSurfaceVelocity(playerRigidbody.position);
        }

        // Preserve motion into/away from the ground relative to its movement.
        Vector3 relativeVelocity = playerRigidbody.linearVelocity - groundVelocity;
        Vector3 radialVelocity =
            Vector3.Project(relativeVelocity, localUp);

        Vector3 velocity = groundVelocity + radialVelocity + moveDirection * playerWalkSpeed;

        // Keep the wall's separating velocity: replacing it with input every
        // step makes the controller fight the physics solver as walls rotate in.
        for (int pass = 0; pass < 3; pass++)
        {
            foreach (var entry in movementContacts)
            {
                if (entry.Key == null || !entry.Key.enabled)
                {
                    continue;
                }

                Rigidbody body = entry.Key.attachedRigidbody;
                CylinderRotation rotatingCylinder = body != null
                    ? body.GetComponent<CylinderRotation>()
                    : null;

                foreach (ContactPoint contact in entry.Value)
                {
                    Vector3 wallVelocity = rotatingCylinder != null
                        ? rotatingCylinder.GetSurfaceVelocity(contact.point)
                        : body != null ? body.GetPointVelocity(contact.point) : Vector3.zero;
                    float inwardSpeed = Vector3.Dot(velocity - wallVelocity, contact.normal);
                    if (inwardSpeed < 0f)
                    {
                        velocity -= contact.normal * inwardSpeed;
                    }
                }
            }
        }

        playerRigidbody.linearVelocity = velocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        movementContacts[collision.collider] = collision.contacts;
    }

    private void OnCollisionStay(Collision collision)
    {
        movementContacts[collision.collider] = collision.contacts;
    }

    private void OnCollisionExit(Collision collision)
    {
        movementContacts.Remove(collision.collider);
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
