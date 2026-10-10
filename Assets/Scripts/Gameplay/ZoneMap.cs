using System.Collections.Generic;
using UnityEngine;

namespace DreamWorld.Gameplay
{
    public static class ZoneMap
    {
        static readonly List<Zone> zones = new List<Zone>();

        public static void Register(Zone z) { if (!zones.Contains(z)) zones.Add(z); }
        public static void Unregister(Zone z) => zones.Remove(z);

        // The zone you're in. Zones can sit inside each other (your home inside the plots), so the smallest wins.
        public static Zone At(Vector3 point)
        {
            Zone best = null;
            float bestSize = float.MaxValue;
            foreach (var z in zones)
            {
                if (!z.Contains(point)) continue;
                var s = z.transform.lossyScale;
                float size = Mathf.Abs(s.x * s.z);
                if (size < bestSize) { best = z; bestSize = size; }
            }
            return best;
        }

        public static bool IsSafe(Vector3 point)
        {
            var z = At(point);
            return z != null && z.kind == ZoneKind.Safe;
        }
    }
}
