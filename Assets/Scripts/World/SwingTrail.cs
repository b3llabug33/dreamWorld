using System.Collections.Generic;
using UnityEngine;

namespace DreamWorld.World
{
    // The ribbon behind a weapon swing: WeaponPose feeds it the blade (base + tip) every frame,
    // and it draws a strip through the recent samples that fades out quickly. Lives in world space.
    public class SwingTrail : MonoBehaviour
    {
        public Color color = new Color(1f, 0.62f, 0.9f, 0.85f);
        public float fadeSeconds = 0.2f;

        struct Slice { public Vector3 a, b; public float time; public Color color; public bool end; }
        readonly List<Slice> samples = new List<Slice>();
        Mesh mesh;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();
        bool emittedLast;

        static Material material;

        public static SwingTrail Create()
        {
            var go = new GameObject("SwingTrail", typeof(MeshFilter), typeof(MeshRenderer));
            var trail = go.AddComponent<SwingTrail>();
            trail.mesh = new Mesh { name = "SwingTrail" };
            trail.mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = trail.mesh;
            if (material == null) material = new Material(Shader.Find("Sprites/Default"));   // vertex colors + alpha, no culling
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return trail;
        }

        // One frame of the blade. emit = the strike is moving (otherwise the old samples just fade).
        public void Sample(Vector3 bladeBase, Vector3 tip, bool emit)
        {
            if (emit)
            {
                // fill fast arcs in with a midpoint so low frame rates still look curved
                if (emittedLast && samples.Count > 0)
                {
                    var last = samples[samples.Count - 1];
                    if ((last.b - tip).magnitude > 0.25f)
                        samples.Add(new Slice { a = (last.a + bladeBase) * 0.5f, b = Vector3.Lerp(last.b, tip, 0.5f) + ((last.b + tip) * 0.5f - (last.a + bladeBase) * 0.5f).normalized * 0.04f,
                                                 time = Time.time - Time.deltaTime * 0.5f, color = color });
                }
                samples.Add(new Slice { a = bladeBase, b = tip, time = Time.time, color = color });
            }
            else if (emittedLast && samples.Count > 0)
            {
                // a break between swings: start the next ribbon separately
                var last = samples[samples.Count - 1];
                last.end = true;
                samples[samples.Count - 1] = last;
            }
            emittedLast = emit;
        }

        void LateUpdate()
        {
            samples.RemoveAll(s => Time.time - s.time > fadeSeconds);
            verts.Clear(); colors.Clear(); tris.Clear();
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                float k = 1f - (Time.time - s.time) / fadeSeconds;
                var c = s.color;
                float alpha = c.a * k * k;
                verts.Add(s.a);
                verts.Add(s.b);
                colors.Add(new Color(c.r, c.g, c.b, alpha * 0.15f));             // fades toward the hilt
                colors.Add(new Color(Mathf.Min(1f, c.r + 0.25f), Mathf.Min(1f, c.g + 0.25f), Mathf.Min(1f, c.b + 0.25f), alpha));
                bool joined = i > 0 && !samples[i - 1].end;
                if (joined)
                {
                    int v = i * 2;
                    tris.Add(v - 2); tris.Add(v - 1); tris.Add(v);
                    tris.Add(v); tris.Add(v - 1); tris.Add(v + 1);
                }
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }
    }
}
