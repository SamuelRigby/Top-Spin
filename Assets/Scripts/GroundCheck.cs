using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class GroundCheck : MonoBehaviour
{
    public bool IsGrounded => CheckIsGrounded();
    private Rigidbody _rigidbody;
    private float _playerRadius;
    private float _playerHeight;
    private float _distance;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        CapsuleCollider capsuleCollider = GetComponent<CapsuleCollider>();
        _playerRadius = capsuleCollider.radius;
        _playerHeight = capsuleCollider.height;
        _distance = _playerHeight - (_playerRadius * 2) + 0.1f;
    }

    private bool CheckIsGrounded()
    {
        Vector3 position = _rigidbody.transform.position;
        Vector3 direction = -_rigidbody.transform.up;
        return Physics.SphereCast(position, _playerRadius, direction, out RaycastHit _, _distance);
    }
}
