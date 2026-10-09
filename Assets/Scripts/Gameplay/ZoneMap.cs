using System.Collections.Generic;
using UnityEngine;

namespace DreamWorld.Gameplay
{
    public static class ZoneMap
    {
        static readonly List<Zone> zones = new List<Zone>();

        public static void Register(Zone z) { if (!zones.Contains(z)) zones.Add(z); }
        public static void Unregister(Zone z) => zones.Remove(z);

        public static Zone At(Vector3 point)
        {
            foreach (var z in zones)
                if (z.Contains(point)) return z;
            return null;
        }

        public static bool IsSafe(Vector3 point)
        {
            var z = At(point);
            return z != null && z.kind == ZoneKind.Safe;
        }
    }
}
