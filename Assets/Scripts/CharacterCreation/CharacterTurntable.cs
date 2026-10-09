using UnityEngine;
using UnityEngine.EventSystems;

namespace DreamWorld.CharacterCreation
{
    // Spins the character around its vertical axis: click-drag anywhere that isn't UI,
    // or call Turn() from the rotate buttons. Rotation eases toward the target angle.
    public class CharacterTurntable : MonoBehaviour
    {
        public float dragDegreesPerPixel = 0.4f;
        public float buttonStepDegrees = 45f;
        public float smoothing = 12f;

        float targetYaw;
        bool dragging;
        float lastMouseX;

        void Start() => targetYaw = transform.eulerAngles.y;

        public void Turn(int direction) => targetYaw += direction * buttonStepDegrees;

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                // Don't start a spin when the click lands on a button.
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                dragging = !overUI;
                lastMouseX = Input.mousePosition.x;
            }
            if (Input.GetMouseButtonUp(0)) dragging = false;

            if (dragging)
            {
                // Dragging right turns the character's front to the right (negative yaw).
                targetYaw -= (Input.mousePosition.x - lastMouseX) * dragDegreesPerPixel;
                lastMouseX = Input.mousePosition.x;
            }

            float yaw = Mathf.LerpAngle(transform.eulerAngles.y, targetYaw, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
