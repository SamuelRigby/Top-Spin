using UnityEngine;
using UnityEngine.Splines;

public class SplineAudioFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private Transform player;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float followSpeed = 10f;

    private float currentT;

    private void Start()
    {
        if (splineContainer == null || player == null)
        {
            Debug.LogError("SplineAudioFollower : référence manquante.", this);
            enabled = false;
            return;
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        UpdateClosestPoint();

        if (audioSource != null && !audioSource.isPlaying)
            audioSource.Play();
    }

    private void Update()
    {
        if (splineContainer == null || player == null)
            return;

        UpdateClosestPoint();
    }

    private void UpdateClosestPoint()
    {
        var spline = splineContainer.Spline;

        if (spline == null || spline.Count < 2)
            return;

        Vector3 playerLocal =
            splineContainer.transform.InverseTransformPoint(player.position);

        SplineUtility.GetNearestPoint(
            spline,
            new Unity.Mathematics.float3(
                playerLocal.x, playerLocal.y, playerLocal.z),
            out var nearest,
            out currentT
        );

        Vector3 localPosition =
            new Vector3(nearest.x, nearest.y, nearest.z);

        Vector3 targetPosition =
            splineContainer.transform.TransformPoint(localPosition);

        transform.position = targetPosition;
    }
}