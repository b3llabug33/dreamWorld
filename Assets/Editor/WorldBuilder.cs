using System;
using System.Collections.Generic;
using System.IO;
using DreamWorld.CharacterCreation;
using DreamWorld.Gameplay;
using DreamWorld.Net;
using DreamWorld.Rendering;
using FishNet.Component.Spawning;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using DreamWorld.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = System.Random;

namespace DreamWorld.EditorTools
{
    // dreamWorld > Build World: Mushroom Hollow  - (re)creates the first world area scene.
    // dreamWorld > Build Creator Backdrop         - the creator/login stage: a forest overgrowing a giant old computer.
    // Props come from Assets/Models/World/WorldKit.fbx (made by ArtSource/build_world.py).
    public static partial class WorldBuilder
    {
        const string KitPath = "Assets/Models/World/WorldKit.fbx";
        const string KitDir = "Assets/Models/World";
        const string MatDir = "Assets/Materials/World";
        const string WorldScene = "Assets/Scenes/World_MushroomHollow.unity";
        const string CreatorScene = "Assets/Scenes/CharacterCreation.unity";
        const string PlayerPrefab = "Assets/Prefabs/Dreamer.prefab";
        const string NetPlayerPrefab = "Assets/Prefabs/NetDreamer.prefab";

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
            SetupModels();   // the code-built models (WorldBuilder.Assets.cs)
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            NightAtmosphere(fogStart: 18, fogEnd: 80);
            var root = new GameObject("MushroomHollow").transform;
            PathSegs.Clear();
            var rng = new Random(42);

            // ground: big enough for the town, the plots and the woods around them
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root);
            // x -84..120 (Mistpine Hollow's own terrain takes over west of -84), z -114..126
            ground.transform.position = new Vector3(18, 0, 6);
            ground.transform.localScale = new Vector3(20.4f, 1, 24);
            ground.GetComponent<Renderer>().sharedMaterial = TiledMaterial("Grass", 120);

            // the town square, its buildings + townsfolk, the paths, the plots and the woods (WorldBuilder.Town.cs)
            BuildTown(root, rng, out var homeContents);
            BuildNewBuildings(root);   // bakery, greenhouse arcade, server cafe, watchtower (WorldBuilder.Assets.cs)
            BuildMistpineTrail(root);  // the trail west to Mistpine Hollow (WorldBuilder.Mistpine.cs), before anything is scattered

