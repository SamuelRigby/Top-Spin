using UnityEngine;
using UnityEngine.InputSystem;

public class SimplePlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 5f;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.qKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.dKey.isPressed) horizontal += 1f;
        if (Keyboard.current.zKey.isPressed) vertical += 1f;
        if (Keyboard.current.sKey.isPressed) vertical -= 1f;

        Vector3 movement = new Vector3(horizontal, 0f, vertical).normalized;

        transform.position += movement * speed * Time.deltaTime;
    }
}