using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[DefaultExecutionOrder(-100)]
public class CustomGravity : MonoBehaviour
{
    [SerializeField] private float _gravityMagnitude = 9.81f;
    [SerializeField] private float _centrifugalMagnitude = 9.81f;
    [SerializeField] private float _maxRadialDistance = 100f;
    private float _gravityMultiplier;
    private int _touchingWorldCount;
    private bool IsCollidingWorld => _touchingWorldCount > 0;
    private Rigidbody _rigidbody;

    private void Awake()
    {
        _maxRadialDistance -= GetComponent<CapsuleCollider>().height / 2;
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.useGravity = false;
        CalculateMultipliers();
    }

    private void FixedUpdate()
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

    private void ApplyGravity()
    {

        if (_rigidbody.linearVelocity.y >= _gravityMagnitude)
        {
            return;
        }

        Vector3 gravityForce = _gravityMultiplier * _gravityMagnitude * GetGravityDirection();
        Vector3 compositeForce = gravityForce;
        _rigidbody.AddForce(compositeForce, ForceMode.Acceleration);
    }

    private void CalculateMultipliers()
    {
        _gravityMultiplier = (Vector3.zero - transform.position).magnitude / _maxRadialDistance;
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
