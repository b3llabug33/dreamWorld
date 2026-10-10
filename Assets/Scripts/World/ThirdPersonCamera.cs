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
        // Inventory view: swing around in front of the player so you see her straight on, standing to
        // the left of the bag panel (like a character screen).
        public static bool Showcase { get; set; }

        PlayerController player;
        bool snap;

        // Jump straight to the follow position next frame instead of gliding there (after a teleport).
        public void SnapNext() => snap = true;

        void Start()
        {
            if (target != null) yaw = target.eulerAngles.y;
        }

        // Sprinting widens the view a little (eases in and out).
        public float sprintFovBoost = 6f;
        float baseFov = -1f, fovBoost;
        Camera cam;

        void LateUpdate()
        {
            if (target == null) return;
            if (player == null || player.transform != target) player = target.GetComponent<PlayerController>();
            if (cam == null) cam = GetComponent<Camera>();
            if (cam != null)
            {
                if (baseFov < 0f) baseFov = cam.fieldOfView;
                bool sprint = player != null && player.Sprinting && !Showcase;
                fovBoost = Mathf.MoveTowards(fovBoost, sprint ? sprintFovBoost : 0f, Time.deltaTime * sprintFovBoost * 3f);
                cam.fieldOfView = baseFov + fovBoost;
            }

            bool free = CursorFree || !Application.isFocused;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;

            if (Showcase)
            {
                Vector3 facing = target.forward;
                facing.y = 0f;
                facing = facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward;
                Vector3 chest = target.position + Vector3.up * 1.0f;
                Vector3 cameraRight = Vector3.Cross(Vector3.up, -facing);   // the camera looks back at her
                Vector3 spot = chest + facing * 2.6f + Vector3.up * 0.12f + cameraRight * 0.15f;
                Vector3 aim = chest + cameraRight * 0.75f;                   // aim right of her, so she sits left of center
                float k = 1f - Mathf.Exp(-6f * (Time.timeScale > 0f ? Time.deltaTime : Time.unscaledDeltaTime));   // game time, so recordings stay smooth
                transform.position = Vector3.Lerp(transform.position, spot, k);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aim - transform.position, Vector3.up), k);
                // keep the orbit angle behind her, so closing the bag glides back to the normal view
                yaw = Quaternion.LookRotation(facing).eulerAngles.y;
                return;
            }

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

            transform.position = snap ? desired : Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            Vector3 lookAt = lockOn != null ? Vector3.Lerp(focus, lockOn.Body.position + Vector3.up * 0.6f, 0.35f) : focus;
            var look = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
            transform.rotation = snap ? look : Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-18f * Time.deltaTime));
            snap = false;
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
