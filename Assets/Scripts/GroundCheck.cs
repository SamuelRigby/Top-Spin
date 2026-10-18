using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class GroundCheck : MonoBehaviour
{
    public bool IsGrounded => _groundColliders.Count > 0;

    private readonly HashSet<Collider> _groundColliders = new HashSet<Collider>();
    private CustomGravity _customGravity;

    private void Awake()
    {
        _customGravity = GetComponent<CustomGravity>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckGroundContact(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        CheckGroundContact(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        _groundColliders.Remove(collision.collider);
    }

    private void OnDisable()
    {
        _groundColliders.Clear();
    }

    private void CheckGroundContact(Collision collision)
    {
        Vector3 localUp = _customGravity != null
            ? -_customGravity.GetGravityDirection()
            : transform.up;

        if (localUp.sqrMagnitude < 0.0001f)
        {
            localUp = transform.up;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (Vector3.Dot(collision.GetContact(i).normal, localUp) >= 0.5f)
            {
                _groundColliders.Add(collision.collider);
                return;
            }
        }

        _groundColliders.Remove(collision.collider);
    }
}
