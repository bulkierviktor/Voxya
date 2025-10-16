using System.Diagnostics;
using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target; // El jugador al que seguiremos

    [Header("Camera Settings")]
    public float distance = 5.0f;
    public float mouseSensitivity = 5.0f;
    public Vector2 pitchMinMax = new Vector2(-40, 85);
    public float rotationSmoothTime = 0.12f;

    private float yaw;
    private float pitch;
    private Vector3 rotationSmoothVelocity;
    private Vector3 currentRotation;

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("La cámara necesita un objetivo (target) para seguir.");
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null) return;

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

        currentRotation = Vector3.SmoothDamp(currentRotation, new Vector3(pitch, yaw), ref rotationSmoothVelocity, rotationSmoothTime);
        transform.eulerAngles = currentRotation;

        Vector3 targetPosition = target.position - transform.forward * distance;

        transform.position = targetPosition;
    }
}