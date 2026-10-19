using TMPro;
using UnityEngine;

public class WorldTextPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    private Transform mainCameraTransform;

    void Start()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        if (mainCameraTransform != null)
        {
            transform.rotation = mainCameraTransform.rotation;
        }
    }

    public void Init(string message, int fontSize, Color color)
    {
        text.text = message;
        text.fontSize = fontSize;
        text.color = color;
    }
}
