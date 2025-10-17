using UnityEngine;
using UnityEngine.InputSystem; // Asegúrate de que esta línea existe

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Settings")]
    public float distance = 5.0f;
    public float mouseSensitivity = 100f;
    public Vector2 pitchMinMax = new Vector2(-40, 85);
    public float rotationSmoothTime = 0.12f;

    private float yaw;
    private float pitch;
    private Vector2 lookInput;
    private Vector3 rotationSmoothVelocity;
    private Vector3 currentRotation;

    void Start()
    {
        if (target == null) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // --- Método de Input (¡Modificado!) ---
    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        yaw += lookInput.x * mouseSensitivity * Time.deltaTime;
        pitch -= lookInput.y * mouseSensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

        currentRotation = Vector3.SmoothDamp(currentRotation, new Vector3(pitch, yaw), ref rotationSmoothVelocity, rotationSmoothTime);
        transform.eulerAngles = currentRotation;

        Vector3 targetPosition = target.position - transform.forward * distance;
        transform.position = targetPosition;
    }
}