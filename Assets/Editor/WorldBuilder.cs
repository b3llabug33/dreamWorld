using System;
using System.Collections.Generic;
using System.IO;
using DreamWorld.CharacterCreation;
using DreamWorld.Rendering;
using DreamWorld.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = System.Random;

namespace DreamWorld.EditorTools
{
    // dreamWorld > Build World: Mushroom Hollow  - (re)creates the first world area scene.
    // dreamWorld > Build Creator Backdrop         - dresses the character creator with the same night look.
    // Props come from Assets/Models/World/WorldKit.fbx (made by ArtSource/build_world.py).
    public static class WorldBuilder
    {
        const string KitPath = "Assets/Models/World/WorldKit.fbx";
        const string KitDir = "Assets/Models/World";
        const string MatDir = "Assets/Materials/World";
        const string WorldScene = "Assets/Scenes/World_MushroomHollow.unity";
        const string CreatorScene = "Assets/Scenes/CharacterCreation.unity";
        const string PlayerPrefab = "Assets/Prefabs/Dreamer.prefab";

        [Serializable] class Sub { public string texture; public bool unlit; }
        [Serializable] class Prop { public string name; public string collider; public Sub[] submeshes; }
        [Serializable] class Kit { public Prop[] props; }

        static Dictionary<string, Prop> kit;
        static GameObject kitModel;

        static readonly Color Warm = new Color(1f, 0.74f, 0.42f);
        static readonly Dictionary<string, Color> MushroomGlow = new Dictionary<string, Color>
        {
            { "Mushroom_Purple", new Color(0.70f, 0.42f, 1f) },
            { "Mushroom_Blue", new Color(0.40f, 0.65f, 1f) },
            { "Mushroom_Pink", new Color(1f, 0.50f, 0.78f) },
        };

        // ---------------------------------------------------------------- world area

        [MenuItem("dreamWorld/Build World: Mushroom Hollow")]
        public static void BuildWorld()
        {
            LoadKit();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            NightAtmosphere(fogStart: 14, fogEnd: 62);
            var root = new GameObject("MushroomHollow").transform;
            var rng = new Random(42);

            // ground + paths
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root);
            ground.transform.localScale = new Vector3(14, 1, 14);
            ground.GetComponent<Renderer>().sharedMaterial = TiledMaterial("Grass", 70);
            PathStrip(root, new Vector3(0, 0, -16), new Vector3(0, 0, 2), 2.4f);
            PathStrip(root, new Vector3(0, 0, 6), new Vector3(0, 0, 15), 2.4f);
            PathStrip(root, new Vector3(-2.5f, 0, 4), new Vector3(-6.5f, 0, 5.5f), 1.8f);
            PathStrip(root, new Vector3(2.5f, 0, 4.5f), new Vector3(6.5f, 0, 8f), 1.8f);
            Place(root, "Stage", new Vector3(0, -0.06f, 4), 0, 2.6f);   // round paved plaza

            // the village
            Place(root, "Well", new Vector3(0, 0, 4), 20);
            Place(root, "Bench", new Vector3(3.2f, 0, 3), -110);
            Place(root, "Bench", new Vector3(-3.2f, 0, 3), 110);
            Place(root, "Cottage", new Vector3(-9, 0, 6.5f), 100);
            Place(root, "Cottage", new Vector3(9, 0, 10), -115);
            Place(root, "Mushroom_House", new Vector3(-6, 0, 15), 155);
            Place(root, "Mushroom_House", new Vector3(6.5f, 0, -1.5f), -70);
            Place(root, "Mushroom_House", new Vector3(0.5f, 0, 19), 180, 1.2f);
            Place(root, "Signpost", new Vector3(2, 0, -9), -20);
            foreach (var (pos, yaw) in new[] { (new Vector3(-12.3f, 0, 4), 90f), (new Vector3(-12.3f, 0, 6.1f), 90f), (new Vector3(-12.3f, 0, 8.2f), 90f),
                                               (new Vector3(-11, 0, 9.5f), 0f), (new Vector3(12.2f, 0, 9), 80f), (new Vector3(11.9f, 0, 11.1f), 80f) })
                Place(root, "Fence", pos, yaw);

