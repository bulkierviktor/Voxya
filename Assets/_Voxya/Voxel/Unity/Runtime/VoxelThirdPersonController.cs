using UnityEngine;

// Controlador de personaje de tercera persona "estilo Cube World":
// - Movimiento con CharacterController (tierra/aire), gravedad, salto, sprint.
// - Dirección de movimiento relativa a cámara (orbit).
// - Rotación suave del personaje hacia la dirección de avance o hacia el yaw de cámara cuando estás parado.
// - Pensado para trabajar con VoxelSpringArm (la cámara) y con VoxelWorld (streaming front-first).
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class VoxelThirdPersonController : MonoBehaviour
{
    [Header("Referencias")]
    public Transform cameraTransform;      // Asigna la Camera del VoxelSpringArm
    public VoxelSpringArm springArm;       // Asigna el componente VoxelSpringArm

    [Header("Movimiento")]
    public float walkSpeed = 5.0f;
    public float sprintSpeed = 8.0f;
    public float airControl = 0.25f;       // 0..1: cuánto control tienes en el aire
    public float acceleration = 12f;       // respuesta de aceleración
    public float rotationLerp = 12f;

    [Header("Salto/Gravedad")]
    public float jumpSpeed = 6.0f;
    public float gravity = 24.0f;
    public float coyoteTime = 0.12f;       // margen para saltar justo al borde
    public float jumpBuffer = 0.12f;       // margen para cachear el botón antes de tocar suelo

    [Header("Opciones")]
    public bool alignToCameraWhenIdle = true; // si estás parado, el personaje mira donde mira la cámara

    private CharacterController cc;
    private Vector3 velocity;             // velocidad acumulada (incluye eje Y)
    private float groundedTimer;          // tiempo desde última vez en suelo
    private float jumpBufferTimer;        // tiempo desde última pulsación de salto

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    void Start()
    {
        if (cameraTransform == null)
        {
            Camera cam = Camera.main;
            if (cam) cameraTransform = cam.transform;
        }
        if (springArm == null && cameraTransform != null)
            springArm = cameraTransform.GetComponent<VoxelSpringArm>();
    }

    void Update()
    {
        // 1) Entrada (teclas por defecto del Input Manager)
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        input = Vector2.ClampMagnitude(input, 1f);

        bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (Input.GetButtonDown("Jump")) jumpBufferTimer = jumpBuffer;

        // 2) Cálculo de dirección relativa a cámara (plano XZ)
        Vector3 camFwd = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        if (camFwd.sqrMagnitude < 1e-4f) camFwd = transform.forward;
        Vector3 camRight = new Vector3(camFwd.z, 0f, -camFwd.x); // perpendicular en XZ

        Vector3 wishDir = (camFwd * input.y + camRight * input.x);
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        float targetSpeed = sprint ? sprintSpeed : walkSpeed;
        Vector3 targetVelXZ = wishDir * targetSpeed;

        // 3) Suelo/aire y salto
        bool grounded = cc.isGrounded;
        if (grounded) groundedTimer = coyoteTime;
        else groundedTimer -= Time.deltaTime;

        jumpBufferTimer -= Time.deltaTime;

        // aplica gravedad continuo
        velocity.y -= gravity * Time.deltaTime;

        // Si estamos en suelo, pegamos el Y a un pequeño valor hacia abajo
        if (grounded && velocity.y < -2f) velocity.y = -2f;

        // Salto si procede (buffer + coyote)
        if (jumpBufferTimer > 0f && groundedTimer > 0f)
        {
            velocity.y = jumpSpeed;
            jumpBufferTimer = 0f;
            groundedTimer = 0f;
        }

        // 4) Aceleración horizontal (XZ)
        Vector3 velXZ = new Vector3(velocity.x, 0f, velocity.z);
        float accel = grounded ? acceleration : (acceleration * airControl);
        velXZ = Vector3.Lerp(velXZ, targetVelXZ, 1f - Mathf.Exp(-accel * Time.deltaTime));
        velocity.x = velXZ.x;
        velocity.z = velXZ.z;

        // 5) Movimiento
        cc.Move(velocity * Time.deltaTime);

        // 6) Rotación del personaje
        Vector3 forwardPlanar = wishDir.sqrMagnitude > 0.001f ? wishDir : (alignToCameraWhenIdle ? camFwd : transform.forward);
        Quaternion targetRot = Quaternion.LookRotation(forwardPlanar, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-rotationLerp * Time.deltaTime));
    }
}