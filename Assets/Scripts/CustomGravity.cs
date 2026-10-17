using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(GameObject))]
public class CustomGravity : MonoBehaviour
{
    [SerializeField] private float _gravityMagnitude = 9.81f;
    private Rigidbody _rigidbody;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.useGravity = false;
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

    private void ApplyGravity()
    {
        Vector3 gravity = _gravityMagnitude * GetGravityDirection();
        _rigidbody.AddForce(gravity, ForceMode.Acceleration);
    }
}
