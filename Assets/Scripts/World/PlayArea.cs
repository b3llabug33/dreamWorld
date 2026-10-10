using System.Collections.Generic;
using UnityEngine;

namespace DreamWorld.World
{
    // Extra walkable ground outside the main circle around town (PlayerController.areaRadius): the trail out
    // to Mistpine Hollow and the hollow itself. A flat box on the ground: this object's position and scale (x, z).
    public class PlayArea : MonoBehaviour
    {
        static readonly List<PlayArea> areas = new List<PlayArea>();

        void OnEnable() { if (!areas.Contains(this)) areas.Add(this); }
        void OnDisable() => areas.Remove(this);

        public bool Contains(Vector3 p)
        {
            var c = transform.position;
            var s = transform.lossyScale;
            return Mathf.Abs(p.x - c.x) <= s.x * 0.5f && Mathf.Abs(p.z - c.z) <= s.z * 0.5f;
        }

        public static bool AnyContains(Vector3 p)
        {
            foreach (var a in areas)
                if (a != null && a.Contains(p)) return true;
            return false;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 0.7f, 1f, 0.15f);
            Gizmos.DrawCube(transform.position, new Vector3(transform.lossyScale.x, 0.2f, transform.lossyScale.z));
        }
    }
}
