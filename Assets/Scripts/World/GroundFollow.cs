using UnityEngine;

namespace DreamWorld.World
{
    // Keeps something standing on the ground below it. Monsters and wanderers move on a flat plane; on hilly
    // ground this sets their height to the ground under them every frame, after they move.
    // Like walking, it only steps up small heights: bumping into a tree, a rock or a giant mushroom doesn't
    // lift it onto the top (it used to, and then kept climbing from there into the sky).
    public class GroundFollow : MonoBehaviour
    {
        public float probeUp = 3f;
        public float probeDown = 8f;
        public float smoothing = 14f;
        public float maxStep = 0.6f;   // highest ledge it can step up onto in one go

        static readonly RaycastHit[] hits = new RaycastHit[12];

        void LateUpdate()
        {
            var p = transform.position;
            int n = Physics.RaycastNonAlloc(p + Vector3.up * probeUp, Vector3.down, hits, probeUp + probeDown, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity, terrain = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c.transform == transform || c.transform.IsChildOf(transform) || c.attachedRigidbody != null) continue;
                if (c is CharacterController) continue;   // players and other characters aren't ground
                if (c.GetComponentInParent<DreamWorld.Gameplay.PlayerStats>() != null) continue;   // online players' hitboxes
                float y = hits[i].point.y;
                if (IsTerrain(c)) terrain = Mathf.Max(terrain, y);
                if (y <= p.y + maxStep) best = Mathf.Max(best, y);
            }
            // nothing it could step onto (it's sunk under a steep slope): back up onto the terrain itself
            if (float.IsNegativeInfinity(best)) best = terrain;
            if (float.IsNegativeInfinity(best)) return;
            p.y = Mathf.Lerp(p.y, best, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            transform.position = p;
        }

        // the hollow's ground mesh and Mistpine's ground/quarry
        static bool IsTerrain(Collider c) => c.name == "Ground" || c.name.StartsWith("Mist_Ground") || c.name.StartsWith("Mist_Quarry")
                                            || (c.transform.parent != null && (c.transform.parent.name == "Mist_Ground" || c.transform.parent.name == "Mist_Quarry"));
    }
}
