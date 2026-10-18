using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CylinderRotation : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 9.81f;
    private Rigidbody _rigidBody;

    void Awake()
    {
        _rigidBody = GetComponent<Rigidbody>();
        // Match the player's interpolation so the level and camera render
        // the same point between physics steps.
        _rigidBody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void FixedUpdate()
    {
        Quaternion deltaRotation = Quaternion.Euler(0, 0, rotationSpeed * Time.fixedDeltaTime);
        _rigidBody.MoveRotation(deltaRotation * _rigidBody.rotation);
    }

    public Vector3 GetSurfaceVelocity(Vector3 worldPosition)
    {
        // Match the world Z axis used by FixedUpdate, in radians per second.
        Vector3 angularVelocity =
            Vector3.forward * rotationSpeed * Mathf.Deg2Rad;
        Vector3 offsetFromAxis = worldPosition - _rigidBody.position;

        return Vector3.Cross(angularVelocity, offsetFromAxis);
    }
}
