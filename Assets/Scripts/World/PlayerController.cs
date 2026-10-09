using DreamWorld.CharacterCreation;
using UnityEngine;

namespace DreamWorld.World
{
    // Third-person movement: WASD/arrows relative to the camera, Shift to run, Space to jump.
    // Turns the character to face where she walks and drives IdleMotion's walk cycle.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Transform cameraTransform;
        public float walkSpeed = 2.2f;
        public float runSpeed = 4.6f;
        public float acceleration = 12f;
        public float turnSmoothing = 12f;
        public float gravity = -18f;
        public float jumpSpeed = 5f;
        [Tooltip("Meters per full step cycle (two steps).")]
        public float strideLength = 1.3f;
        [Tooltip("Keeps her inside the play area.")]
        public float areaRadius = 46f;

        CharacterController controller;
        IdleMotion motion;
        float speed;
        float verticalSpeed;

        void Start()
        {
            controller = GetComponent<CharacterController>();
            motion = GetComponentInChildren<IdleMotion>();
            if (cameraTransform == null) cameraTransform = Camera.main.transform;
        }

        void Update()
        {
            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            Vector3 direction = forward * input.y + right * input.x;
            if (direction.sqrMagnitude > 1) direction.Normalize();

            bool running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float target = direction.magnitude * (running ? runSpeed : walkSpeed);
            speed = Mathf.MoveTowards(speed, target, acceleration * Time.deltaTime);

            if (direction.sqrMagnitude > 0.01f)
            {
                var look = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-turnSmoothing * Time.deltaTime));
            }

            if (controller.isGrounded)
            {
                verticalSpeed = -1f;   // keeps her snapped to the ground on slopes
                if (Input.GetKeyDown(KeyCode.Space)) verticalSpeed = jumpSpeed;
            }
            verticalSpeed += gravity * Time.deltaTime;

            Vector3 horizontal = (direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward) * speed;
            controller.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);

            // soft boundary: slide her back inside the area
            var flat = new Vector3(transform.position.x, 0, transform.position.z);
            if (flat.magnitude > areaRadius)
                controller.Move(-flat.normalized * (flat.magnitude - areaRadius));

            if (motion != null)
            {
                motion.moveAmount = Mathf.Clamp01(speed / runSpeed);
                motion.stridePhase += speed * Time.deltaTime / strideLength * Mathf.PI * 2f;
            }
        }
    }
}
