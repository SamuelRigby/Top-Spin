using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(GameObject))]
[RequireComponent(typeof(GroundCheck))]
public class CustomGravity : MonoBehaviour
{
    [SerializeField] private float _gravityMagnitude = 9.81f;
    private Rigidbody _rigidbody;
    private GroundCheck _groundCheck;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.useGravity = false;
        _groundCheck = GetComponent<GroundCheck>();
    }

    void FixedUpdate()
    {
        ApplyGravity();
    }

    public Vector3 GetGravityDirection()
    {
        Vector3 vector3D = _rigidbody.gameObject.transform.position - Vector3.zero;
        return new Vector3(vector3D.x, vector3D.y, 0).normalized;
    }

    public Vector3 GetCentrifugalDirection()
    {
        Vector3 gravityDirection = GetGravityDirection();
        Vector2 centrifugalDirection = Vector2.Perpendicular(new Vector2(gravityDirection.x, gravityDirection.y));
        return (Vector3) centrifugalDirection.normalized;
    }

    private void ApplyGravity()
    {
        Vector3 gravityForce = _gravityMagnitude * GetGravityDirection();


        Vector3 centrifugalForce = Vector3.zero;
        if (!_groundCheck.IsGrounded)
        {
            centrifugalForce = (_gravityMagnitude / 2) * GetCentrifugalDirection();
        }

        Vector3 compositeForce = gravityForce + centrifugalForce;
        _rigidbody.AddForce(compositeForce, ForceMode.Acceleration);
    }
}
