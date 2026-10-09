using UnityEngine;

namespace DreamWorld.Rendering
{
    // Spins an object around one of its own axes, through a pivot point in its local space.
    // Used for PC fans, whose meshes aren't always centered on their pivot.
    public class Spin : MonoBehaviour
    {
        public Vector3 localAxis = Vector3.up;
        public Vector3 localPivot;
        public float degreesPerSecond = 240f;

        void Update()
        {
            var pivot = transform.TransformPoint(localPivot);
            transform.RotateAround(pivot, transform.TransformDirection(localAxis), degreesPerSecond * Time.deltaTime);
        }
    }
}
