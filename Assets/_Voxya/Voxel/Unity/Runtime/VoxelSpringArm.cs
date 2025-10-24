using UnityEngine;

// Brazo de cámara de tercera persona "estilo Cube World":
// - Orbit con ratón (yaw/pitch), zoom con rueda.
// - Colisión de cámara (sphere cast) para no atravesar geometría.
// - Offset "sobre el hombro" ajustable.
// - Suavizados para evitar jitter.
[DisallowMultipleComponent]
public class VoxelSpringArm : MonoBehaviour
{
    [Header("Target")]
    public Transform target;                 // Normalmente el transform del Player (raíz)
    public Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f); // Altura del pivot (ojos/hombros)

    [Header("Orbit")]
    public float mouseSensitivity = 120f;    // deg/segundo
    public float minPitch = -35f;
    public float maxPitch = 65f;
    public bool lockCursor = true;

    [Header("Distancia y zoom")]
    public float distance = 5.5f;            // distancia por defecto
    public float minDistance = 1.6f;
    public float maxDistance = 7.5f;
    public float zoomSpeed = 4f;

    [Header("Offset de hombro")]
    public float shoulderOffset = 0.45f;     // desplazamiento lateral (metros) para vista sobre el hombro
    public bool shoulderRight = true;        // falso = hombro izquierdo

    [Header("Colisión de cámara")]
    public float probeRadius = 0.25f;
    public LayerMask collisionMask = ~0;     // por defecto, todo
    public float collisionBuffer = 0.1f;

    [Header("Suavizados")]
    public float rotationLerp = 20f;         // rotación del pivot
    public float positionLerp = 20f;         // movimiento de la cámara

    // Estado
    private float yaw;
    private float pitch;
    private Vector3 currentCamPos;

    // Exposición para otros (el controller puede leer estos ángulos)
    public float Yaw => yaw;
    public float Pitch => pitch;

    void Start()
    {
        if (target == null)
            Debug.LogWarning("[VoxelSpringArm] target no asignado.");
        var e = transform.eulerAngles;
        yaw = e.y;
        pitch = Mathf.Clamp(e.x, minPitch, maxPitch);

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        currentCamPos = transform.position;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1) Entrada de ratón para orbit
        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y");
        yaw += mx * mouseSensitivity * Time.deltaTime;
        pitch -= my * mouseSensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // 2) Zoom con rueda
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
        }

        // 3) Calcula pivot y rotación deseada
        Vector3 pivot = target.position + pivotOffset;
        Quaternion desiredRot = Quaternion.Euler(pitch, yaw, 0f);

        // 4) Offset lateral "sobre el hombro"
        Vector3 side = (shoulderRight ? 1f : -1f) * (Vector3)(Quaternion.Euler(0f, yaw, 0f) * Vector3.right) * shoulderOffset;

        // 5) Punto ideal de cámara sin colisión
        Vector3 desiredCamPos = pivot + side - (desiredRot * Vector3.forward) * distance;

        // 6) Colisión de cámara con sphere cast (desde el pivot hacia la cámara)
        Vector3 dir = (desiredCamPos - pivot);
        float dist = dir.magnitude;
        Vector3 safeCamPos = desiredCamPos;
        if (dist > 0.001f)
        {
            dir /= dist;
            if (Physics.SphereCast(pivot, probeRadius, dir, out RaycastHit hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
            {
                safeCamPos = hit.point - dir * collisionBuffer;
            }
        }

        // 7) Interpolaciones suaves
        // rotación del "brazo/pivot": en este script usamos transform del propio SpringArm como pivot
        transform.position = Vector3.Lerp(transform.position, pivot, 1f - Mathf.Exp(-positionLerp * Time.deltaTime));
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, 1f - Mathf.Exp(-rotationLerp * Time.deltaTime));

        // posiciona la cámara (este script debe estar en el GameObject que tiene la Camera)
        currentCamPos = Vector3.Lerp(currentCamPos, safeCamPos, 1f - Mathf.Exp(-positionLerp * Time.deltaTime));
        transform.position = currentCamPos;

        // Nota: si prefieres separar pivot y cámara en dos objetos, puedes:
        // - Dejar un "Pivot" con el yaw/pitch y este script allí, y hacer que la Camera sea hija del Pivot.
        // - Para simplicidad, aquí usamos el propio transform como cámara, ajustándolo a safeCamPos.
    }
}