            // lamps along the path, each with a warm vertex-lit point light
            foreach (var z in new[] { -13f, -7f, 0.5f, 8.5f, 13.5f })
                foreach (var side in new[] { -1, 1 })
                {
                    if ((z > 0 && z < 1) && side > 0) continue;
                    var lamp = Place(root, "Lamp_Post", new Vector3(side * 1.9f, 0, z + side * 0.6f), side > 0 ? -90 : 90);
                    AddLight(lamp.transform, new Vector3(0, 2.3f, 0), Warm, 7f, 1.5f);
                }

            // glowing mushrooms, clusters, flowers, bushes and rocks around the village
            var mushrooms = new[] { "Mushroom_Purple", "Mushroom_Blue", "Mushroom_Pink" };
            for (int i = 0; i < 16; i++)
            {
                var pos = Scatter(rng, 9, 22, avoidPath: true);
                string kind = mushrooms[rng.Next(3)];
                var m = Place(root, kind, pos, rng.Next(360), 0.8f + (float)rng.NextDouble() * 1.1f);
                if (i % 2 == 0) AddLight(m.transform, new Vector3(0, 1.6f, 0), MushroomGlow[kind], 5f, 1.1f);
            }
            for (int i = 0; i < 14; i++) Place(root, "Mushroom_Cluster", Scatter(rng, 3, 20, avoidPath: true), rng.Next(360), 0.8f + (float)rng.NextDouble());
            for (int i = 0; i < 22; i++) Place(root, "Flowers", Scatter(rng, 2.5f, 16, avoidPath: true), rng.Next(360), 1f + (float)rng.NextDouble());
            for (int i = 0; i < 14; i++) Place(root, "Bush", Scatter(rng, 7, 26, avoidPath: true), rng.Next(360), 0.8f + (float)rng.NextDouble() * 0.7f);
            for (int i = 0; i < 9; i++) Place(root, i % 3 == 0 ? "Rock_B" : "Rock_A", Scatter(rng, 8, 30, avoidPath: true), rng.Next(360), 0.7f + (float)rng.NextDouble());

            // forest ring + hills on the horizon
            for (int i = 0; i < 70; i++)
                Place(root, rng.NextDouble() < 0.55 ? "Tree_Pine" : "Tree_Round", Scatter(rng, 21, 44, avoidPath: true), rng.Next(360),
                      0.85f + (float)rng.NextDouble() * 0.6f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2 / 12 + (float)rng.NextDouble() * 0.3f;
                float r = 56 + (float)rng.NextDouble() * 8;
                Place(root, "Hill", new Vector3(Mathf.Cos(a) * r, -0.8f, Mathf.Sin(a) * r), rng.Next(360), 0.9f + (float)rng.NextDouble() * 0.5f);
            }

            // player + camera
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
            player.name = "Player";
            player.transform.SetPositionAndRotation(new Vector3(0, 0.05f, -13), Quaternion.identity);
            var turntable = player.GetComponent<CharacterTurntable>();
            if (turntable != null) UnityEngine.Object.DestroyImmediate(turntable);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.6f; cc.radius = 0.25f; cc.center = new Vector3(0, 0.82f, 0);
            var controller = player.AddComponent<PlayerController>();

            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 50;
            cam.farClipPlane = 140;
            camGo.transform.position = new Vector3(0, 2, -18);
            var psx = camGo.AddComponent<PSXCamera>();
            psx.postShader = Shader.Find("Hidden/DreamWorld/PSXPost");
            psx.targetHeight = 300;
            var follow = camGo.AddComponent<ThirdPersonCamera>();
            follow.target = player.transform;
            controller.cameraTransform = camGo.transform;

            var hud = new GameObject("WorldHUD").AddComponent<WorldHUD>();
            hud.titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PixelifySans.ttf");
            hud.bodyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/VT323-Regular.ttf");

            EditorSceneManager.SaveScene(scene, WorldScene);
            AddToBuildSettings(WorldScene);
            Debug.Log("[World] built " + WorldScene);
        }

        // ---------------------------------------------------------------- creator backdrop

