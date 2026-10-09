using UnityEngine;

namespace DreamWorld.World
{
    // Orbit camera that follows the player. Hold a mouse button and drag to look around,
    // Q/E to turn, scroll to zoom. Pulls in closer if a wall or tree is in the way.
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public float height = 1.35f;
        public float distance = 4.5f;
        public float minDistance = 1.8f;
        public float maxDistance = 9f;
        public float yaw;
        public float pitch = 14f;
        public float mouseSensitivity = 3f;
        public float keyTurnSpeed = 100f;
        public float smoothing = 12f;
        public LayerMask obstacles = ~0;

        Vector3 velocity;

        void Start()
        {
            if (target != null) yaw = target.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (target == null) return;
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            }
            if (Input.GetKey(KeyCode.Q)) yaw -= keyTurnSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.E)) yaw += keyTurnSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -5f, 65f);
            distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 0.5f, minDistance, maxDistance);

            Vector3 focus = target.position + Vector3.up * height;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            float d = distance;
            // don't let trees/houses block the view: stop just in front of what we hit
            if (Physics.SphereCast(focus, 0.2f, rotation * Vector3.back, out var hit, distance, obstacles, QueryTriggerInteraction.Ignore)
                && hit.transform != target && !hit.transform.IsChildOf(target))
                d = Mathf.Max(minDistance * 0.5f, hit.distance - 0.1f);
            Vector3 desired = focus + rotation * Vector3.back * d;

            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
        }
    }
}
