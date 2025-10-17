using UnityEngine;
using UnityEngine.InputSystem;

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

    private Vector2 moveInput;
    private bool jumpInput;

    // ¡NUEVO! Esta línea va con las otras variables privadas.
    // Controla si el jugador puede moverse. Empieza en 'false'.
    private bool controlsEnabled = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main.transform;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        jumpInput = context.action.triggered;
    }

    // ¡NUEVO! Este método completo lo puedes añadir después de OnJump y antes de Update.
    // Es la función que llamará el WorldGenerator para "despertar" al jugador.
    public void EnableControls()
    {
        controlsEnabled = true;
    }

    void Update()
    {
        // ¡NUEVO! Esta es la primera línea que debes añadir dentro de Update().
        // Si los controles no están habilitados, se salta todo lo demás.
        if (!controlsEnabled) return;

        // El resto del método Update() se queda como estaba.
        HandleGravity();
        HandleMovement();
    }

    private void HandleGravity()
    {
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleMovement()
    {
        Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        if (direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            controller.Move(moveDir.normalized * moveSpeed * Time.deltaTime);
        }

        if (jumpInput && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpInput = false;
        }
    }
}