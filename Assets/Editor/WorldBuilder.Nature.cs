using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace DreamWorld.EditorTools
{
    // The two third-party nature packs (credits in Assets/ThirdParty/CREDITS.md):
    //   - Aberrant Curse "PSX Forest Pack": birches, gnarled dead trees, mossy rocks, scraggly bushes, mushrooms,
    //     crystals, an owl, a frog, a log cabin and a cave mouth (credit required)
    //   - ElegantCrow "Retro PSX Nature Pack": leafy trees, shrubs and grass clumps, with seasonal textures
    // Each model becomes a prefab in Assets/Prefabs/Nature/<Name>_<season>[_tint].prefab: upright, base at y=0,
    // centered, exactly 1 m tall (placements scale it to a height in meters), PSX Lit materials. The names match
    // ArtSource/packs.py, so Blender layouts (e.g. Mistpine's "dressing" list) place the same prefabs here.
    public static partial class WorldBuilder
    {
        const string ACDir = "Assets/ThirdParty/AberrantCurse_PSXForest";
        const string RNDir = "Assets/ThirdParty/ElegantCrow_RetroNature";
        const string RTDir = "Assets/ThirdParty/Ratoddy_RetroPSXTrees";
        const string PlaygroundDir = "Assets/ThirdParty/CursedLake_Playground";
        const string NatureMatDir = "Assets/Materials/Nature";
        const string NaturePrefabDir = "Assets/Prefabs/Nature";

        enum NatCol { None, Trunk, Box }

        // name -> (model file, default height in meters, collider)
        static Dictionary<string, (string file, float height, NatCol col)> natureModels;
        static Dictionary<string, (string file, float height, NatCol col)> NatureModels
        {
            get
            {
                if (natureModels != null) return natureModels;
                var m = new Dictionary<string, (string, float, NatCol)>
                {
                    ["AC_Birch1"] = ($"{ACDir}/Meshes/BirchTree1.fbx", 11f, NatCol.Trunk),
                    ["AC_Birch2"] = ($"{ACDir}/Meshes/BirchTree2.fbx", 10f, NatCol.Trunk),
                    ["AC_Bush1"] = ($"{ACDir}/Meshes/Bush1.fbx", 1.3f, NatCol.None),
                    ["AC_Bush2"] = ($"{ACDir}/Meshes/Bush2.fbx", 1.1f, NatCol.None),
                    ["AC_Bush3"] = ($"{ACDir}/Meshes/Bush3.fbx", 2.2f, NatCol.None),
                    ["AC_Cabin"] = ($"{ACDir}/Meshes/Cabin.fbx", 4.6f, NatCol.Box),
                    ["AC_CaveMouth"] = ($"{ACDir}/Meshes/CaveMouth.fbx", 9f, NatCol.Box),
                    ["AC_Crystal1"] = ($"{ACDir}/Meshes/Crystal1.fbx", 1.3f, NatCol.Box),
                    ["AC_Crystal2"] = ($"{ACDir}/Meshes/Crystal2.fbx", 1.2f, NatCol.Box),
                    ["AC_Frog"] = ($"{ACDir}/Meshes/Frog.fbx", 0.14f, NatCol.None),
                    ["AC_Owl"] = ($"{ACDir}/Meshes/Owl.fbx", 0.45f, NatCol.None),
                    ["AC_Lantern"] = ($"{ACDir}/Meshes/Lantern.fbx", 1f, NatCol.None),
                    ["AC_Grass1"] = ($"{ACDir}/Meshes/Grass1.fbx", 0.55f, NatCol.None),
                    ["AC_Grass2"] = ($"{ACDir}/Meshes/Grass2.fbx", 0.5f, NatCol.None),
                    ["AC_Mushroom1"] = ($"{ACDir}/Meshes/Mushroom1.fbx", 0.35f, NatCol.None),
                    ["AC_Mushroom2"] = ($"{ACDir}/Meshes/Mushroom2.fbx", 0.5f, NatCol.None),
                    ["AC_Mushroom3"] = ($"{ACDir}/Meshes/Mushroom3.fbx", 0.4f, NatCol.None),
                    ["AC_Rock1"] = ($"{ACDir}/Meshes/Rock1.fbx", 1.2f, NatCol.Box),
                    ["AC_Rock2"] = ($"{ACDir}/Meshes/Rock2.fbx", 1.8f, NatCol.Box),
                };
                for (int i = 1; i <= 5; i++) m[$"AC_DeadTree{i}"] = ($"{ACDir}/Meshes/Tree{i}.fbx", 8f, NatCol.Trunk);
                for (int i = 1; i <= 8; i++)
                {
                    m[$"RN_Tree{i}"] = ($"{RNDir}/Models/tree0{i}.fbx", i < 7 ? 7.5f : 9f, NatCol.Trunk);
                    m[$"RN_Bush{i}"] = ($"{RNDir}/Models/bush0{i}.fbx", 1.2f, NatCol.None);
                }
                for (int i = 1; i <= 9; i++) m[$"RN_Grass{i}"] = ($"{RNDir}/Models/grass0{i}.fbx", 0.5f, NatCol.None);
                m["RN_GrassBush"] = ($"{RNDir}/Models/grass_bush.fbx", 0.55f, NatCol.None);
                // RATODDY retro PSX trees: textured leafy trees (one has an autumn texture), bushes, a mushroom, a rock
                for (int i = 1; i <= 7; i++) m[$"RT_Tree{i}"] = ($"{RTDir}/Tree{i}.fbx", 9f, NatCol.Trunk);
                for (int i = 1; i <= 4; i++) m[$"RT_Bush{i}"] = ($"{RTDir}/Bush{i}.fbx", 1.3f, NatCol.None);
                m["RT_Mushroom"] = ($"{RTDir}/Mushroom.fbx", 0.6f, NatCol.None);
                m["RT_Rock"] = ($"{RTDir}/Rock.fbx", 1.0f, NatCol.Box);
                m["RN_GrassPatch"] = ($"{RNDir}/Models/grass_patch.fbx", 0.5f, NatCol.None);
                return natureModels = m;
            }
        }

        static readonly Dictionary<string, string> ACTextures = new Dictionary<string, string>
        {
            ["BirchTree"] = "BirchTree", ["Bush"] = "Bush", ["LogCabin"] = "LogCabbin", ["LogCabinProps"] = "CabinProps",
            ["Cave Entrance"] = "CaveEntrance", ["RockFace"] = "RockFace", ["Misc"] = "Misc", ["MossyRock"] = "MossyRock",
            ["Tree"] = "Tree", ["Material"] = "Dirt",
        };

        [MenuItem("dreamWorld/Setup Nature Packs")]
        public static void SetupNature()
        {
            foreach (var folder in new[] { $"{ACDir}/Textures", $"{RNDir}/Textures", RTDir, $"{PlaygroundDir}/Textures" })
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                    if (imp.filterMode == FilterMode.Point && imp.alphaIsTransparency && imp.maxTextureSize == 256) continue;
                    imp.filterMode = FilterMode.Point;       // crisp retro texels
                    imp.maxTextureSize = 256;
                    imp.mipmapEnabled = true;
                    imp.alphaIsTransparency = true;          // leaves/grass are cut out by alpha
                    imp.SaveAndReimport();
                }
            foreach (var (file, _, _) in NatureModels.Values.Distinct())
            {
                var imp = AssetImporter.GetAtPath(file) as ModelImporter;
                if (imp == null) { Debug.LogWarning("[Nature] missing " + file); continue; }
                if (!imp.importAnimation && !imp.importCameras && !imp.importLights) continue;
                imp.importAnimation = false;
                imp.animationType = ModelImporterAnimationType.None;
                imp.importCameras = false;
                imp.importLights = false;
                imp.SaveAndReimport();
            }
        }

        // ---------------------------------------------------------------- materials + prefabs

        // The texture a pack material uses (the FBX files only carry material names).
        static string NatureTexture(string modelFile, string slotName, string season)
        {
            string b = slotName.Split('.')[0];
            if (modelFile.StartsWith(RTDir))   // each model's texture sits next to it; Tree2 also has an autumn one
            {
                string baseName = modelFile.Substring(0, modelFile.Length - 4);
                return season == "fall" && File.Exists(baseName + "_fall.png") ? baseName + "_fall.png" : baseName + ".png";
            }
            if (modelFile.StartsWith(ACDir))
                return $"{ACDir}/Textures/{(ACTextures.TryGetValue(b, out var t) ? t : "Misc")}.png";
            string file = Path.GetFileNameWithoutExtension(modelFile);
            string kind = file.StartsWith("tree") ? "tree" : file.StartsWith("bush") ? "bush" : file == "grass_bush" ? "grass_bush" : "grass";
            string stem = kind == "tree" ? file : kind == "bush" ? "bush" + file.Substring(5).TrimStart('0') : kind;
            string p = $"{RNDir}/Textures/{stem}_{season}.png";
            if (!File.Exists(p)) p = $"{RNDir}/Textures/{stem}_{season}1.png";   // spring bushes come in variants
            if (!File.Exists(p)) p = $"{RNDir}/Textures/{stem}_summer.png";
            return p;
        }

        static Material NatureMaterial(string texPath, Color tint)
        {
            EnsureFolder(NatureMatDir);
            string key = Path.GetFileNameWithoutExtension(texPath) + (tint == Color.white ? "" : "_" + ColorUtility.ToHtmlStringRGB(tint));
            string path = $"{NatureMatDir}/PSX_N_{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            bool cutout = tex != null && AssetImporter.GetAtPath(texPath) is TextureImporter ti && ti.DoesSourceTextureHaveAlpha();
            mat = new Material(Shader.Find("DreamWorld/PSX Lit")) { mainTexture = tex, color = tint };
            mat.SetFloat("_AffineStrength", 0.08f);
            mat.SetFloat("_Softness", cutout ? 0.35f : 0.12f);   // foliage: softer so flipped back faces don't go black
            if (cutout)
            {
                mat.SetFloat("_Cutoff", 0.5f);
                mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static readonly Dictionary<string, GameObject> natureCache = new Dictionary<string, GameObject>();

        // The prefab for one pack model in one season and tint (made on first use).
        public static GameObject NaturePrefab(string name, string season = "summer", Color? tint = null)
        {
            var t = tint ?? Color.white;
            string key = $"{name}_{season}" + (t == Color.white ? "" : "_" + ColorUtility.ToHtmlStringRGB(t));
            if (natureCache.TryGetValue(key, out var cached) && cached != null) return cached;
            string path = $"{NaturePrefabDir}/{key}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return natureCache[key] = existing;
            if (!NatureModels.TryGetValue(name, out var def)) { Debug.LogWarning("[Nature] unknown model " + name); return null; }
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(def.file);
            if (src == null) { Debug.LogWarning("[Nature] missing " + def.file); return null; }
            EnsureFolder(NaturePrefabDir);

            var root = new GameObject(key);
            var model = (GameObject)Object.Instantiate(src, root.transform);
            model.name = "Model";
            // Aberrant's FBX files were exported Z-up but tagged Y-up: undo the import rotation on every top node
            // (the same fix that stands them up in Blender, ArtSource/packs.py).
            if (def.file.StartsWith(ACDir))
            {
                model.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                foreach (Transform c in model.transform) c.localRotation = Quaternion.identity;
            }
            bool foliage = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                var names = mats.Select(m => m != null ? m.name : "Misc").ToArray();   // the FBX's material names pick the texture
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = NatureMaterial(NatureTexture(def.file, names[i], season), t);
                    foliage |= mats[i].GetFloat("_Cutoff") > 0f;
                }
                r.sharedMaterials = mats;
                if (mats.Any(m => m.GetFloat("_Cutoff") > 0f)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // no square leaf shadows
            }
            // normalize: base on the ground, centered, 1 m tall
            var b = RendererBounds(root);
            float h = Mathf.Max(b.size.y, 1e-4f);
            var holder = new GameObject("Scaled").transform;   // scales the model to 1 m; the model sits base-down, centered, inside it
            holder.SetParent(root.transform, false);
            holder.localScale = Vector3.one / h;
            var own = model.transform.localPosition;           // some FBX roots carry their own offset
            model.transform.SetParent(holder, false);
            model.transform.localPosition = own - new Vector3(b.center.x, b.min.y, b.center.z);

            b = RendererBounds(root);
            switch (def.col)
            {
                case NatCol.Trunk:
                    var cap = root.AddComponent<CapsuleCollider>();
                    cap.center = new Vector3(0, 0.5f, 0);
                    cap.height = 1f;
                    cap.radius = 0.03f;    // about a 0.3 m trunk on a 10 m tree
                    break;
                case NatCol.Box:
                    var box = root.AddComponent<BoxCollider>();
                    box.center = b.center;
                    box.size = Vector3.Scale(b.size, new Vector3(0.85f, 1f, 0.85f));
                    break;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return natureCache[key] = prefab;
        }

        // Bounds from the actual vertices (Renderer.bounds can be stale right after re-parenting/rotating).
        static Bounds RendererBounds(GameObject go)
        {
            bool any = false;
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = mf.transform.TransformPoint(v);
                    if (!any) { b = new Bounds(w, Vector3.zero); any = true; }
                    else b.Encapsulate(w);
                }
            }
            return any ? b : new Bounds(go.transform.position, Vector3.one);
        }

        // One pack model in the scene, `height` meters tall.
        public static GameObject PutNature(Transform parent, string name, Vector3 pos, float yaw, float height = 0f,
                                           string season = "summer", Color? tint = null)
        {
            var prefab = NaturePrefab(name, season, tint);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one * (height > 0 ? height : NatureModels[name].height);
            return go;
        }

        // CursedLake's PSX playground (swings, slide, seesaw, sandbox, bench, fence) as one prefab: PSX Lit
        // materials, a box collider per piece. Placed whole in a free spot near town.
        static GameObject PlaygroundPrefab()
        {
            const string path = NaturePrefabDir + "/Playground.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlaygroundDir}/Playground.fbx");
            if (src == null) return null;
            EnsureFolder(NaturePrefabDir);
            var root = new GameObject("Playground");
            var model = (GameObject)Object.Instantiate(src, root.transform);
            model.name = "Model";
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string tex = mats[i] != null && mats[i].name.StartsWith("Fence") ? "Fence" : "Playground";
                    mats[i] = NatureMaterial($"{PlaygroundDir}/Textures/{tex}.png", NatureDusk);
                }
                r.sharedMaterials = mats;
                if (r.name != "Sandbox") r.gameObject.AddComponent<BoxCollider>();
            }
            var b = RendererBounds(root);
            model.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);   // centered, standing on the ground
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // a touch of blue so pack models match Mushroom Hollow's evening light
        static readonly Color NatureDusk = new Color(0.86f, 0.9f, 1f);

        // ---------------------------------------------------------------- Mushroom Hollow dressing

        // Leafy trees, shrubs and grass around town, a wilder mix (birches, mossy rocks, mushrooms, crystals,
        // owls, frogs) in the woods, and gnarled dead trees near the mushroom knight's clearing.
        static void NatureDressing(Transform root, Random rng)
        {
            SetupNature();
            var n = new GameObject("NaturePacks").transform;
            n.SetParent(root);
            Physics.SyncTransforms();
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            T Pick<T>(params T[] xs) => xs[rng.Next(xs.Length)];

            // a spot in a ring around the town center that's off paths/buildings and (for solid things) not inside anything
            Vector3? Spot(float minR, float maxR, float margin, float clearance)
            {
                for (int i = 0; i < 60; i++)
                {
                    float a = R(0, Mathf.PI * 2), r = R(minR, maxR);
                    var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r + 6);
                    if (!Free(p, margin) || NearHome(p, 2f)) continue;
                    if (clearance > 0 && Physics.CheckSphere(p + Vector3.up * (clearance + 0.3f), clearance, ~0, QueryTriggerInteraction.Ignore)) continue;
                    return p;
                }
                return null;
            }
            int placed = 0;
            void Scatter(int count, float minR, float maxR, float margin, float clearance, System.Func<string> model,
                         float h0, float h1, System.Func<string> season = null, Color? tint = null)
            {
                for (int i = 0; i < count; i++)
                {
                    var p = Spot(minR, maxR, margin, clearance);
                    if (p == null) continue;
                    if (PutNature(n, model(), p.Value, R(0, 360), R(h0, h1), season?.Invoke() ?? "summer", tint) != null) placed++;
                    if (clearance > 0) Physics.SyncTransforms();
                }
            }
            // a little playground in a clear spot near the square
            var pg = PlaygroundPrefab();
            if (pg != null)
            {
                for (int i = 0; i < 600; i++)
                {
                    float a = R(0, Mathf.PI * 2), r = R(16, 42);
                    var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r + 6);
                    if (!Free(p, 4f) || NearHome(p, 10f)) continue;
                    // the 14 x 11 m footprint (+ a little room) may only overlap woods trees, which make way for it
                    var cols = Physics.OverlapBox(p + Vector3.up * 2.2f, new Vector3(8f, 1.9f, 8f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    var woodsRoot = root.Find("Woods");
                    Transform Placed(Transform t) { while (t.parent != null && t.parent != woodsRoot) t = t.parent; return t.parent == woodsRoot ? t : null; }
                    if (cols.Any(c => woodsRoot == null || Placed(c.transform) == null)) continue;
                    foreach (var t in cols.Select(c => Placed(c.transform)).Distinct().ToList()) Object.DestroyImmediate(t.gameObject);
                    foreach (Transform t in woodsRoot.Cast<Transform>().ToList())   // also anything without a collider under it (flowers, mushrooms)
                        if (Mathf.Abs(t.position.x - p.x) < 8f && Mathf.Abs(t.position.z - p.z) < 8f) Object.DestroyImmediate(t.gameObject);
                    Physics.SyncTransforms();
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(pg, n);
                    // face the town center
                    go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(new Vector3(-p.x, 0, 6 - p.z)));
                    Keep(p.x - 8, p.z - 8, p.x + 8, p.z + 8);
                    Debug.Log("[Nature] playground at " + p);
                    break;
                }
            }
            string Seasonal() => rng.NextDouble() < 0.7 ? "summer" : "fall";
            var dusk = NatureDusk;

            // around town: leafy trees and round shrubs at the edges of the square and along the paths
            Scatter(10, 15, 27, 1.5f, 1.2f, () => Pick("RN_Tree1", "RN_Tree2", "RN_Tree3", "RN_Tree5"), 6f, 8f, Seasonal, dusk);
            Scatter(36, 10, 30, 0.6f, 0.5f, () => Pick("RN_Bush2", "RN_Bush3", "RN_Bush6", "RN_Bush4", "RN_Bush5"), 0.9f, 1.5f, () => Pick("summer", "spring"), dusk);
            // the woods: leafy + cone trees, birches, mossy rocks, scraggly bushes
            Scatter(46, 27, 78, 1.5f, 1.4f, () => Pick("RN_Tree1", "RN_Tree4", "RN_Tree6", "RN_Tree7", "RN_Tree8", "AC_Birch1", "AC_Birch2"), 7f, 11f, Seasonal, dusk);
            Scatter(30, 26, 76, 1f, 0.8f, () => Pick("AC_Rock1", "AC_Rock2"), 0.7f, 1.9f, null, dusk);
            // (only the full 3D shrubs: the Aberrant bushes are crossed flat cards that look like sheets turned the wrong way)
            Scatter(50, 26, 78, 0.8f, 0.5f, () => Pick("RN_Bush1", "RN_Bush2", "RN_Bush3", "RN_Bush6", "RN_Bush7", "RN_Bush8", "RT_Bush1", "RT_Bush2", "RT_Bush3", "RT_Bush4"), 0.9f, 1.8f, Seasonal, dusk);
            // little things: mushrooms (it is Mushroom Hollow), crystals, owls on rocks, frogs
            Scatter(12, 24, 76, 0.6f, 0f, () => Pick("AC_Mushroom1", "AC_Mushroom2", "RT_Mushroom"), 0.25f, 0.5f);
            Scatter(10, 30, 74, 1f, 0.6f, () => Pick("AC_Crystal1", "AC_Crystal2"), 0.8f, 1.5f, null, new Color(0.9f, 0.85f, 1f));
            Scatter(8, 24, 70, 0.6f, 0f, () => "AC_Frog", 0.12f, 0.16f);
            for (int i = 0; i < 4; i++)
            {
                var p = Spot(28, 70, 1.5f, 1f);
                if (p == null) continue;
                PutNature(n, "AC_Rock2", p.Value, R(0, 360), 1.3f, "summer", dusk);
                PutNature(n, "AC_Owl", p.Value + Vector3.up * 1.05f, R(0, 360), 0.45f);
                Physics.SyncTransforms();
            }
            // grass everywhere it isn't paved: many small clumps, no colliders
            Scatter(380, 4, 80, 0.4f, 0f, () => Pick("RN_Grass1", "RN_Grass2", "RN_Grass4", "RN_Grass5", "RN_Grass6", "RN_Grass8", "RN_GrassBush"),
                    0.35f, 0.7f, Seasonal, dusk);
            // dense grass patches so the ground isn't one flat sheet
            Scatter(170, 8, 80, 0.6f, 0f, () => "RN_GrassPatch", 0.35f, 0.55f, Seasonal, dusk);
            // spooky dead trees around the mushroom knight's clearing (south-west woods)
            for (int i = 0; i < 7; i++)
            {
                float a = R(0, Mathf.PI * 2), r = R(9, 16);
                var p = new Vector3(-31 + Mathf.Cos(a) * r, 0, -35 + Mathf.Sin(a) * r);
                if (Free(p, 1f) && !Physics.CheckSphere(p + Vector3.up * 1.5f, 1f, ~0, QueryTriggerInteraction.Ignore))
                    PutNature(n, $"AC_DeadTree{rng.Next(1, 6)}", p, R(0, 360), R(6, 9), "summer", new Color(0.8f, 0.78f, 0.9f));
            }
            Debug.Log($"[Nature] placed {placed} pack models in Mushroom Hollow");
        }
    }
}