            // hills on the horizon
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2 / 16 + (float)rng.NextDouble() * 0.2f;
                float r = 86 + (float)rng.NextDouble() * 10;
                int hillYaw = rng.Next(360);
                float hillScale = 1.2f + (float)rng.NextDouble() * 0.6f;
                if (a > 2.0f && a < 4.3f) continue;   // west: the way to Mistpine Hollow
                var hill = Place(root, "Hill", new Vector3(Mathf.Cos(a) * r, -0.8f, Mathf.Sin(a) * r + 6), hillYaw, hillScale, collider: false);
                foreach (var hr in hill.GetComponentsInChildren<Renderer>()) hr.sharedMaterial = HillMaterial();   // grassy, not flat-shaded
            }

            // monsters out in the wilds (solo for now)
            var shroomling = BuildShroomlingPrefab();
            var monsters = new GameObject("Shroomlings").AddComponent<MonsterSpawner>();
            monsters.transform.SetParent(root);
            monsters.prefab = shroomling;
            int placed = 0;
            for (int tries = 0; tries < 600 && placed < 12; tries++)
            {
                // out in the woods: never in a safe zone, the plots, or right next to a path
                float ang = (float)(rng.NextDouble() * Math.PI * 2), rad = 32 + (float)rng.NextDouble() * 28;
                var pos = new Vector3(Mathf.Cos(ang) * rad, 0, Mathf.Sin(ang) * rad + 6);
                if (!Free(pos, 4f)) continue;
                var spot = new GameObject("Spot").transform;
                spot.SetParent(monsters.transform);
                spot.SetPositionAndRotation(pos, Quaternion.Euler(0, rng.Next(360), 0));
                placed++;
            }

            // weapon shop, enemies, props, furniture and critters (WorldBuilder.Assets.cs)
            BuildModelAssets(root, homeContents);
            // the third-party nature packs: trees, shrubs, grass, rocks, mushrooms, owls (WorldBuilder.Nature.cs)
            NatureDressing(root, rng);
            // rolling hills out in the woods, real grass everywhere, a pond and a stream (WorldBuilder.Ground.cs)
            BuildGround(root, rng);
            // Mistpine Hollow, the foggy woods west of town (WorldBuilder.Mistpine.cs)
            BuildMistpine(root, rng);
            // trees to chop, rocks and ore to mine, cotton, berries... and the workbenches (WorldBuilder.Gathering.cs)
            BuildGathering(root, rng);

            // player + camera
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
            player.name = "Player";
            player.transform.SetPositionAndRotation(new Vector3(0, 0.05f, -10), Quaternion.identity);
            var turntable = player.GetComponent<CharacterTurntable>();
            if (turntable != null) UnityEngine.Object.DestroyImmediate(turntable);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.6f; cc.radius = 0.25f; cc.center = new Vector3(0, 0.82f, 0);
            var controller = player.AddComponent<PlayerController>();
            controller.areaRadius = 82f;
            player.AddComponent<PlayerStats>();
            player.AddComponent<Abilities>();   // spells on Z/X/C (Gameplay/Abilities.cs)
            player.AddComponent<PlayerLife>();
            player.AddComponent<WeaponPose>();

            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 50;
            cam.farClipPlane = 190;
            camGo.transform.position = new Vector3(0, 2, -18);
            var psx = camGo.AddComponent<PSXCamera>();
            psx.postShader = Shader.Find("Hidden/DreamWorld/PSXPost");
            psx.targetHeight = 400;
            var follow = camGo.AddComponent<ThirdPersonCamera>();
            follow.target = player.transform;
            controller.cameraTransform = camGo.transform;

            var hud = new GameObject("WorldHUD").AddComponent<WorldHUD>();
            hud.titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PixelifySans.ttf");
            hud.bodyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/VT323-Regular.ttf");
            var gameHud = new GameObject("GameHUD").AddComponent<GameHUD>();
            gameHud.titleFont = hud.titleFont;
            gameHud.bodyFont = hud.bodyFont;

            // multiplayer: solo keeps the offline Player above; host/join/server spawn NetDreamers
            var netPrefab = BuildNetPlayerPrefab();
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            nmGo.AddComponent<Tugboat>();
            var nmSo = new SerializedObject(nm);
            nmSo.FindProperty("_dontDestroyOnLoad").boolValue = false;   // lives and dies with this scene
            // FishNet's list of networked prefabs (it errors on Awake without one; DefaultPrefabObjects fills itself in)
            nmSo.FindProperty("_spawnablePrefabs").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/DefaultPrefabObjects.asset");
            nmSo.ApplyModifiedPropertiesWithoutUndo();
            var spawner = nmGo.AddComponent<PlayerSpawner>();
            spawner.SetPlayerPrefab(netPrefab.GetComponent<NetworkObject>());
            var spawns = new List<Transform>();
            foreach (var x in new[] { -1.2f, 0f, 1.2f })
            {
                var sp = new GameObject("NetSpawn").transform;
                sp.SetParent(nmGo.transform);
                sp.SetPositionAndRotation(new Vector3(x, 0.05f, -10 + Mathf.Abs(x)), Quaternion.identity);
                spawns.Add(sp);
            }
            spawner.Spawns = spawns.ToArray();
            var launcher = new GameObject("NetLauncher").AddComponent<NetLauncher>();
            launcher.networkManager = nm;
            launcher.offlinePlayer = player;
            launcher.hud = hud;
            PlayerSettings.runInBackground = true;   // a server/host must keep running when unfocused

            NetworkMonsters();   // online: every monster prefab can be shared (WorldBuilder.Net.cs)
            EditorSceneManager.SaveScene(scene, WorldScene);
            AddToBuildSettings(WorldScene);
            Debug.Log("[World] built " + WorldScene);
        }

        // The online version of the character: same Dreamer, plus movement and FishNet sync.
        static GameObject BuildNetPlayerPrefab()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "NetDreamer";
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var turntable = go.GetComponent<CharacterTurntable>();
            if (turntable != null) UnityEngine.Object.DestroyImmediate(turntable);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.6f; cc.radius = 0.25f; cc.center = new Vector3(0, 0.82f, 0);
            go.AddComponent<PlayerController>();
            // owner-only parts start switched off: a remote copy must never claim PlayerStats.Local (that's
            // the HUD, bag and saving). NetPlayer switches them on for the person playing this character.
            go.AddComponent<PlayerStats>().enabled = false;
            go.AddComponent<Abilities>().enabled = false;
            go.AddComponent<PlayerLife>().enabled = false;
            go.AddComponent<WeaponPose>();
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkTransform>();   // owner-driven position/rotation, synced to everyone
            go.AddComponent<NetPlayer>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, NetPlayerPrefab);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------------------------------------------------------------- gameplay pieces

        static void SafeZone(Transform parent, string name, Vector3 center, Vector3 size, float yaw)
        {
            var zone = new GameObject("Zone_" + name).AddComponent<Zone>();
            zone.kind = ZoneKind.Safe;
            zone.displayName = name;
            zone.transform.SetParent(parent);
            zone.transform.SetPositionAndRotation(center + Vector3.up * 3f, Quaternion.Euler(0, yaw, 0));
            zone.transform.localScale = new Vector3(size.x, 8f, size.z);
        }

        // A safe strip along a path from a to b.
        static void SafeStrip(Transform parent, string name, Vector3 a, Vector3 b, float width)
        {
            var d = b - a;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            SafeZone(parent, name, (a + b) / 2, new Vector3(width, 0, d.magnitude + 0.8f), yaw);
        }

        static GameObject Block(Transform parent, string name, Vector3 localPos, Vector3 size, string texture)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = PropMaterial(texture, false);
            return go;
        }

        static void BuildSnackStand(Transform parent, Vector3 pos, float yaw)
        {
            var stand = new GameObject("SnackStand").transform;
            stand.SetParent(parent);
            stand.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            Block(stand, "Counter", new Vector3(0, 0.5f, 0), new Vector3(1.6f, 1f, 0.7f), "Planks");
            Block(stand, "PostL", new Vector3(-0.75f, 1.35f, 0.3f), new Vector3(0.08f, 1.7f, 0.08f), "Planks");
            Block(stand, "PostR", new Vector3(0.75f, 1.35f, 0.3f), new Vector3(0.08f, 1.7f, 0.08f), "Planks");
            var awning = Block(stand, "Awning", new Vector3(0, 2.2f, 0.05f), new Vector3(1.9f, 0.12f, 1.1f), "CapPink");
            awning.transform.localRotation = Quaternion.Euler(-12, 0, 0);
            Block(stand, "Snack1", new Vector3(-0.35f, 1.08f, -0.1f), new Vector3(0.22f, 0.16f, 0.22f), "CapBlue");
            Block(stand, "Snack2", new Vector3(0.05f, 1.08f, -0.05f), new Vector3(0.2f, 0.2f, 0.2f), "CapPurple");
            Block(stand, "Snack3", new Vector3(0.4f, 1.08f, -0.12f), new Vector3(0.24f, 0.14f, 0.24f), "CapPink");
            AddLight(stand, new Vector3(0, 1.9f, -0.6f), Warm, 4f, 0.9f);
            var shop = stand.gameObject.AddComponent<SnackStand>();
            shop.range = 2.4f;
        }

        static void BuildHomeComputer(Transform parent, Vector3 pos, float yaw)
        {
            var home = new GameObject("HomeComputer").transform;
            home.SetParent(parent);
            home.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            Block(home, "Desk", new Vector3(0, 0.38f, 0), new Vector3(1.2f, 0.76f, 0.6f), "Planks");
            Block(home, "Monitor", new Vector3(0, 1.0f, 0.05f), new Vector3(0.62f, 0.48f, 0.42f), "IronDark");
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Screen";
            UnityEngine.Object.DestroyImmediate(screen.GetComponent<Collider>());
            screen.transform.SetParent(home, false);
            screen.transform.localPosition = new Vector3(0, 1.0f, -0.165f);
            screen.transform.localScale = new Vector3(0.5f, 0.38f, 1);
            screen.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/PSX_Screen_Boot.mat");
            AddLight(home, new Vector3(0, 1.0f, -0.7f), new Color(0.75f, 0.62f, 1f), 3.5f, 1.0f);
            var spawn = new GameObject("WakeUpSpot").transform;
            spawn.SetParent(home, false);
            spawn.localPosition = new Vector3(0, 0.05f, -1.4f);
            spawn.localRotation = Quaternion.Euler(0, 180, 0);
            var pc = home.gameObject.AddComponent<HomeComputer>();
            pc.spawnPoint = spawn;
        }

        // The first monster: a hopping mushroom about knee-to-waist high.
        static Monster BuildShroomlingPrefab()
        {
            var root = new GameObject("Shroomling");
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = 0.35f;
            col.height = 0.9f;
            col.center = new Vector3(0, 0.45f, 0);
            var body = Place(root.transform, "Mushroom_Purple", Vector3.zero, 0, 0.42f, collider: false);
            body.name = "Body";
            foreach (var x in new[] { -0.07f, 0.07f })   // little black eyes on the stem
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
                eye.name = "Eye";
                UnityEngine.Object.DestroyImmediate(eye.GetComponent<Collider>());
                eye.transform.SetParent(body.transform, false);
                eye.transform.localPosition = new Vector3(x, 0.62f, -0.17f);
                eye.transform.localScale = new Vector3(0.05f, 0.09f, 0.03f);
                eye.GetComponent<Renderer>().sharedMaterial = PropMaterial("IronDark", false);
            }
            AddLight(body.transform, new Vector3(0, 1.2f, 0), MushroomGlow["Mushroom_Purple"], 2.5f, 0.8f);
            root.AddComponent<DreamWorld.World.GroundFollow>();   // the woods have hills now
            var monster = root.AddComponent<Monster>();
            monster.body = body.transform;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Shroomling.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<Monster>();
        }

        // ---------------------------------------------------------------- creator backdrop

        [MenuItem("dreamWorld/Build Creator Backdrop")]
        public static void BuildCreatorBackdrop()
        {
            LoadKit();
            var scene = EditorSceneManager.OpenScene(CreatorScene, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Floor" || go.name == "Backdrop" || go.name == "Moon Light" || go.name == "Directional Light")
                    UnityEngine.Object.DestroyImmediate(go);
            NightAtmosphere(fogStart: 9, fogEnd: 34);
            var root = new GameObject("Backdrop").transform;
            var rng = new Random(7);

            // An old giant computer the forest has grown over: she stands on a mossy CPU chip in a
            // little circuit-board clearing, and the parts poke out of the grass, half-buried and tilted.
            const float Floor = -0.16f;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root);
            ground.transform.localPosition = new Vector3(0, Floor, 0);
            ground.transform.localScale = new Vector3(8, 1, 8);
            ground.GetComponent<Renderer>().sharedMaterial = TiledMaterial("Grass", 40);
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());

            var clearing = GameObject.CreatePrimitive(PrimitiveType.Plane);
            clearing.name = "Circuit Clearing";
            clearing.transform.SetParent(root);
            clearing.transform.localPosition = new Vector3(0, Floor + 0.01f, 0.4f);
            clearing.transform.localScale = new Vector3(0.36f, 1, 0.36f);
            clearing.GetComponent<Renderer>().sharedMaterial = CircuitMaterial(1);
            UnityEngine.Object.DestroyImmediate(clearing.GetComponent<Collider>());

            Tech(root, "cpu_1", new Vector3(0, Floor + 0.01f, 0.1f), Vector3.zero, 40);
            AddPulseLight(root, new Vector3(0, 0.25f, 0.9f), 2.5f, 0.8f, 0.66f);              // under-glow off the chip

            // overgrown parts: RAM sticks leaning like old pillars, a sunken graphics card, a CRT in the weeds
            for (int i = 0; i < 3; i++)
            {
                var ram = Tech(root, "ram_e_ddr4", new Vector3(-3.2f - i * 0.9f, Floor - 0.5f, 2.8f + i * 1.3f),
                               new Vector3(i * 7 - 6, 20 + i * 15, 90 + (i - 1) * 9), 20);
                GlowPulse(ram, i * 0.12f);
            }
            var gpu = Tech(root, "vga_pcie_b", new Vector3(4.2f, Floor - 0.9f, 5.0f), new Vector3(0, 165, 14), 18);
            GlowPulse(gpu, 0.5f);
            var cooler = Tech(root, "cpu_cooler_e", new Vector3(-1.2f, Floor - 0.6f, 8.0f), new Vector3(8, 200, -6), 22);
            GlowPulse(cooler, 0.3f);
            var tower = Tech(root, "pc_case_b_cover", new Vector3(7.5f, Floor - 0.8f, 11f), new Vector3(0, 210, 16), 8);
            GlowPulse(tower, 0.8f);

            // (Monitor_a's screen faces its +x, so yaw ~90 turns it toward the camera)
            var crtLeft = Tech(root, "Monitor_a", new Vector3(-1.45f, Floor - 0.15f, 2.7f), new Vector3(-6, 76, 6), 2.6f);
            ScreenOn(crtLeft, "Screen_Boot");
            AddLight(crtLeft.transform, new Vector3(0.4f, 0.25f, 0), new Color(0.75f, 0.62f, 1f), 4, 0.9f);
            var crtRight = Tech(root, "Monitor_a", new Vector3(1.55f, Floor - 0.2f, 2.9f), new Vector3(4, 106, -8), 2.2f);
            ScreenOn(crtRight, "Screen_Stats");
            AddLight(crtRight.transform, new Vector3(0.4f, 0.25f, 0), new Color(0.75f, 0.62f, 1f), 4, 0.8f);

            var keys = Tech(root, "keyboard_d", new Vector3(-3.7f, Floor - 0.15f, 0.6f), new Vector3(10, 100, 0), 9);
            GlowPulse(keys, 0.2f);
            var phones = Tech(root, "headphone_b", new Vector3(3.1f, Floor - 0.1f, 1.1f), new Vector3(0, 60, -18), 5);
            GlowPulse(phones, 0.65f);

            // the forest wins: big glowing mushrooms, clusters and flowers crowd around every part
            var m1 = Place(root, "Mushroom_Purple", new Vector3(-2.7f, Floor, 2.0f), 30, 1.5f, collider: false);
            AddLight(m1.transform, new Vector3(0, 1.6f, 0), MushroomGlow["Mushroom_Purple"], 5, 1.3f);
            var m2 = Place(root, "Mushroom_Blue", new Vector3(3.2f, Floor, 3.2f), 200, 1.9f, collider: false);
            AddLight(m2.transform, new Vector3(0, 1.6f, 0), MushroomGlow["Mushroom_Blue"], 5, 1.2f);
            var m3 = Place(root, "Mushroom_Pink", new Vector3(-3.4f, Floor, 9.0f), 80, 2.2f, collider: false);
            AddLight(m3.transform, new Vector3(0, 1.4f, 0), MushroomGlow["Mushroom_Pink"], 4, 1.0f);
            Place(root, "Mushroom_Pink", new Vector3(1.6f, Floor, 1.5f), 80, 0.6f, collider: false);
            var lamp = Place(root, "Lamp_Post", new Vector3(-4.6f, Floor, 5.0f), 60, 1, collider: false);
            AddLight(lamp.transform, new Vector3(0, 2.3f, 0), Warm, 7, 1.2f);
            Place(root, "Mushroom_House", new Vector3(5.5f, Floor, 9), -150, 1.1f, collider: false);

            var spots = new[]
            {
                new Vector3(-1.5f, 0, 1.0f), new Vector3(2.0f, 0, 0.7f), new Vector3(-3.0f, 0, 2.6f), new Vector3(-4.1f, 0, 4.3f),
                new Vector3(-3.4f, 0, 0.0f), new Vector3(3.6f, 0, 4.3f), new Vector3(4.8f, 0, 5.6f), new Vector3(0.9f, 0, 2.1f),
                new Vector3(-3.9f, 0, 1.7f), new Vector3(3.0f, 0, 0.5f), new Vector3(-0.6f, 0, 7.6f), new Vector3(-2.0f, 0, 8.2f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var p = spots[i] + new Vector3(0, Floor, 0);
                string kind = i % 3 == 0 ? "Mushroom_Cluster" : i % 3 == 1 ? "Flowers" : "Bush";
                Place(root, kind, p, rng.Next(360), 0.9f + (float)rng.NextDouble() * 0.5f, collider: false);
            }
            // flowers around the edges of the clearing
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2 + 0.3f;
                Place(root, "Flowers", new Vector3(Mathf.Cos(a) * 1.9f, Floor, 0.4f + Mathf.Sin(a) * 1.9f), rng.Next(360), 0.9f, collider: false);
            }

            // trees all around, a few ruined parts peeking out between them
            for (int i = 0; i < 30; i++)
            {
                float a = Mathf.Lerp(Mathf.PI * 0.08f, Mathf.PI * 0.92f, (float)rng.NextDouble());
                float r = 7 + (float)rng.NextDouble() * 15;
                Place(root, rng.NextDouble() < 0.6 ? "Tree_Pine" : "Tree_Round", new Vector3(Mathf.Cos(a) * r, Floor, Mathf.Sin(a) * r + 2),
                      rng.Next(360), 0.9f + (float)rng.NextDouble() * 0.5f, collider: false);
            }
            for (int i = 0; i < 8; i++)
                Place(root, "Bush", new Vector3(-7 + i * 2.1f + (float)rng.NextDouble(), Floor, 6.5f + (float)rng.NextDouble() * 2), rng.Next(360), 1.1f, collider: false);
            var ruinA = Tech(root, "ram_e_ddr4", new Vector3(-9f, Floor - 1.2f, 15f), new Vector3(0, 30, 75), 40);
            GlowPulse(ruinA, 0.4f);
            var ruinB = Tech(root, "Monitor_a", new Vector3(10f, Floor - 0.6f, 16f), new Vector3(-8, 110, 12), 7);
            ScreenOn(ruinB, "Screen_Boot");

            // key + rim lights on her, slowly shifting pink / violet / cyan
            AddPulseLight(root, new Vector3(-1.8f, 1.8f, -1.4f), 5, 0.8f, 0f);
            AddPulseLight(root, new Vector3(1.7f, 2.1f, 1.3f), 5, 0.9f, 0.33f);

            var cam = Camera.main;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 80;
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[World] creator backdrop built");
        }

        // ---------------------------------------------------------------- computer parts
        // Parts from the Ultimate Pc Computer Pack, copied in by ArtSource/import_tech_assets.py.

        const string TechDir = "Assets/ThirdParty/AssetStore/PCParts";
        const string CircuitDir = "Assets/ThirdParty/AssetStore/Circuit";
        const string ScreenDir = "Assets/Models/Tech/Textures";

        // FBX material name -> (texture in TechDir/Textures, tint, glow strength)
        static readonly Dictionary<string, (string texture, Color tint, float glow)> TechSkins =
            new Dictionary<string, (string, Color, float)>
        {
            { "Monitor_a", ("Monitor_a", new Color(0.72f, 0.68f, 0.92f), 0.6f) },
            { "pc_case_b_body", ("pc_case_b_body", new Color(0.40f, 0.32f, 0.78f), 1.2f) },
            { "pc_case_b_cover", ("pc_case_b_cover", new Color(0.46f, 0.36f, 0.86f), 1.2f) },
            { "pc_case_b_wire", ("pc_case_b_wire", Color.white, 1f) },
            { "cpu_1_9", ("cpu_a__i", new Color(0.80f, 0.72f, 1f), 0.8f) },
            { "cpu_cooler_e", ("cpu_cooler_e_2", new Color(0.62f, 0.58f, 0.80f), 1.6f) },
            { "headphone_b", ("headphone_b_1", new Color(0.55f, 0.48f, 0.80f), 1.4f) },
            { "Keyboard_d", ("Keyboard_d_2", new Color(0.45f, 0.40f, 0.70f), 1.5f) },
            { "motherboard_pcie_a", ("motherboard_pcie_a", new Color(0.72f, 0.62f, 0.95f), 0.9f) },
            { "ram_a__g", ("ram_a__f", new Color(0.62f, 0.56f, 0.85f), 1.6f) },
            { "vga_pcie_b", ("vga_pcie_b", new Color(0.62f, 0.52f, 0.95f), 1.5f) },
        };

        // Places a computer part so its bottom sits at position.y and it's centered on position.x/z.
        static GameObject Tech(Transform parent, string model, Vector3 position, Vector3 euler, float scale)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{TechDir}/{model}.fbx");
            var holder = new GameObject(model).transform;
            holder.SetParent(parent);
            holder.localRotation = Quaternion.Euler(euler);
            holder.localScale = Vector3.one * scale;
            var copy = UnityEngine.Object.Instantiate(source, holder);
            copy.name = model + "_mesh";
            copy.transform.localPosition = Vector3.zero;

            var renderers = copy.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && TechSkins.ContainsKey(mats[i].name)) mats[i] = TechMaterial(mats[i].name);
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (r.name.Contains("_fan_")) SpinFan(r);
            }
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            copy.transform.position += position - new Vector3(b.center.x, b.min.y, b.center.z);
            return holder.gameObject;
        }

        static Material TechMaterial(string name)
        {
            var skin = TechSkins[name];
            var dir = TechDir + "/Materials";
            EnsureFolder(dir);
            var path = $"{dir}/PSX_T_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("DreamWorld/PSX Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TechDir}/Textures/{skin.texture}_AlbedoTransparency.png");
            mat.color = skin.tint;
            var glow = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TechDir}/Textures/{skin.texture}_Emission.png");
            mat.SetTexture("_EmissionMap", glow);
            mat.SetColor("_EmissionColor", glow != null ? new Color(1f, 0.55f, 0.95f) * skin.glow : Color.black);
            mat.SetFloat("_AffineStrength", 0.12f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Fans spin around their thinnest axis, through the middle of the fan mesh.
        static void SpinFan(MeshRenderer fan)
        {
            var bounds = fan.GetComponent<MeshFilter>().sharedMesh.bounds;
            var size = bounds.size;
            var axis = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
            var spin = fan.gameObject.AddComponent<Spin>();
            spin.localAxis = axis;
            spin.localPivot = bounds.center;
            spin.degreesPerSecond = 200f + fan.name.Length * 13f;
        }

        // RGB glow that slowly cycles colors on every renderer of a part.
        static void GlowPulse(GameObject part, float offset)
        {
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>())
            {
                var pulse = r.gameObject.AddComponent<RGBPulse>();
                pulse.glowRenderer = r;
                pulse.offset = offset;
                pulse.glowStrength = 1.4f;
            }
        }

        static void AddPulseLight(Transform parent, Vector3 position, float range, float intensity, float offset)
        {
            AddLight(parent, position, Color.white, range, intensity);
            var light = parent.GetChild(parent.childCount - 1);
            light.name = "RGB Light";
            light.gameObject.AddComponent<RGBPulse>().offset = offset;
        }

        // Puts one of the terminal screens (ArtSource/make_screens.py) on a CRT monitor.
        static void ScreenOn(GameObject monitor, string screen)
        {
            EnsureFolder(MatDir);
            var path = $"{MatDir}/PSX_{screen}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("DreamWorld/PSX Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ScreenDir}/{screen}.png");
            mat.SetFloat("_Unlit", 1);
            mat.SetFloat("_AffineStrength", 0f);
            EditorUtility.SetDirty(mat);
            foreach (var r in monitor.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (i == 1) mats[i] = mat;   // Monitor_a's materials are [body, screen]
                r.sharedMaterials = mats;
            }
        }

        static Material CircuitMaterial(float tiles)
        {
            EnsureFolder(CircuitDir);
            var path = $"{CircuitDir}/PSX_Circuit_Tiled{tiles}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("DreamWorld/PSX Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CircuitDir}/Circuit_Twilight.png");
            mat.mainTextureScale = new Vector2(tiles, tiles);
            mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{CircuitDir}/Circuit_Glow.png"));
            mat.SetColor("_EmissionColor", new Color(0.9f, 0.6f, 1f) * 0.9f);
            mat.SetFloat("_AffineStrength", 0f);   // big ground quads would warp too much
            EditorUtility.SetDirty(mat);
            return mat;
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

            // measure the prop in its own (unrotated, unscaled) space so colliders fit tightly
            var savedRot = holder.rotation;
            var savedScale = holder.localScale;
            holder.rotation = Quaternion.identity;
            holder.localScale = Vector3.one;
            var local = r.bounds;
            local.center -= holder.position;
            holder.rotation = savedRot;
            holder.localScale = savedScale;

            if (collider && p.collider == "box")
            {
                var box = holder.gameObject.AddComponent<BoxCollider>();
                box.center = local.center;
                box.size = local.size * 0.95f;
            }
            else if (collider && p.collider == "round")   // wells and other round solid props
            {
                var cap = holder.gameObject.AddComponent<CapsuleCollider>();
                cap.radius = Mathf.Max(local.extents.x, local.extents.z) * 0.95f;
                cap.height = Mathf.Max(local.size.y, cap.radius * 2);
                cap.center = new Vector3(local.center.x, local.size.y / 2, local.center.z);
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
            // a touch of PS1 texture warp; more than this makes big faces bend with the camera
            mat.SetFloat("_AffineStrength", texture == "Paving" ? 0f : 0.12f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // The far hills: the ground's grass texture, a little darker and bluer so they sit back in the dusk.
        static Material HillMaterial()
        {
            var path = $"{MatDir}/PSX_W_Hill.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(TiledMaterial("Grass", 120));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTextureScale = new Vector2(10, 10);
            mat.color = new Color(0.72f, 0.74f, 0.9f);
            mat.SetFloat("_Softness", 0.25f);
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
        // every path strip laid this build (a, b, width): the ground pass keeps grass off them
        static readonly List<(Vector3 a, Vector3 b, float w)> PathSegs = new List<(Vector3, Vector3, float)>();

        static void PathStrip(Transform parent, Vector3 a, Vector3 b, float width)
        {
            PathSegs.Add((a, b, width));
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
            // two strips the same size share one material (creating it again would replace the asset and
            // leave the first strip with no material at all: a bright magenta path)
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { existing.CopyPropertiesFromMaterial(mat); EditorUtility.SetDirty(existing); mat = existing; }
            else AssetDatabase.CreateAsset(mat, path);
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
