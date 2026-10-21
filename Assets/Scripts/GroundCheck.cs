using UnityEngine;

[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(CustomGravity))]
public class GroundCheck : MonoBehaviour
{
    [SerializeField] private float _checkDistance = 0.08f;
    [SerializeField] private float _minGroundDot = 0.5f;
    public bool IsGrounded { get; private set; }
    private CustomGravity _customGravity;
    private CapsuleCollider _capsule;

    private void Awake()
    {
        _customGravity = GetComponent<CustomGravity>();
        _capsule = GetComponent<CapsuleCollider>();
    }

    private void FixedUpdate()
    {
        IsGrounded = CheckGround();
    }

    private bool CheckGround()
    {
        Vector3 localUp = -_customGravity.GetGravityDirection();
        Vector3 origin = transform.TransformPoint(_capsule.center);
        float radius = _capsule.radius * 0.9f;
        float distance = _capsule.height / 2 - radius + _checkDistance;

        if (Physics.SphereCast(origin, radius, -localUp, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            return Vector3.Dot(hit.normal, localUp) >= _minGroundDot;
        }

        return false;
    }
}