using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CylinderRotation : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 9.81f;
    private Rigidbody _rigidBody;

    void Awake()
    {
        _rigidBody = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        Quaternion deltaRotation = Quaternion.Euler(0, 0, rotationSpeed * Time.deltaTime);
        _rigidBody.MoveRotation(deltaRotation * _rigidBody.rotation);
    }
}
