using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;

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

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    private InputActionMap playerActionMap;
    private InputAction playerMoveAction;
    private InputAction playerJumpAction;

    private Rigidbody playerRigidbody;
    private CustomGravity customGravity;
    private GroundCheck groundCheck;
    private Animator animator;
    private Transform cameraFollowTarget;
    private bool ownsCameraUp;
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
        animator = GetComponentInChildren<Animator>();

        SetupRigidbody();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        playerActionMap.Enable();
    }

    private void Start()
    {
        // Use a separate frame so camera-relative movement cannot feed the
        // player's steering rotation back into the camera orbit.
        if (cameraUp == null || cameraUp == transform || cameraUp.IsChildOf(transform))
        {
            cameraUp = new GameObject("Player Camera Frame").transform;
            ownsCameraUp = true;
        }

        cameraUp.rotation = Quaternion.FromToRotation(Vector3.up, transform.up);
        foreach (CinemachineCamera camera in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
        {
            Transform target = camera.Follow;
            if (target == null || (target != transform && !target.IsChildOf(transform)))
                continue;

            cameraFollowTarget = target;
            cameraUp.position = target.position;
            camera.Follow = cameraUp;
            CinemachineOrbitalFollow orbit = camera.GetComponent<CinemachineOrbitalFollow>();
            if (orbit != null)
            {
                orbit.TrackerSettings.BindingMode = BindingMode.LockToTarget;
                orbit.TrackerSettings.PositionDamping = Vector3.zero;
                orbit.TrackerSettings.RotationDamping = Vector3.zero;
                orbit.TrackerSettings.QuaternionDamping = 0f;
            }
        }

        Camera outputCamera = cameraTransform != null
            ? cameraTransform.GetComponent<Camera>() : Camera.main;
        if (outputCamera != null)
        {
            cameraTransform = outputCamera.transform;
            if (outputCamera.TryGetComponent(out CinemachineBrain brain))
            {
                brain.WorldUpOverride = cameraUp;
                // The proxy is moved in LateUpdate from the interpolated player
                // pose, so Cinemachine must sample it on the render clock too.
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
                brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            }
        }
    }

    private void OnDestroy()
    {
        if (ownsCameraUp && cameraUp != null)
            Destroy(cameraUp.gameObject);
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

        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        PlayerMoveAndRotate();
        PlayerJump();
    }

    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }
        
        float animSpeed = Vector2.ClampMagnitude(playerMoveAmount, 1f).magnitude;
        animator.SetFloat(SpeedHash, animSpeed, 0.1f, Time.deltaTime);
        animator.SetBool(IsGroundedHash, groundCheck.IsGrounded);
        
        animator.SetFloat(
            VerticalSpeedHash,
            Vector3.Dot(playerRigidbody.linearVelocity, LocalUp)
        );
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

            if (animator != null)
            {
                animator.SetTrigger(JumpHash);
            }
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
            Quaternion.FromToRotation(cameraUp.up, transform.up)
            * cameraUp.rotation;
        if (cameraFollowTarget != null)
            cameraUp.position = cameraFollowTarget.position;
    }
}