        [MenuItem("dreamWorld/Build Creator Backdrop")]
        public static void BuildCreatorBackdrop()
        {
            LoadKit();
            var scene = EditorSceneManager.OpenScene(CreatorScene, OpenSceneMode.Single);
            foreach (var name in new[] { "Floor", "Backdrop" })
            {
                var old = GameObject.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
            }
            var oldLight = GameObject.Find("Directional Light");
            if (oldLight != null) UnityEngine.Object.DestroyImmediate(oldLight);
            NightAtmosphere(fogStart: 9, fogEnd: 34);
            var root = new GameObject("Backdrop").transform;
            var rng = new Random(7);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root);
            ground.transform.localScale = new Vector3(8, 1, 8);
            ground.GetComponent<Renderer>().sharedMaterial = TiledMaterial("Grass", 40);
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());

            Place(root, "Stage", new Vector3(0, -0.12f, 0), 0, 0.85f, collider: false);
            // a little glade behind her: glowing mushrooms, a lamp, the mushroom house, trees
            var m1 = Place(root, "Mushroom_Purple", new Vector3(-2.6f, 0, 2.2f), 30, 1.5f, collider: false);
            AddLight(m1.transform, new Vector3(0, 1.6f, 0), MushroomGlow["Mushroom_Purple"], 5, 1.3f);
            var m2 = Place(root, "Mushroom_Blue", new Vector3(2.9f, 0, 3.4f), 200, 1.9f, collider: false);
            AddLight(m2.transform, new Vector3(0, 1.6f, 0), MushroomGlow["Mushroom_Blue"], 5, 1.2f);
            Place(root, "Mushroom_Pink", new Vector3(1.6f, 0, 1.6f), 80, 0.7f, collider: false);
            Place(root, "Mushroom_Cluster", new Vector3(-1.5f, 0, 1.0f), 0, 1.2f, collider: false);
            Place(root, "Mushroom_Cluster", new Vector3(2.0f, 0, 0.6f), 120, 0.9f, collider: false);
            Place(root, "Flowers", new Vector3(-1.0f, 0, -0.6f), 0, 1.3f, collider: false);
            Place(root, "Flowers", new Vector3(1.3f, 0, -0.4f), 90, 1.1f, collider: false);
            Place(root, "Mushroom_House", new Vector3(5.5f, 0, 9), -150, 1.1f, collider: false);
            var lamp = Place(root, "Lamp_Post", new Vector3(-4.2f, 0, 4.5f), 60, 1, collider: false);
            AddLight(lamp.transform, new Vector3(0, 2.3f, 0), Warm, 7, 1.4f);
            for (int i = 0; i < 26; i++)
            {
                float a = Mathf.Lerp(Mathf.PI * 0.1f, Mathf.PI * 0.9f, (float)rng.NextDouble());
                float r = 8 + (float)rng.NextDouble() * 14;
                Place(root, rng.NextDouble() < 0.6 ? "Tree_Pine" : "Tree_Round", new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r + 2),
                      rng.Next(360), 0.9f + (float)rng.NextDouble() * 0.5f, collider: false);
            }
            for (int i = 0; i < 6; i++)
                Place(root, "Bush", new Vector3(-6 + i * 2.4f + (float)rng.NextDouble(), 0, 5.5f + (float)rng.NextDouble() * 2), rng.Next(360), 1, collider: false);

            var cam = Camera.main;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 80;
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[World] creator backdrop built");
        }

        // ---------------------------------------------------------------- helpers

        static void NightAtmosphere(float fogStart, float fogEnd)
        {
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/PSX_Sky.mat");
            if (skyMat == null)
            {
                EnsureFolder(MatDir);
                skyMat = new Material(Shader.Find("DreamWorld/PSX Sky"));
                AssetDatabase.CreateAsset(skyMat, MatDir + "/PSX_Sky.mat");
            }
            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.27f, 0.45f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.20f, 0.15f, 0.30f);
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            var moon = new GameObject("Moon Light").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.66f, 0.70f, 1f);
            moon.intensity = 0.65f;
            moon.shadows = LightShadows.None;
            moon.transform.rotation = Quaternion.Euler(38, 160, 0);
        }

        static void LoadKit()
        {
            var json = JsonUtility.FromJson<Kit>(File.ReadAllText(KitDir + "/WorldKit.manifest.json"));
            kit = new Dictionary<string, Prop>();
            foreach (var p in json.props) kit[p.name] = p;
            AssetDatabase.ImportAsset(KitPath);
            var importer = (ModelImporter)AssetImporter.GetAtPath(KitPath);
            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription || importer.importAnimation)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.importAnimation = false;
                importer.SaveAndReimport();
            }
            kitModel = AssetDatabase.LoadAssetAtPath<GameObject>(KitPath);
        }

        // Instantiates one prop from the kit under a holder object (holder = position/yaw/scale).
        static GameObject Place(Transform parent, string name, Vector3 position, float yaw, float scale = 1f, bool collider = true)
        {
            var source = kitModel.transform.Find(name);
            var holder = new GameObject(name).transform;
            holder.SetParent(parent);
            holder.localPosition = position;
            holder.localRotation = Quaternion.Euler(0, yaw, 0);
            holder.localScale = Vector3.one * scale;
            var copy = UnityEngine.Object.Instantiate(source.gameObject, holder);
            copy.name = name + "_mesh";
            copy.transform.localPosition = Vector3.zero;

            var r = copy.GetComponent<MeshRenderer>();
            var p = kit[name];
            var mats = new Material[p.submeshes.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = PropMaterial(p.submeshes[i].texture, p.submeshes[i].unlit);
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (collider && p.collider == "box")
            {
                var b = r.bounds;
                var box = holder.gameObject.AddComponent<BoxCollider>();
                box.center = holder.InverseTransformPoint(b.center);
                box.size = new Vector3(b.size.x, b.size.y, b.size.z) / scale * 0.95f;
            }
            else if (collider && p.collider == "trunk")
            {
                var cap = holder.gameObject.AddComponent<CapsuleCollider>();
                cap.radius = 0.25f;
                cap.height = 2.2f;
                cap.center = new Vector3(0, 1.1f, 0);
            }
            return holder.gameObject;
        }

        static Material PropMaterial(string texture, bool unlit)
        {
            EnsureFolder(MatDir);
            var path = $"{MatDir}/PSX_W_{texture}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("DreamWorld/PSX Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KitDir}/Textures/{texture}.png");
            mat.SetFloat("_Unlit", unlit ? 1 : 0);
            mat.SetFloat("_Softness", 0.1f);
            mat.SetFloat("_AffineStrength", 0.4f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material TiledMaterial(string texture, float tiles)
        {
            var path = $"{MatDir}/PSX_W_{texture}_Tiled{tiles}.mat";
            EnsureFolder(MatDir);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("DreamWorld/PSX Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KitDir}/Textures/{texture}.png");
            mat.mainTextureScale = new Vector2(tiles, tiles);
            mat.SetFloat("_AffineStrength", 0f);   // big ground quads would warp too much
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // A flat paved strip from a to b (slightly above the grass so it doesn't flicker).
        static void PathStrip(Transform parent, Vector3 a, Vector3 b, float width)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Quad);
            strip.name = "Path";
            UnityEngine.Object.DestroyImmediate(strip.GetComponent<Collider>());
            strip.transform.SetParent(parent);
            float length = Vector3.Distance(a, b);
            strip.transform.position = (a + b) / 2 + Vector3.up * 0.02f;
            strip.transform.rotation = Quaternion.LookRotation(Vector3.down, b - a);
            strip.transform.localScale = new Vector3(width, length, 1);
            strip.GetComponent<Renderer>().sharedMaterial = TiledMaterial("Paving", 1f);
            var mat = new Material(strip.GetComponent<Renderer>().sharedMaterial) { mainTextureScale = new Vector2(width / 1.2f, length / 1.2f) };
            var path = $"{MatDir}/PSX_W_Path_{Mathf.RoundToInt(width * 10)}_{Mathf.RoundToInt(length * 10)}.mat";
            AssetDatabase.CreateAsset(mat, path);
            strip.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static Vector3 Scatter(Random rng, float minR, float maxR, bool avoidPath)
        {
            for (int tries = 0; tries < 50; tries++)
            {
                double a = rng.NextDouble() * Math.PI * 2;
                double r = minR + rng.NextDouble() * (maxR - minR);
                var p = new Vector3((float)(Math.Cos(a) * r), 0, (float)(Math.Sin(a) * r) + 3);
                bool onPath = Mathf.Abs(p.x) < 2.6f && p.z > -18 && p.z < 17;
                bool onPlaza = new Vector2(p.x, p.z - 4).magnitude < 5f;
                if (!avoidPath || (!onPath && !onPlaza)) return p;
            }
            return new Vector3(30, 0, 30);
        }

        static void AddLight(Transform parent, Vector3 localPos, Color color, float range, float intensity)
        {
            var go = new GameObject("Glow", typeof(Light));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.GetComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForceVertex;   // PS1-style per-vertex lighting
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == scenePath)) scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
