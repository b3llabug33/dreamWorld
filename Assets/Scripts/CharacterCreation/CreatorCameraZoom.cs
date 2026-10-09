using DreamWorld.Rendering;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DreamWorld.CharacterCreation
{
    // Zooms the creator camera between a full-body shot and a face close-up.
    // Scroll wheel (when not over the UI), Z/X keys, or the +/- buttons via Zoom().
    [RequireComponent(typeof(Camera))]
    public class CreatorCameraZoom : MonoBehaviour
    {
        public Vector3 farPosition = new Vector3(0, 0.84f, -3.7f);
        public float farFov = 40f;
        public Vector3 nearPosition = new Vector3(0, 1.47f, -1.05f);
        public float nearFov = 28f;
        [Tooltip("PSX render height at full zoom; a little sharper so the face reads.")]
        public int nearRenderHeight = 480;
        public float scrollStep = 0.12f;
        public float keySpeed = 1.2f;
        public float smoothing = 10f;

        [Range(0, 1)] public float zoom;
        float target;
        Camera cam;
        PSXCamera psx;
        int farRenderHeight;

        void Start()
        {
            cam = GetComponent<Camera>();
            psx = GetComponent<PSXCamera>();
            if (psx != null) farRenderHeight = psx.targetHeight;
            target = zoom;
        }

        public void Zoom(float delta) => target = Mathf.Clamp01(target + delta);

        void Update()
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUI && Input.mouseScrollDelta.y != 0) Zoom(Input.mouseScrollDelta.y * scrollStep);
            if (Input.GetKey(KeyCode.Z)) Zoom(keySpeed * Time.deltaTime);
            if (Input.GetKey(KeyCode.X)) Zoom(-keySpeed * Time.deltaTime);

            zoom = Mathf.Lerp(zoom, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            float t = Mathf.SmoothStep(0, 1, zoom);
            transform.position = Vector3.Lerp(farPosition, nearPosition, t);
            cam.fieldOfView = Mathf.Lerp(farFov, nearFov, t);
            if (psx != null) psx.targetHeight = Mathf.RoundToInt(Mathf.Lerp(farRenderHeight, nearRenderHeight, t));
        }
    }
}
