using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(GroundCheck))]
public class CustomGravity : MonoBehaviour
{
    [SerializeField] private float _gravityMagnitude = 9.81f;
    [SerializeField] private float _centrifugalMagnitude = 9.81f;
    [SerializeField] private float _maxRadialDistance = 100f;
    private float RadialDistance => (Vector3.zero - transform.position).magnitude;
    private float _gravityMultiplier;
    private float _centrifugalMultiplier;
    private int _touchingWorldCount;
    private bool IsCollidingWorld => _touchingWorldCount > 0;
    private Rigidbody _rigidbody;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.useGravity = false;
        _maxRadialDistance -= GetComponent<CapsuleCollider>().height / 2;
        CalculateMultipliers();
    }

    void FixedUpdate()
    {
        if (IsCollidingWorld)
        {
            CalculateMultipliers();
        }

        ApplyGravity();
    }

    public Vector3 GetGravityDirection()
    {
        Vector3 vector3D = transform.position - Vector3.zero;
        return new Vector3(vector3D.x, vector3D.y, 0).normalized;
    }

    public Vector3 GetCentrifugalDirection()
    {
        Vector2 centrifugalDirection = Vector2.Perpendicular((Vector2)GetGravityDirection());
        return (Vector3)centrifugalDirection.normalized;
    }

    private void ApplyGravity()
    {
        Vector3 gravityForce = _gravityMultiplier * _gravityMagnitude * GetGravityDirection();
        Vector3 centrifugalForce = Vector3.zero;
        if (!IsCollidingWorld)
        {
            centrifugalForce = _centrifugalMultiplier * _centrifugalMagnitude * GetCentrifugalDirection();
        }

        Vector3 compositeForce = gravityForce + centrifugalForce;
        _rigidbody.AddForce(compositeForce, ForceMode.Acceleration);
    }

    private void CalculateMultipliers()
    {
        _gravityMultiplier = RadialDistance / _maxRadialDistance;
        _centrifugalMultiplier = 1 - RadialDistance / _maxRadialDistance;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == 6)
        {
            _touchingWorldCount++;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.layer == 6)
        {
            _touchingWorldCount--;
        }
    }
}
