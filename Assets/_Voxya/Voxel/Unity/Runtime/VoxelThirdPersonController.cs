using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class VoxelThirdPersonController : MonoBehaviour
{
    public Transform cameraTransform;
    public VoxelSpringArm springArm;

    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float airControl = 0.25f;
    public float acceleration = 12f;
    public float rotationLerp = 12f;

    public float jumpSpeed = 6f;
    public float gravity = 24f;
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.12f;

    public bool alignToCameraWhenIdle = true;

    CharacterController cc;
    Vector3 velocity;
    float groundedTimer;
    float jumpBufferTimer;

    void Awake() { cc = GetComponent<CharacterController>(); }

    void Start()
    {
        if (cameraTransform == null) { var cam = Camera.main; if (cam) cameraTransform = cam.transform; }
        if (springArm == null && cameraTransform != null) springArm = cameraTransform.GetComponent<VoxelSpringArm>();
    }

    void Update()
    {
        Vector2 input = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        input = Vector2.ClampMagnitude(input, 1f);
        bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (Input.GetButtonDown("Jump")) jumpBufferTimer = jumpBuffer;

        Vector3 camFwd = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        if (camFwd.sqrMagnitude < 1e-4f) camFwd = transform.forward;
        Vector3 camRight = new(camFwd.z, 0f, -camFwd.x);

        Vector3 wishDir = (camFwd * input.y + camRight * input.x);
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        float targetSpeed = sprint ? sprintSpeed : walkSpeed;
        Vector3 targetVelXZ = wishDir * targetSpeed;

        bool grounded = cc.isGrounded;
        if (grounded) groundedTimer = coyoteTime; else groundedTimer -= Time.deltaTime;
        jumpBufferTimer -= Time.deltaTime;

        velocity.y -= gravity * Time.deltaTime;
        if (grounded && velocity.y < -2f) velocity.y = -2f;

        if (jumpBufferTimer > 0f && groundedTimer > 0f)
        {
            velocity.y = jumpSpeed;
            jumpBufferTimer = 0f; groundedTimer = 0f;
        }

        Vector3 velXZ = new(velocity.x, 0f, velocity.z);
        float accel = grounded ? acceleration : acceleration * airControl;
        velXZ = Vector3.Lerp(velXZ, targetVelXZ, 1f - Mathf.Exp(-accel * Time.deltaTime));
        velocity.x = velXZ.x; velocity.z = velXZ.z;

        cc.Move(velocity * Time.deltaTime);

        Vector3 forwardPlanar = wishDir.sqrMagnitude > 0.001f ? wishDir : (alignToCameraWhenIdle ? camFwd : transform.forward);
        Quaternion targetRot = Quaternion.LookRotation(forwardPlanar, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-rotationLerp * Time.deltaTime));
    }
}