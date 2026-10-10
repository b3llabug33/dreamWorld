using UnityEngine;

namespace DreamWorld.World
{
    // Keeps something standing on the ground below it. Monsters and wanderers move on a flat plane; on hilly
    // ground (Mistpine Hollow) this sets their height to the terrain under them every frame, after they move.
    public class GroundFollow : MonoBehaviour
    {
        public float probeUp = 3f;
        public float probeDown = 8f;
        public float smoothing = 14f;

        static readonly RaycastHit[] hits = new RaycastHit[8];

        void LateUpdate()
        {
            var p = transform.position;
            int n = Physics.RaycastNonAlloc(p + Vector3.up * probeUp, Vector3.down, hits, probeUp + probeDown, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c.transform == transform || c.transform.IsChildOf(transform) || c.attachedRigidbody != null) continue;
                if (c is CharacterController) continue;   // players and other characters aren't ground
                best = Mathf.Max(best, hits[i].point.y);
            }
            if (float.IsNegativeInfinity(best)) return;
            p.y = Mathf.Lerp(p.y, best, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            transform.position = p;
        }
    }
}
