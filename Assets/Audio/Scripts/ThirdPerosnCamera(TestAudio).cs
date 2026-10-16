using UnityEngine;

public class SimpleThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;

    [Header("Camera")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -6f);
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private float lookHeight = 1.5f;

    private void LateUpdate()
    {
        if (player == null)
            return;

        Vector3 targetPosition = player.position + offset;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );

        transform.LookAt(player.position + Vector3.up * lookHeight);
    }
}
