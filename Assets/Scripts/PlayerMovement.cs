using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    private CharacterController controller;
    private Transform cameraTransform;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 12f;

    [Header("Gravity & Jump")]
    public float gravity = -20f;
    public float jumpHeight = 1.5f;
    private float verticalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        if (controller == null || !controller.enabled) return;

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical += 1f;
        }

        Vector3 inputDir = new Vector3(horizontal, 0f, vertical).normalized;

        // Yerçekimi ve Zıplama
        if (controller.isGrounded)
        {
            verticalVelocity = -2f;

            bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            if (jumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 moveDirection = Vector3.zero;

        if (inputDir.magnitude >= 0.1f)
        {
            // Kameranın İleri ve Sağ vektörlerini al, Y eksenini sıfırla
            Vector3 camForward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
            
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            // A basınca sola, D basınca sağa temiz yönlenme
            moveDirection = (camForward * inputDir.z) + (camRight * inputDir.x);

            // Karakteri hareket ettiği yöne doğru akıcı bir şekilde döndür
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        bool isRunning = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        float currentSpeed = isRunning ? moveSpeed * 1.5f : moveSpeed;

        Vector3 finalMove = moveDirection * currentSpeed;
        finalMove.y = verticalVelocity;

        controller.Move(finalMove * Time.deltaTime);
    }
}