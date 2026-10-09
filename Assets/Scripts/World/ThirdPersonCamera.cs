using UnityEngine;

namespace DreamWorld.World
{
    // Elden Ring-style follow camera: the mouse looks around (cursor hidden and locked while playing),
    // scroll zooms. When the player locks on (Q), the camera swings behind them and keeps the target
    // in view. Pulls in closer if a wall or tree is in the way. Menus free the cursor (CursorFree).
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public float height = 1.35f;
        public float distance = 4.5f;
        public float minDistance = 1.8f;
        public float maxDistance = 9f;
        public float yaw;
        public float pitch = 14f;
        public float mouseSensitivity = 2.2f;
        public float smoothing = 12f;
        public LayerMask obstacles = ~0;

        // Set by menus (pause, level up, banking): shows the cursor and stops mouse-look.
        public static bool CursorFree { get; set; }

        PlayerController player;

        void Start()
        {
            if (target != null) yaw = target.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (target == null) return;
            if (player == null || player.transform != target) player = target.GetComponent<PlayerController>();

            bool free = CursorFree || !Application.isFocused;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;

            var lockOn = player != null ? player.LockTarget : null;
            if (lockOn != null)
            {
                // swing behind the player, looking toward the target
                Vector3 to = lockOn.Body.position - target.position;
                to.y = 0f;
                float wantYaw = Quaternion.LookRotation(to.sqrMagnitude > 0.01f ? to : target.forward).eulerAngles.y;
                yaw = Mathf.LerpAngle(yaw, wantYaw, 1f - Mathf.Exp(-8f * Time.deltaTime));
                pitch = Mathf.Lerp(pitch, 16f, 1f - Mathf.Exp(-4f * Time.deltaTime));
            }
            else if (!free)
            {
                yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            }
            pitch = Mathf.Clamp(pitch, -10f, 65f);
            if (!free) distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 0.5f, minDistance, maxDistance);

            Vector3 focus = target.position + Vector3.up * height;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            float d = distance;
            // don't let trees/houses block the view: stop just in front of what we hit
            if (Physics.SphereCast(focus, 0.2f, rotation * Vector3.back, out var hit, distance, obstacles, QueryTriggerInteraction.Ignore)
                && hit.transform != target && !hit.transform.IsChildOf(target))
                d = Mathf.Max(minDistance * 0.5f, hit.distance - 0.1f);
            Vector3 desired = focus + rotation * Vector3.back * d;

            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            Vector3 lookAt = lockOn != null ? Vector3.Lerp(focus, lockOn.Body.position + Vector3.up * 0.6f, 0.35f) : focus;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookAt - transform.position, Vector3.up),
                                                  1f - Mathf.Exp(-18f * Time.deltaTime));
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
