using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float rotationSpeed = 10f;

    [Header("Gravity")]
    public float gravity = -9.81f;

    [Header("Jump")]
    public float jumpHeight = 1.5f;

    private CharacterController controller;
    private Vector3 verticalVelocity;
    private Transform cameraTransform;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        HandleMovement();
        HandleJump();
        HandleGravity();
    }

    void HandleMovement()
    {
        Vector2 input = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
                input.y += 1f;

            if (Keyboard.current.sKey.isPressed)
                input.y -= 1f;

            if (Keyboard.current.dKey.isPressed)
                input.x += 1f;

            if (Keyboard.current.aKey.isPressed)
                input.x -= 1f;
        }

        Vector3 direction = Vector3.zero;

        if (cameraTransform != null)
        {
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;

            cameraForward.y = 0f;
            cameraRight.y = 0f;

            cameraForward.Normalize();
            cameraRight.Normalize();

            direction =
                (cameraForward * input.y +
                 cameraRight * input.x).normalized;
        }

        if (direction.magnitude >= 0.1f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

            float currentSpeed = walkSpeed;

            if (Keyboard.current != null &&
                Keyboard.current.leftShiftKey.isPressed)
            {
                currentSpeed = runSpeed;
            }

            controller.Move(
                direction * currentSpeed * Time.deltaTime
            );
        }
    }

    void HandleJump()
    {
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame &&
            controller.isGrounded)
        {
            verticalVelocity.y =
                Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    void HandleGravity()
    {
        if (controller.isGrounded &&
            verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }

        verticalVelocity.y += gravity * Time.deltaTime;

        controller.Move(
            verticalVelocity * Time.deltaTime
        );
    }
}