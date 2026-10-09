using UnityEngine;

namespace DreamWorld.Gameplay
{
    public enum ZoneKind { Safe, Home }

    // A box area of the world. Safe zones (the town square, the paths) block all damage:
    // no PvP and monsters won't follow you in. Everywhere else, including homes, is dangerous.
    // The box is this object's transform: position, rotation and scale (a unit cube scaled up).
    public class Zone : MonoBehaviour
    {
        public ZoneKind kind = ZoneKind.Safe;
        public string displayName = "town square";

        void OnEnable() => ZoneMap.Register(this);
        void OnDisable() => ZoneMap.Unregister(this);

        public bool Contains(Vector3 worldPoint)
        {
            var p = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(p.x) <= 0.5f && Mathf.Abs(p.z) <= 0.5f && p.y >= -0.5f && p.y <= 0.5f;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind == ZoneKind.Safe ? new Color(0.4f, 1f, 0.6f, 0.25f) : new Color(1f, 0.6f, 0.8f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
        }
    }
}
