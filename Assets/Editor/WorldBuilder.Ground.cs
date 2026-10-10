using System.Collections.Generic;
using System.Linq;
using DreamWorld.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace DreamWorld.EditorTools
{
    // The ground of Mushroom Hollow, and the bigger town square:
    //   - BuildPlaza: a wide tiled plaza with a cobblestone border, a fountain, planters, benches and a ring of lamps
    //   - TownExtraBuildings: the beetle shell house and the tide pool aquarium shop on the square
    //   - BuildGround (after everything is placed): a terrain mesh that stays flat in town, on paths and under
    //     buildings but rolls into hills out in the woods (rising toward the edge of the world), with a pond and a
    //     stream (and the mossy arch bridge), everything in the woods set onto the new ground, and real grass
    //     tufts covering it.
    public static partial class WorldBuilder
    {
        const float PlazaRadius = 17.5f;
        const float WaterLevel = -0.35f;
        const string GroundMatDir = "Assets/Materials/Ground";

        // ---------------------------------------------------------------- the town square

        static void BuildPlaza(Transform town)
        {
            var plaza = new GameObject("Plaza").transform;
            plaza.SetParent(town);
            // the paving: rosette tiles, a cobblestone border ring and a cobble ring around the fountain
            Disc(plaza, "Paving", Plaza, 0f, PlazaRadius, 0.035f, GroundMaterial("Tiles_Pattern_1_Grey_1", 2.4f), collider: true);
            Disc(plaza, "Border", Plaza, PlazaRadius, PlazaRadius + 1.3f, 0.045f, GroundMaterial("Cobblestones_01_Grey_1", 1.4f), collider: true);
            Disc(plaza, "FountainRing", Plaza, 3.2f, 4.6f, 0.05f, GroundMaterial("Cobblestones_01_Orange_1", 1.4f), collider: false);
            Put(plaza, "StoneFountain", Plaza, 0, 2.3f, "box");
            AddLight(plaza, Plaza + Vector3.up * 1.5f, new Color(0.6f, 0.85f, 1f), 7f, 1.2f);

            // benches facing the fountain, between the cross paths
            foreach (var a in new[] { 25f, 65f, 115f, 155f, 205f, 245f, 295f, 335f })
            {
                var p = Plaza + Quaternion.Euler(0, a, 0) * Vector3.forward * 7.8f;
                Place(plaza, "Bench", p, a + 180, 1f, collider: false);
            }
            // four planters on the diagonals: a stone box with shrubs and flowers
            foreach (var a in new[] { 45f, 135f, 225f, 315f })
            {
                var p = Plaza + Quaternion.Euler(0, a, 0) * Vector3.forward * 12.5f;
                var box = Block(plaza, "Planter", p + Vector3.up * 0.28f, new Vector3(3.2f, 0.56f, 3.2f), "Paving");
                box.transform.rotation = Quaternion.Euler(0, a, 0);
                box.GetComponent<Renderer>().sharedMaterial = GroundMaterial("Cobblestones_01_White_1", 1.2f);
                PutNature(plaza, "RN_Bush3", p + Vector3.up * 0.55f, a, 1.3f, "summer", NatureDusk);
                for (int k = 0; k < 4; k++)
                    Place(plaza, "Flowers", p + Quaternion.Euler(0, a + 45 + k * 90, 0) * Vector3.forward * 1.15f + Vector3.up * 0.55f, k * 90, 0.7f, collider: false);
            }
            // where Orion hangs out: on the south side, where you walk in from the respawn point
            var orionSpot = new GameObject("OrionSpot").transform;
            orionSpot.SetParent(plaza);
            orionSpot.position = Plaza + new Vector3(2.2f, 0.03f, -11f);
            // a ring of lamps on the border
            for (int i = 0; i < 12; i++)
            {
                float a = 15f + i * 30f;
                var p = Plaza + Quaternion.Euler(0, a, 0) * Vector3.forward * (PlazaRadius + 0.65f);
                var lamp = Place(plaza, "Lamp_Post", p, a, 1f, collider: false);
                AddLight(lamp.transform, new Vector3(0, 2.3f, 0), Warm, 8f, 1.2f);
            }
        }

        // The beetle shell house (Orion's colors) and the tide pool aquarium shop, where two mushroom houses were.
        static void TownExtraBuildings(Transform town)
        {
            var group = new GameObject("SquareBuildings").transform;
            group.SetParent(town);
            NewBuilding(group, "BeetleShellHouse", Plaza + Quaternion.Euler(0, -105, 0) * Vector3.forward * 33f, Plaza, 3.2f, 2.6f, new Color(0.7f, 1f, 0.8f),
                        "the beetle shell house", "a house shaped like Orion. she says it's flattering. she's right");
            NewBuilding(group, "AquariumShop", Spread(10, 30), Plaza, 2.6f, 2.4f, new Color(0.55f, 0.85f, 1f),
                        "tide pool aquarium", "the windows are fish tanks. the fish are judging your outfit");
        }

        // A flat ring (or disc when inner = 0) of quads, UVs in world meters so textures tile evenly.
        static GameObject Disc(Transform parent, string name, Vector3 center, float inner, float outer, float y, Material mat, bool collider)
        {
            const int seg = 64;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2 / seg;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                foreach (var r in new[] { inner, outer })
                {
                    var v = dir * r + Vector3.up * y;
                    verts.Add(v);
                    uvs.Add(new Vector2(center.x + v.x, center.z + v.z));
                }
                if (i < seg)
                {
                    int b = i * 2;
                    tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent);
            go.transform.position = center;
            go.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, name);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            return go;
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            var path = $"Assets/Models/Generated/{name}.asset";
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null)
            {
                EditorUtility.CopySerialized(mesh, old);
                return old;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // A tiling PSX material for a FlakDeau texture: `meters` = how big one tile is in the world (meshes here use
        // world-meter UVs).
        static Material GroundMaterial(string texture, float meters, bool vertexColor = false, string suffix = "")
        {
            EnsureFolder(GroundMatDir);
            var path = $"{GroundMatDir}/PSX_G_{texture}{suffix}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("DreamWorld/PSX Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            var texPath = $"{PixelTexDir}/{texture}.png";
            var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (imp != null && (imp.filterMode != FilterMode.Point || imp.wrapMode != TextureWrapMode.Repeat))
            {
                imp.filterMode = FilterMode.Point;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.mipmapEnabled = true;
                imp.SaveAndReimport();
            }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            mat.mainTextureScale = Vector2.one / meters;
            mat.SetFloat("_AffineStrength", 0f);
            mat.SetFloat("_VertexColor", vertexColor ? 1f : 0f);
            mat.color = new Color(0.86f, 0.88f, 1f);   // the hollow's evening tint
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- the ground

        // the pond and the stream out of it (picked in BuildGround)
        static Vector3 pondCenter;
        static float pondRadius;
        static readonly List<Vector3> streamPts = new List<Vector3>();
        static List<Rect> groundFlat;

        static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        static float DistToRect(Rect r, float x, float z)
        {
            float dx = Mathf.Max(r.xMin - x, 0, x - r.xMax), dz = Mathf.Max(r.yMin - z, 0, z - r.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float DistToPolyline(List<Vector3> pts, float x, float z, out float t)
        {
            float best = 1e9f;
            t = 0;
            var p = new Vector2(x, z);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var a = new Vector2(pts[i].x, pts[i].z);
                var b = new Vector2(pts[i + 1].x, pts[i + 1].z);
                var ab = b - a;
                float u = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                float d = (a + ab * u - p).magnitude;
                if (d < best) { best = d; t = (i + u) / (pts.Count - 1); }
            }
            return best;
        }

        // Ground height: 0 in town / on paths / under buildings (with a soft rise away from them), rolling hills in
        // the woods, higher toward the edge of the world, the pond and stream carved in, flat at the Mistpine seam.
        static float GroundHeight(float x, float z)
        {
            float flat = float.MaxValue;
            foreach (var r in groundFlat) flat = Mathf.Min(flat, DistToRect(r, x, z));
            float m = Smooth01((flat - 2f) / 14f);
            float hills = (Mathf.PerlinNoise(x * 0.028f + 11.3f, z * 0.028f + 4.1f) - 0.42f) * 5.0f
                        + (Mathf.PerlinNoise(x * 0.085f + 2.7f, z * 0.085f + 9.9f) - 0.5f) * 1.2f;
            float r0 = new Vector2(x, z - 6).magnitude;
            float rim = Smooth01((r0 - 74f) / 34f) * 7f;                        // the hollow's bowl rising at the edge
            // where it's flat (town, paths, under buildings) the ground sits 8 cm low, so building floors, ground
            // plates and paving (all at ~0) don't share its height and flicker through it
            float h = m * (Mathf.Max(hills, -0.4f) + rim) - 0.08f * (1f - m);
            h *= Smooth01((x + 80f) / 12f);                                      // meet Mistpine's flat edge at x = -84
            // the pond: a bowl down to -1.3 m
            float dp = new Vector2(x - pondCenter.x, z - pondCenter.z).magnitude;
            if (dp < pondRadius + 4f)
                h = Mathf.Lerp(h, -1.3f, Smooth01((pondRadius + 4f - dp) / 5f));
            // the stream: a channel 0.9 m deep
            if (streamPts.Count > 1)
            {
                float ds = DistToPolyline(streamPts, x, z, out _);
                if (ds < 4.5f) h = Mathf.Lerp(h, Mathf.Min(h, -0.95f), Smooth01((4.5f - ds) / 3f));
            }
            return h;
        }

        static void BuildGround(Transform root, Random rng)
        {
            Physics.SyncTransforms();
            // where it stays flat: the town keep-outs (square, buildings, trails, plots...) but not the giant woods-wide ones
            groundFlat = KeepOut.Where(r => r.width < 90 && r.height < 90).ToList();

            PickWater(root, rng);

            // ---- the terrain mesh, replacing the flat plane (x -84..120 like the plane; Mistpine starts at -84)
            var oldGround = root.Find("Ground");
            const float step = 2f, x0 = -84f, x1 = 120f, z0 = -114f, z1 = 126f;
            int nx = Mathf.RoundToInt((x1 - x0) / step), nz = Mathf.RoundToInt((z1 - z0) / step);
            var verts = new Vector3[(nx + 1) * (nz + 1)];
            var cols = new Color[verts.Length];
            var uvs = new Vector2[verts.Length];
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = x0 + i * step, z = z0 + j * step;
                    float h = GroundHeight(x, z);
                    int k = j * (nx + 1) + i;
                    verts[k] = new Vector3(x, h, z);
                    uvs[k] = new Vector2(x, z);
                    // color variation: patchy greens, darker in the dips, a little drier on the hills
                    float n = Mathf.PerlinNoise(x * 0.06f + 30f, z * 0.06f + 70f);
                    var c = Color.Lerp(new Color(0.78f, 0.9f, 0.78f), new Color(1.05f, 1.05f, 0.9f), n);
                    c *= Mathf.Lerp(0.85f, 1.05f, Smooth01((h + 1f) / 5f));
                    if (h < -0.5f) c = Color.Lerp(c, new Color(0.62f, 0.55f, 0.45f), Smooth01((-0.5f - h) / 0.6f));   // muddy banks
                    c.a = 1f;
                    cols[k] = c;
                }
            var tris = new List<int>(nx * nz * 6);
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            var mesh = new Mesh { name = "HollowGround", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.uv = uvs;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var ground = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer));
            ground.transform.SetParent(root);
            ground.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, "HollowGround");
            ground.GetComponent<MeshRenderer>().sharedMaterial = GroundMaterial("Grass_02_Green_1", 3f, vertexColor: true);
            ground.AddComponent<MeshCollider>().sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;
            if (oldGround != null) Object.DestroyImmediate(oldGround.gameObject);
            Physics.SyncTransforms();

            // ---- set everything out in the woods onto the new ground (keeping how high above it they were)
            foreach (var name in new[] { "Woods", "WoodsProps", "NaturePacks", "Critters", "Animals", "Shroomlings", "Hill" })
                foreach (Transform c in root.Cast<Transform>().Where(t => t.name == name).ToList())
                    foreach (Transform t in c.Cast<Transform>().ToList())
                        t.position += Vector3.up * GroundHeight(t.position.x, t.position.z);
            foreach (Transform spawner in root.Cast<Transform>().Where(t => t.name.StartsWith("Spawner_")).ToList())
                foreach (Transform spot in spawner)
                    spot.position = new Vector3(spot.position.x, GroundHeight(spot.position.x, spot.position.z), spot.position.z);
            foreach (Transform t in root.Cast<Transform>().Where(t => t.name.StartsWith("Sign_") || t.name == "Signpost").ToList())
                t.position += Vector3.up * GroundHeight(t.position.x, t.position.z);
            var animals = root.Find("Animals");
            if (animals != null)
                foreach (Transform a in animals)
                    if (a.GetComponent<GroundFollow>() == null) a.gameObject.AddComponent<GroundFollow>();
            // anything left standing in the water goes
            foreach (var name in new[] { "Woods", "WoodsProps", "NaturePacks" })
            {
                var c = root.Find(name);
                if (c == null) continue;
                for (int i = c.childCount - 1; i >= 0; i--)
                {
                    var t = c.GetChild(i);
                    if (InWater(t.position.x, t.position.z, 1.2f)) Object.DestroyImmediate(t.gameObject);
                }
            }

            BuildWater(root);
            Physics.SyncTransforms();
            BuildGrass(root, rng);
        }

        static bool InWater(float x, float z, float margin)
        {
            if (new Vector2(x - pondCenter.x, z - pondCenter.z).magnitude < pondRadius + margin) return true;
            return streamPts.Count > 1 && DistToPolyline(streamPts, x, z, out _) < 1.6f + margin;
        }

        // The pond goes in the clearest of a few open spots in the woods; the stream winds from it to the edge.
        static void PickWater(Transform root, Random rng)
        {
            var spots = new[] { new Vector3(27, 0, -40), new Vector3(-30, 0, 50), new Vector3(42, 0, 60), new Vector3(-62, 0, -32) };
            Vector3 best = spots[0];
            int bestCount = int.MaxValue;
            foreach (var s in spots)
            {
                int n = Physics.OverlapSphere(s + Vector3.up, 11f, ~0, QueryTriggerInteraction.Ignore).Length;
                if (groundFlat.Any(r => DistToRect(r, s.x, s.z) < 12f)) n += 1000;
                if (n < bestCount) { bestCount = n; best = s; }
            }
            pondCenter = best;
            pondRadius = 8.5f;
            streamPts.Clear();
            // out from the pond, away from town, wiggling, to just past the bowl's rim
            var away = new Vector3(best.x, 0, best.z - 6).normalized;
            var side = Vector3.Cross(Vector3.up, away);
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f;
                var p = best + away * (pondRadius - 1f + t * 34f) + side * Mathf.Sin(t * 6f) * 4f;
                streamPts.Add(new Vector3(p.x, 0, p.z));
            }
            // clear the woods (and anything else scattered) off the water before the ground is made
            foreach (var name in new[] { "Woods", "WoodsProps", "NaturePacks", "Critters" })
            {
                var c = root.Find(name);
                if (c == null) continue;
                for (int i = c.childCount - 1; i >= 0; i--)
                {
                    var t = c.GetChild(i);
                    if (InWater(t.position.x, t.position.z, 2.5f)) Object.DestroyImmediate(t.gameObject);
                }
            }
            Debug.Log($"[Ground] pond at {pondCenter}");
        }

        static void BuildWater(Transform root)
        {
            var water = new GameObject("Water").transform;
            water.SetParent(root);
            var mat = GroundMaterial("Water_01_Blue_2", 4f, suffix: "_water");
            mat.SetFloat("_Softness", 0.3f);
            mat.color = new Color(0.42f, 0.55f, 0.72f);   // deep and calm at night, not swimming-pool blue
            // the pond surface
            var pond = Disc(water, "Pond", pondCenter, 0f, pondRadius + 1.5f, WaterLevel, mat, collider: false);
            pond.AddComponent<WaterScroll>();
            // the stream: a ribbon along the channel
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i < streamPts.Count; i++)
            {
                var a = streamPts[Mathf.Max(0, i - 1)];
                var b = streamPts[Mathf.Min(streamPts.Count - 1, i + 1)];
                var side = Vector3.Cross(Vector3.up, (b - a).normalized) * 2.1f;
                foreach (var v in new[] { streamPts[i] - side, streamPts[i] + side })
                {
                    verts.Add(new Vector3(v.x, WaterLevel - 0.15f, v.z));
                    uvs.Add(new Vector2(v.x, v.z));
                }
                if (i < streamPts.Count - 1)
                {
                    int k = i * 2;
                    tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
                }
            }
            var mesh = new Mesh { name = "Stream" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            var stream = new GameObject("Stream", typeof(MeshFilter), typeof(MeshRenderer));
            stream.transform.SetParent(water);
            stream.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, "Stream");
            stream.GetComponent<MeshRenderer>().sharedMaterial = mat;
            stream.AddComponent<WaterScroll>();

            // the mossy arch bridge over the stream, a little way out from the pond, plus reeds and rocks on the banks
            int bi = streamPts.Count / 3;
            var bp = streamPts[bi];
            var dir = (streamPts[bi + 1] - streamPts[bi - 1]).normalized;
            var across = Vector3.Cross(Vector3.up, dir);
            float bankY = Mathf.Max(GroundHeight(bp.x + across.x * 3.5f, bp.z + across.z * 3.5f), GroundHeight(bp.x - across.x * 3.5f, bp.z - across.z * 3.5f));
            var bridge = Put(water, "Buildings", "MossyArchBridge", new Vector3(bp.x, bankY - 0.15f, bp.z), Quaternion.LookRotation(across).eulerAngles.y, 1f, "mesh", world: true);
            bridge.name = "MossyArchBridge";
            var rnd = new Random(5);
            for (int i = 0; i < 26; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2;
                var p = pondCenter + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (pondRadius + 0.6f + (float)rnd.NextDouble() * 1.4f);
                p.y = GroundHeight(p.x, p.z);
                if (i % 3 == 0) PutNature(water, rnd.NextDouble() < 0.5 ? "AC_Rock1" : "AC_Rock2", p, (float)rnd.NextDouble() * 360, 0.5f + (float)rnd.NextDouble() * 0.6f, "summer", NatureDusk);
                else PutNature(water, "RN_GrassBush", p, (float)rnd.NextDouble() * 360, 0.7f + (float)rnd.NextDouble() * 0.4f, "summer", NatureDusk);
            }
            AddLight(water, pondCenter + Vector3.up * 2f, new Color(0.5f, 0.75f, 1f), 12f, 0.8f);
            Keep(pondCenter.x - pondRadius - 2, pondCenter.z - pondRadius - 2, pondCenter.x + pondRadius + 2, pondCenter.z + pondRadius + 2);
        }

        // ---------------------------------------------------------------- grass

        // Real grass: thousands of small crossed-card tufts (the Retro Nature grass texture) baked into one mesh per
        // 24 m chunk, everywhere on open ground: not on the plaza, paths, water, under buildings or props.
        static void BuildGrass(Transform root, Random rng)
        {
            var group = new GameObject("Grass").transform;
            group.SetParent(root);
            var mat = NatureMaterial($"{RNDir}/Textures/grass_summer.png", new Color(1f, 1.08f, 0.95f));
            var groundCol = root.Find("Ground").GetComponent<Collider>();
            const float chunk = 24f, x0 = -84f, x1 = 120f, z0 = -114f, z1 = 126f;
            int total = 0;
            for (float cx = x0; cx < x1; cx += chunk)
                for (float cz = z0; cz < z1; cz += chunk)
                {
                    var verts = new List<Vector3>();
                    var uvs = new List<Vector2>();
                    var cols = new List<Color>();
                    var tris = new List<int>();
                    int n = (int)(chunk * chunk / 1.1f);   // about one tuft per 1.1 square meters
                    for (int i = 0; i < n; i++)
                    {
                        float x = cx + (float)rng.NextDouble() * chunk, z = cz + (float)rng.NextDouble() * chunk;
                        if (!GrassOk(x, z, groundCol, out float y)) continue;
                        float h = 0.34f + (float)rng.NextDouble() * 0.4f, w = h * 1.15f;
                        float yaw = (float)rng.NextDouble() * 180f;
                        var shade = Color.Lerp(new Color(0.8f, 0.92f, 0.8f), new Color(1.05f, 1.05f, 0.88f), (float)rng.NextDouble());
                        for (int q = 0; q < 2; q++)   // two crossed cards
                        {
                            var r = Quaternion.Euler(0, yaw + q * 90, 0) * Vector3.right * (w / 2);
                            int b = verts.Count;
                            var basePos = new Vector3(x, y - 0.03f, z);
                            verts.AddRange(new[] { basePos - r, basePos + r, basePos + r + Vector3.up * h, basePos - r + Vector3.up * h });
                            uvs.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                            cols.AddRange(new[] { shade, shade, shade, shade });
                            tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
                        }
                        total++;
                    }
                    if (verts.Count == 0) continue;
                    var mesh = new Mesh { name = $"Grass_{cx}_{cz}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.SetVertices(verts);
                    mesh.SetUVs(0, uvs);
                    mesh.SetColors(cols);
                    mesh.SetTriangles(tris, 0);
                    mesh.RecalculateNormals();
                    mesh.normals = Enumerable.Repeat(Vector3.up, verts.Count).ToArray();   // lit like the ground, not like walls
                    mesh.RecalculateBounds();
                    var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(group);
                    go.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, "Grass/" + mesh.name);
                    var r2 = go.GetComponent<MeshRenderer>();
                    r2.sharedMaterial = mat;
                    r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            Debug.Log($"[Ground] {total} grass tufts");
        }

        // Open ground: the first thing straight down is the ground itself, and it's not a path or water.
        static bool GrassOk(float x, float z, Collider ground, out float y)
        {
            y = 0;
            if (new Vector2(x - Plaza.x, z - Plaza.z).magnitude < PlazaRadius + 1.5f) return false;
            if (InWater(x, z, 0.4f)) return false;
            foreach (var (a, b, w) in PathSegs)
            {
                var p = new Vector2(x, z);
                var a2 = new Vector2(a.x, a.z);
                var ab = new Vector2(b.x, b.z) - a2;
                float u = Mathf.Clamp01(Vector2.Dot(p - a2, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                if ((a2 + ab * u - p).magnitude < w / 2 + 0.25f) return false;
            }
            if (!Physics.Raycast(new Vector3(x, 30f, z), Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (hit.collider != ground) return false;
            y = hit.point.y;
            return true;
        }
    }
}
