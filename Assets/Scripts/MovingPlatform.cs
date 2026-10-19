using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MovingPlatform : MonoBehaviour
{
    [SerializeField] private AnimationCurve _accelerationCurve;
    [SerializeField] private Transform _point1;
    [SerializeField] private Transform _point2;
    [SerializeField] private float _transitionTime;
    [SerializeField] private float _cooldownTime;
    private Transform _currPoint;
    private Transform _targetPoint;
    private Vector3 _deltaPosition;
    private float _transitionTimer;
    private bool _onCooldown;
    private Rigidbody _rigidbody;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        GameObject map = GameObject.Find("Map");
        _point1.SetParent(map.transform);
        _point2.SetParent(map.transform);
        transform.position = _point1.position;
        _currPoint = _point1;
        _targetPoint = _point2;
    }

    private void FixedUpdate()
    {
        if (!_onCooldown)
        {
            Oscillate();
        }
        else
        {
            _rigidbody.MovePosition(_currPoint.position);
        }

        _rigidbody.MoveRotation(_currPoint.rotation);
    }

    private void Oscillate()
    {
        float transitionPercentage = _transitionTimer / _transitionTime;
        Vector3 lastPosition = _rigidbody.position;
        Vector3 nextPosition = Vector3.Lerp(_currPoint.position, _targetPoint.position, _accelerationCurve.Evaluate(transitionPercentage));
        _rigidbody.MovePosition(nextPosition);
        _deltaPosition = nextPosition - lastPosition;

        if (transitionPercentage >= 1)
        {
            if (_targetPoint == _point1)
            {
                _currPoint = _point1;
                _targetPoint = _point2;
            }
            else
            {
                _currPoint = _point2;
                _targetPoint = _point1;
            }

            _transitionTimer = 0;
            _deltaPosition = Vector3.zero;
            StartCoroutine(Cooldown());
            return;
        }

        _transitionTimer += Time.fixedDeltaTime;
    }

    IEnumerator Cooldown()
    {
        _onCooldown = true;
        yield return new WaitForSeconds(_cooldownTime);
        _onCooldown = false;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            collision.gameObject.GetComponent<Rigidbody>().position += _deltaPosition;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Vector3 exitVelocity = _deltaPosition / Time.fixedDeltaTime;
            collision.gameObject.GetComponent<Rigidbody>().linearVelocity += exitVelocity;
        }
    }
}
