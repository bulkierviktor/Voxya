using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public float turnSmoothTime = 0.1f;

    [Header("Physics")]
    public float gravity = -19.62f;
    public float jumpHeight = 1.5f;

    private CharacterController controller;
    private Transform cam;
    private Vector3 velocity;
    private float turnSmoothVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main.transform;
    }

    void Update()
    {
        // --- GRAVEDAD ---
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // --- MOVIMIENTO (WASD) ---
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        if (direction.magnitude >= 0.1f)
        {
            // Calcula el ángulo de movimiento relativo a la cámara
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            // Mueve al jugador en la dirección correcta
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            controller.Move(moveDir.normalized * moveSpeed * Time.deltaTime);
        }

        // --- SALTO ---
        if (Input.GetButtonDown("Jump") && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Aplicar la velocidad de la gravedad y el salto
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}