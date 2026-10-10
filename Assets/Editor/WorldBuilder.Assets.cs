using System;
using System.Collections.Generic;
using System.Linq;
using DreamWorld.Gameplay;
using DreamWorld.Town;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace DreamWorld.EditorTools
{
    // The code-built art (ArtSource/build_props.py, build_buildings.py, build_critters.py, exported to FBX by
    // ArtSource/export_unity.py) placed into Mushroom Hollow: the four new buildings, the weapon shop, the
    // enemies and their spawners, props all over town and the woods, furniture inside, and little critters.
    // Every model is vertex colored and uses one of four shared materials (VC_Matte, VC_Shiny, VC_Glow,
    // VC_Glass), picked by the material name each part had in Blender. Called from BuildWorld.
    public static partial class WorldBuilder
    {
        const string ModelsDir = "Assets/Models";
        const string VCDir = "Assets/Materials/VC";
        const string EnemyDir = "Assets/Prefabs/Enemies";
        const string WeaponDir = "Assets/Resources/Weapons";
        static readonly string[] ModelFolders = { "Weapons", "Enemies", "Props", "Buildings", "Critters", "Resources" };

        // ---------------------------------------------------------------- materials + imports

        [MenuItem("dreamWorld/Setup Code-Built Models")]
        public static void SetupModels()
        {
            foreach (var slot in new[] { "Matte", "Shiny", "Glow", "Glass" }) VC(slot);
            foreach (var folder in ModelFolders)
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { $"{ModelsDir}/{folder}" }))
                    SetupImport(AssetDatabase.GUIDToAssetPath(guid));
            BuildWeaponPrefabs();
            AssetDatabase.SaveAssets();
        }

        // The shared vertex-color material for one Blender slot.
        static Material VC(string slot)
        {
            EnsureFolder(VCDir);
            var path = $"{VCDir}/VC_{slot}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("DreamWorld/PSX Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_VertexColor", 1f);
            mat.SetFloat("_AffineStrength", 0f);
            mat.SetFloat("_Softness", slot == "Shiny" ? 0.2f : 0.1f);
            mat.SetFloat("_Unlit", slot == "Glow" ? 1f : 0f);
            mat.SetFloat("_VertexGlow", slot == "Glow" ? 0.35f : slot == "Shiny" ? 0.06f : slot == "Glass" ? 0.12f : 0f);
            bool glass = slot == "Glass";
            mat.SetFloat("_SrcBlend", (float)(glass ? UnityEngine.Rendering.BlendMode.SrcAlpha : UnityEngine.Rendering.BlendMode.One));
            mat.SetFloat("_DstBlend", (float)(glass ? UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : UnityEngine.Rendering.BlendMode.Zero));
            mat.SetFloat("_ZWrite", glass ? 0f : 1f);
            mat.color = glass ? new Color(1f, 1f, 1f, 0.42f) : Color.white;
            mat.renderQueue = glass ? 3000 : -1;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static string SlotOf(string materialName)
        {
            foreach (var slot in new[] { "Shiny", "Glow", "Glass" })
                if (materialName.StartsWith(slot)) return slot;
            return "Matte";
        }

        // No animation/cameras/lights, and every embedded material swapped for the shared VC one.
        static void SetupImport(string path)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            bool dirty = false;
            if (imp.importAnimation || imp.animationType != ModelImporterAnimationType.None || imp.importCameras || imp.importLights)
            {
                imp.importAnimation = false;
                imp.animationType = ModelImporterAnimationType.None;
                imp.importCameras = false;
                imp.importLights = false;
                dirty = true;
            }
            foreach (var m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
            {
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), VC(SlotOf(m.name)));
                dirty = true;
            }
            if (dirty) imp.SaveAndReimport();
        }

        static GameObject ModelAsset(string folder, string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/{folder}/{name}.fbx");
            if (go == null) Debug.LogWarning($"[Assets] missing model {folder}/{name}");
            return go;
        }

        // Held weapons load from Resources at runtime (WeaponPose), so each gets a small prefab there.
        static void BuildWeaponPrefabs()
        {
            EnsureFolder(WeaponDir);
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { $"{ModelsDir}/Weapons" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                go.name = name;
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(go, $"{WeaponDir}/{name}.prefab");
                Object.DestroyImmediate(go);
            }
        }

        // ---------------------------------------------------------------- placing models

        // One model under a holder (position/yaw/scale). Colliders: "box" fits the bounds, "mesh" uses the
        // model itself (buildings, so doorways stay open), "none".
        static GameObject Put(Transform parent, string folder, string name, Vector3 localPos, float yaw, float scale = 1f,
                               string collider = "box", Color? glow = null, float glowRange = 4f, bool world = false)
        {
            var src = ModelAsset(folder, name);
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            if (world) holder.SetPositionAndRotation(localPos, Quaternion.Euler(0, yaw, 0));
            else { holder.localPosition = localPos; holder.localRotation = Quaternion.Euler(0, yaw, 0); }
            holder.localScale = Vector3.one * scale;
            if (src == null) return holder.gameObject;
            var copy = (GameObject)Object.Instantiate(src, holder);
            copy.name = name + "_mesh";
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = src.transform.localRotation;
            foreach (var r in copy.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (collider == "mesh")
                foreach (var mf in copy.GetComponentsInChildren<MeshFilter>())
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            else if (collider == "box")
            {
                var b = LocalBounds(holder);
                var box = holder.gameObject.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size * 0.92f;
            }
            if (glow.HasValue)
                AddLight(holder, new Vector3(0, LocalBounds(holder).size.y * 0.7f, 0), glow.Value, glowRange, 1.2f);
            return holder.gameObject;
        }

        static GameObject Put(Transform parent, string name, Vector3 worldPos, float yaw, float scale = 1f, string collider = "box",
                               Color? glow = null, float glowRange = 4f)
            => Put(parent, "Props", name, worldPos, yaw, scale, collider, glow, glowRange, world: true);

        // Bounds in the holder's own unrotated, unscaled space.
        static Bounds LocalBounds(Transform holder)
        {
            var rs = holder.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(Vector3.up * 0.5f, Vector3.one);
            var b = new Bounds(holder.InverseTransformPoint(rs[0].bounds.center), Vector3.zero);
            foreach (var r in rs)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    b.Encapsulate(holder.InverseTransformPoint(r.transform.TransformPoint(corner)));
                }
            }
            return b;
        }

        static Vector3 W(float x, float z) => new Vector3(x, 0, z);
        static float Toward(Vector3 from, Vector3 to) => Quaternion.LookRotation(Flat(to - from)).eulerAngles.y;   // prop front (+z) faces `to`
        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-4f ? v : Vector3.forward; }

        // ---------------------------------------------------------------- the new buildings

        // Called right after BuildTown so the buildings reserve their ground before anything is scattered.
        static void BuildNewBuildings(Transform root)
        {
            var group = new GameObject("NewBuildings").transform;
            group.SetParent(root);

            // the mushroom bakery: south of town by the south path. it sells food like the snack stand
            var bakery = NewBuilding(group, "MushroomBakery", W(-11, -27), W(-3, -27), 2.6f, 2.6f, new Color(1f, 0.72f, 0.45f),
                                     "the mushroom bakery", "fresh spore bread and cakes. the oven never goes out");
            var counter = new GameObject("BakeryCounter").AddComponent<SnackStand>();
            counter.transform.SetParent(bakery, false);
            counter.transform.localPosition = new Vector3(0.3f, 0, 0);
            counter.title = "mushroom bakery";
            counter.range = 1.8f;
            Put(bakery, "Props", "StrawberryCake", new Vector3(1f, 0.9f, -0.85f), 0, 0.8f, "none");
            Put(bakery, "Props", "MushroomBread", new Vector3(1f, 0.9f, 0.85f), 30, 1f, "none");
            Put(bakery, "Props", "MushroomStool", new Vector3(-1.2f, 0.1f, 0.2f), 0, 0.9f);
            SafeZone(group, "the mushroom bakery", bakery.position, new Vector3(9, 6, 9), bakery.eulerAngles.y);

            // the greenhouse arcade: off the north path, a glass house full of games
            var arcade = NewBuilding(group, "GreenhouseArcade", W(-12, 46), W(3, 46), 3.5f, 2.8f, new Color(0.7f, 1f, 0.8f),
                                     "the greenhouse arcade", "the games are still growing. come back when they're ripe!");
            Put(arcade, "Props", "BugArcade", new Vector3(-1.9f, 0.1f, 2.4f), 180, 1f, glow: new Color(0.6f, 1f, 0.6f), glowRange: 3f);
            Put(arcade, "Props", "ClawMachine", new Vector3(-0.8f, 0.1f, 2.5f), 180, 1f, glow: new Color(1f, 0.6f, 0.9f), glowRange: 3f);
            Put(arcade, "Props", "Jukebox", new Vector3(2.2f, 0.1f, -1.6f), -90, 1f, glow: new Color(1f, 0.8f, 0.4f), glowRange: 3f);
            Put(arcade, "Props", "PottedMonstera", new Vector3(-2.1f, 0.1f, -2.4f), 0, 0.9f);
            SafeZone(group, "the greenhouse arcade", arcade.position, new Vector3(9, 6, 10), arcade.eulerAngles.y);

            // the moss server cafe: the techy edge past the plots, a giant old computer tower full of plants
            var cafe = NewBuilding(group, "MossServerCafe", W(46, 36), W(46, 22), 3f, 3.2f, new Color(0.6f, 1f, 0.75f),
                                   "the moss server cafe", "a cafe inside a giant old computer. it still hums. drinks coming soon");
            Put(cafe, "Props", "PlaidArmchair", new Vector3(-1.1f, 0.1f, 1.2f), 150, 1f);
            Put(cafe, "Props", "MossTerrarium", new Vector3(0.6f, 0.95f, 1.4f), 0, 1f, "none");
            SafeZone(group, "the moss server cafe", cafe.position, new Vector3(8, 6, 10), cafe.eulerAngles.y);
            foreach (var (p, name) in new[] { (W(40, 33), "MossyServerRack"), (W(52, 33.5f), "MossyServerRack"), (W(41, 39), "OvergrownTerminal"),
                                              (W(51, 40), "CRTStack"), (W(43.5f, 28.5f), "OvergrownTerminal") })
                Put(group, name, p, Toward(p, W(46, 26)), 1f, glow: new Color(0.5f, 1f, 0.7f), glowRange: 3.5f);
            Put(group, "CableArch", W(46, 29), 0, 1.2f, "none");

            // the stilt watchtower: out in the west woods. climb the ladder for the view
            var tower = NewBuilding(group, "StiltWatchtower", W(-52, -4), W(-45, 6), 3.3f, 5.6f, new Color(1f, 0.78f, 0.5f),
                                    "the stilt watchtower", "climb up (E at the ladder). you can see the whole hollow from the top");
            var up = new GameObject("LadderBottom").AddComponent<Ladder>();
            var down = new GameObject("LadderTop").AddComponent<Ladder>();
            up.transform.SetParent(tower, false);
            up.transform.localPosition = new Vector3(0, 0.1f, -3.6f);
            up.range = 1.8f;
            down.transform.SetParent(tower, false);
            down.transform.localPosition = new Vector3(0, 4.45f, -1.65f);
            down.transform.localRotation = Quaternion.Euler(0, 180, 0);
            down.verb = "climb down";
            down.range = 1.4f;
            var outTop = new GameObject("TopSpot").transform;
            outTop.SetParent(tower, false);
            outTop.localPosition = new Vector3(0, 4.45f, -1.6f);
            var outBottom = new GameObject("BottomSpot").transform;
            outBottom.SetParent(tower, false);
            outBottom.localPosition = new Vector3(0, 0.1f, -4.6f);
            outBottom.localRotation = Quaternion.Euler(0, 180, 0);
            up.other = outTop;
            down.other = outBottom;
        }

        // A code-built building facing `faceToward` (its doorway is on the model's -z side), with mesh
        // colliders, a light inside, a sign by the door and its ground kept clear of scattering.
        static Transform NewBuilding(Transform parent, string name, Vector3 pos, Vector3 faceToward, float doorZ, float lightY, Color light,
                                     string title, string message)
        {
            float yaw = FaceYaw(pos, faceToward);
            var b = Put(parent, "Buildings", name, pos, yaw, 1f, "mesh", world: true).transform;
            AddLight(b, new Vector3(0, lightY, 0), light, 7f, 1.5f);
            AddLight(b, new Vector3(0, 2.4f, -doorZ - 1f), Warm, 5f, 0.9f);   // a lamp outside the door
            var sign = new GameObject("Sign_" + title).AddComponent<BuildingSign>();
            sign.transform.SetParent(b, false);
            sign.transform.localPosition = new Vector3(1.5f, 0, -doorZ - 1.4f);
            sign.title = title;
            sign.message = message;
            Place(b, "Signpost", new Vector3(1.9f, 0, -doorZ - 1.2f), 180, 1f, collider: false);
            Keep(pos.x - 7, pos.z - 7, pos.x + 7, pos.z + 7);
            // the woods were scattered before the building existed: clear the trees and mushrooms off its ground
            var woods = parent.parent != null ? parent.parent.Find("Woods") : null;
            if (woods != null)
                for (int i = woods.childCount - 1; i >= 0; i--)
                {
                    var c = woods.GetChild(i);
                    if ((Flat(c.position - pos)).magnitude < 8.5f) Object.DestroyImmediate(c.gameObject);
                }
            return b;
        }

        // ---------------------------------------------------------------- the rest (after the shroomlings)

        static void BuildModelAssets(Transform root, Transform homeContents)
        {
            var rng = new Random(77);   // its own seed so the town's scattering stays the same
            WeaponShop(root);
            TownProps(root);
            Interiors(root, homeContents);
            WoodsProps(root, rng);
            Enemies(root, rng);
            Critters(root, rng);
        }

        static readonly string[] WeaponStock =
            { "beetle_horn_sword", "umbrella_sword", "frying_pan", "mushroom_mace", "keyboard_hammer", "crystal_staff", "mantis_scythe", "slingshot", "petal_shield" };

        // The empty shop for rent becomes the weapon shop.
        static void WeaponShop(Transform root)
        {
            var shop = root.Find("Town/EmptyShop");
            if (shop == null) { Debug.LogWarning("[Assets] no EmptyShop"); return; }
            var text = shop.Find("SignText");
            if (text != null) text.GetComponent<TextMesh>().text = "weapons";
            foreach (var s in shop.GetComponentsInChildren<BuildingSign>())
            {
                s.title = "the weapon shop";
                s.message = "swords, maces, a frying pan. use a weapon from your bag to hold it";
            }
            var counter = new GameObject("WeaponCounter").AddComponent<SnackStand>();
            counter.transform.SetParent(shop, false);
            counter.transform.localPosition = new Vector3(0, 0, 0.2f);
            counter.title = "weapon shop";
            counter.stock = WeaponStock;
            counter.range = 2f;
            // weapons hanging on the back wall and lying on the cart
            float x = -2.2f;
            foreach (var w in new[] { "BeetleHornSword", "MushroomMace", "MantisScythe", "UmbrellaSword", "KeyboardHammer", "CrystalStaff" })
            {
                var hang = Put(shop, "Weapons", w, new Vector3(x, 1.0f, 2.75f), 0, 0.85f, "none");
                hang.transform.localRotation = Quaternion.Euler(0, 0, x < 0 ? 12 : -12);
                x += 0.88f;
            }
            var shield = Put(shop, "Weapons", "PetalShield", new Vector3(2.75f, 1.6f, 0.8f), 0, 0.9f, "none");
            shield.transform.localRotation = Quaternion.Euler(0, 0, 90);   // flat against the side wall, facing in
            var pan = Put(shop, "Weapons", "FryingPan", new Vector3(0.3f, 0.95f, 1.3f), 0, 1f, "none");
            pan.transform.localRotation = Quaternion.Euler(90, 70, 0);
            AddLight(shop, new Vector3(0, 2.2f, 1.6f), new Color(1f, 0.8f, 0.55f), 5f, 1.1f);
            Npc(shop, "Beetrix", shop.TransformPoint(new Vector3(0, 0, 2.0f)), shop.eulerAngles.y + 180,
                "Skin=6;Eyes=Upturned 2;Brows=Thick 0;Hair=WolfCut 3;Top=Flannel 3;Bottom=Cargo 10 rise:Mid;Legwear=none;Shoes=Boots 11;Glasses=none;Headwear=none;Neck=none;Mouth=Grin;Extra1=Freckles",
                new[] { "welcome to the weapon shop! the counter's right in front of you.", "the frying pan is my best seller. don't ask.",
                        "use a weapon from your bag to hold it. shift + click for a heavy swing!", "the mushroom knight in the south woods? good luck, friend." });
        }

        static void TownProps(Transform root)
        {
            var t = new GameObject("TownProps").transform;
            t.SetParent(root);
            Put(t, "NoticeBoard", Spread(3.6f, -11.5f), Toward(Spread(3.6f, -11.5f), Spread(0, -11.5f)));
            // the notice board is the bounty board: press E for bounties (Quests/Bounties.cs)
            var board = new GameObject("BountyBoard").AddComponent<DreamWorld.Quests.BountyBoard>();
            board.transform.SetParent(t);
            board.transform.position = Spread(2.6f, -11.5f);
            board.range = 2.4f;
            Put(t, "CrookedSignpost", Spread(-3.4f, -19), Toward(Spread(-3.4f, -19), Spread(0, -19)), 1f, "none");
            Put(t, "MarketStall", Spread(12, 14), Toward(Spread(12, 14), Plaza));
            Put(t, "FruitCrate", Spread(11, 12.6f), 20);
            Put(t, "FruitCrate", Spread(13.4f, 12.9f), -15);
            Put(t, "BugHotel", Spread(-21, 15.5f), Toward(Spread(-21, 15.5f), Plaza));
            Put(t, "DuckStatue", Spread(12, -1.5f), Toward(Spread(12, -1.5f), Plaza));
            Put(t, "SnailBench", Spread(-4.6f, -9.5f), Toward(Spread(-4.6f, -9.5f), Spread(0, -9.5f)));
            Put(t, "LostSockPile", Spread(9.5f, -3.5f), 40, 1f, "none");
            Put(t, "BeetleCart", Spread(13, -2.5f), 70);
            Put(t, "LanternString", Spread(0, 20.5f), 0, 1.4f, "none", glow: new Color(1f, 0.8f, 0.5f), glowRange: 6f);
            Put(t, "LanternString", Spread(19.5f, 8), 90, 1.3f, "none", glow: new Color(1f, 0.8f, 0.5f), glowRange: 6f);
            foreach (var p in new[] { W(-4.8f, -27), W(1f, -34), W(-3.6f, -43), W(10.2f, 44f), W(36f, 10f) })
                Put(t, "MushroomStreetlamp", p, 0, 1f, "none", glow: new Color(0.85f, 0.6f, 1f), glowRange: 7f);
            // the tavern's banners and the general store's chimes
            var tavern = root.Find("Town/Tavern");
            if (tavern != null)
            {
                Put(tavern, "Props", "PlaidBanner", new Vector3(-2.9f, 0.6f, -4.12f), 180, 1f, "none");
                Put(tavern, "Props", "PlaidBanner", new Vector3(2.9f, 0.6f, -4.12f), 180, 1f, "none");
            }
            var store = root.Find("Town/GeneralStore");
            if (store != null) Put(store, "Props", "WindChimes", new Vector3(1.7f, 2.0f, -3.4f), 0, 1f, "none");

            // gothic touches for the vampires: around the wobbly house (where Count Snackula "doesn't" sleep)
            var g = new GameObject("GothicCorner").transform;
            g.SetParent(root);
            var wobbly = root.Find("Town/WobblyHouse");
            if (wobbly != null)
            {
                Put(wobbly, "Props", "BatCandelabra", new Vector3(-1.5f, 0, -2.6f), 180, 1f, "box", new Color(1f, 0.55f, 0.3f), 3f);
                Put(wobbly, "Props", "BatCandelabra", new Vector3(1.5f, 0, -2.6f), 180, 1f, "box", new Color(1f, 0.55f, 0.3f), 3f);
            }
            Put(g, "CoffinBed", Spread(-20.5f, -12.5f), 30);
            Put(g, "SpookyTreeLamp", Spread(-21, -5), 0, 1f, "box", new Color(0.8f, 0.4f, 1f), 6f);
            Put(g, "GraveFlowers", Spread(-19.2f, -14.5f), 10);
            Put(g, "GraveFlowers", Spread(-12.6f, -13.3f), 200);
            Put(g, "WitchCauldron", Spread(-12.4f, -10.6f), 0, 1f, "box", new Color(0.5f, 1f, 0.5f), 4f);
            Put(g, "VampireGnome", Spread(4.3f, 3.6f), Toward(Spread(4.3f, 3.6f), Plaza), 1f, "none", new Color(1f, 0.3f, 0.35f), 2f);
            Put(g, "VampireGnome", Spread(7.6f, -1.6f), Toward(Spread(7.6f, -1.6f), Plaza), 1f, "none");
            Put(g, "CandleCluster", Spread(5.2f, 3.1f), 0, 1f, "none", new Color(1f, 0.7f, 0.4f), 2.5f);
        }

        // Furniture inside the walk-in buildings, fashion props in the studio, home props by your cottage.
        static void Interiors(Transform root, Transform home)
        {
            var tavern = root.Find("Town/Tavern");
            if (tavern != null)
            {
                Put(tavern, "Props", "PlaidArmchair", new Vector3(3f, 0, -2.6f), -130);
                Put(tavern, "Props", "RoundRug", new Vector3(0.2f, 0.03f, -2f), 0, 1f, "none");
                Put(tavern, "Props", "Jukebox", new Vector3(-3.5f, 0, -0.8f), 90, 1f, "box", new Color(1f, 0.7f, 0.9f), 3f);
                Put(tavern, "Props", "CandleCluster", new Vector3(-1.2f, 0.82f, 1.6f), 0, 0.8f, "none");
                Put(tavern, "Props", "CandleCluster", new Vector3(2.1f, 0.82f, 1.4f), 0, 0.8f, "none");
                Put(tavern, "Props", "BugSpecimenFrame", new Vector3(3.9f, 1.5f, 1.2f), -90, 1f, "none");
            }
            var wobbly = root.Find("Town/WobblyHouse");
            if (wobbly != null)
            {
                Put(wobbly, "Props", "RoundRug", new Vector3(0, 0.03f, 0.2f), 0, 0.8f, "none");
                Put(wobbly, "Props", "PlaidArmchair", new Vector3(-1f, 0, 1f), 160);
                Put(wobbly, "Props", "CRTStack", new Vector3(1.2f, 0, 1.3f), 200, 1f, "box", new Color(0.5f, 1f, 0.7f), 3f);
                Put(wobbly, "Props", "FairyLightJar", new Vector3(-1.5f, 0, -1.3f), 0, 1f, "none", new Color(1f, 0.85f, 0.5f), 2.5f);
            }
            var office = root.Find("Town/LandOffice");
            if (office != null) Put(office, "Props", "PottedMonstera", new Vector3(2.2f, 0, -2.1f), 0);
            var store = root.Find("Town/GeneralStore");
            if (store != null)
            {
                Put(store, "Props", "StrawberryCake", new Vector3(-0.5f, 0.82f, 1.2f), 0, 0.8f, "none");
                Put(store, "Props", "MushroomBread", new Vector3(0.2f, 0.82f, 1.4f), 40, 1f, "none");
                Put(store, "Props", "FruitCrate", new Vector3(2.2f, 0, -0.4f), 10);
            }
            var studio = root.Find("Town/FashionStudio");
            if (studio != null)
            {
                Put(studio, "Props", "ClothesRack", new Vector3(-2.3f, 0, 0.4f), 90);
                Put(studio, "Props", "FullMirror", new Vector3(-2.8f, 0, -2.2f), 90);
                Put(studio, "Props", "DressMannequin", new Vector3(1.1f, 0, -2.4f), 200);
                Put(studio, "Props", "FabricBolts", new Vector3(2.75f, 0, 1.4f), -90);
                Put(studio, "Props", "SewingMachine", new Vector3(2.4f, 0.95f, -1.1f), -90, 1f, "none");
            }
            if (home != null)   // your cottage (moves onto the plot you buy)
            {
                Put(home, "Props", "WelcomeMat", new Vector3(0, 0.02f, -1.6f), 0, 1f, "none");
                Put(home, "Props", "FlowerWindowBox", new Vector3(-2f, 0.9f, -1.2f), 0, 1f, "none");
                Put(home, "Props", "FlowerWindowBox", new Vector3(2f, 0.9f, -1.2f), 0, 1f, "none");
                Put(home, "Props", "FrogMailbox", new Vector3(2.9f, 0, -3.4f), 0);
                Put(home, "Props", "VegetablePatch", new Vector3(4.6f, 0, 1f), 90, 1f, "none");
                Put(home, "Props", "Campfire", new Vector3(-4.6f, 0, -3f), 0, 1f, "box", new Color(1f, 0.6f, 0.3f), 5f);
                Put(home, "Props", "Hammock", new Vector3(-5f, 0, 2f), 90);
                Put(home, "Props", "SnailShellPlanter", new Vector3(1.4f, 0, -1.5f), 0);
                Put(home, "Props", "VampireGnome", new Vector3(-1f, 0, -1.6f), 0, 1f, "none");
            }
            // the plots: a mailbox and a bug hotel at the welcome sign
            var plots = root.Find("Plots");
            if (plots != null)
            {
                Put(plots, "FrogMailbox", W(34.6f, 10.2f), 90);
                Put(plots, "BugHotel", W(35, 2.6f), 90);
            }
        }

        static Vector3 WoodsSpot(Random rng, float minR, float maxR, float margin, float a0 = 0, float a1 = 360)
        {
            for (int i = 0; i < 300; i++)
            {
                float a = (a0 + (float)rng.NextDouble() * (a1 - a0)) * Mathf.Deg2Rad, r = minR + (float)rng.NextDouble() * (maxR - minR);
                var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r + 6);
                if (Free(p, margin)) return p;
            }
            return new Vector3(float.NaN, 0, 0);
        }

        static void WoodsProps(Transform root, Random rng)
        {
            var w = new GameObject("WoodsProps").transform;
            w.SetParent(root);
            void Scatter(string name, int n, float minR, float maxR, string col = "box", Color? glow = null, float everyLight = 0f, float s0 = 0.8f, float s1 = 1.3f)
            {
                for (int i = 0; i < n; i++)
                {
                    var p = WoodsSpot(rng, minR, maxR, 0.8f);
                    if (float.IsNaN(p.x)) continue;
                    bool lit = glow.HasValue && everyLight > 0 && i % (int)everyLight == 0;
                    Put(w, name, p, rng.Next(360), s0 + (float)rng.NextDouble() * (s1 - s0), col, lit ? glow : null, 5f);
                }
            }
            Scatter("MossyBoulder", 12, 30, 72);
            Scatter("FernClump", 22, 27, 70, "none");
            Scatter("AutumnBush", 12, 28, 70, "none");
            Scatter("CrystalCluster", 10, 34, 72, "none", new Color(0.7f, 0.6f, 1f), 2, 1f, 1.8f);
            Scatter("GlowcapCluster", 5, 28, 70, "none", new Color(0.4f, 0.9f, 1f), 3, 0.9f, 1.5f);
            Scatter("AntHill", 3, 32, 66, "none");
            Scatter("Caterpillar", 4, 30, 66, "none");
            // along the glow trail: an archway at its start and moonflower lamps
            Put(w, "LeafArchway", W(-29.5f, 15.2f), Quaternion.LookRotation(W(-33, 15.5f) - W(-26, 14.5f)).eulerAngles.y + 90, 1.2f, "none",
                 new Color(0.6f, 1f, 0.6f), 5f);
            foreach (var p in new[] { W(-30, 17.6f), W(-39.5f, 17.2f), W(-46, 9), W(-51, 18.5f) })
                Put(w, "MoonflowerLamp", p, 0, 1f, "none", new Color(0.75f, 0.85f, 1f), 6f);
            foreach (var p in new[] { W(-2, 46), W(4.5f, 52), W(-0.8f, 38) })
                Put(w, "FireflyLantern", p + Vector3.up * 0f, 0, 1.6f, "none", new Color(1f, 0.95f, 0.5f), 4f);
        }

        // ---------------------------------------------------------------- enemies

        static Monster.Drop D(string item, int min = 1, int max = 1, float chance = 1f) => new Monster.Drop { item = item, min = min, max = max, chance = chance };

        static Monster Enemy(string model, Monster.Kind kind, string name, float hp, float damage, int xp, Vector2Int coins, float speed,
                             float range, float windup, float recover, Color tell, Monster.Drop[] drops, float hover = 0f,
                             Action<Monster> extra = null, Color? glow = null)
        {
            EnsureFolder(EnemyDir);
            var root = new GameObject(model);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            body.localPosition = new Vector3(0, hover, 0);
            var mesh = Put(body, "Enemies", model, Vector3.zero, 180, 1f, "none");   // the models face -z: turn them round
            var b = LocalBounds(root.transform);
            var col = root.AddComponent<CapsuleCollider>();
            bool longBody = b.size.z > b.size.y * 1.3f;
            col.direction = longBody ? 2 : 1;
            col.radius = Mathf.Clamp((longBody ? Mathf.Max(b.size.x, b.size.y) : Mathf.Max(b.size.x, b.size.z)) * 0.42f, 0.2f, 1.1f);
            col.height = Mathf.Max(longBody ? b.size.z : b.size.y, col.radius * 2f);
            col.center = b.center;
            if (glow.HasValue) AddLight(body, new Vector3(0, b.size.y * 0.6f, 0), glow.Value, 3f, 1f);
            root.AddComponent<DreamWorld.World.GroundFollow>();   // the woods have hills now
            var m = root.AddComponent<Monster>();
            m.kind = kind;
            m.displayName = name;
            m.maxHealth = hp;
            m.damage = damage;
            m.xpReward = xp;
            m.coinReward = coins;
            m.moveSpeed = speed;
            m.attackRange = range;
            m.windupSeconds = windup;
            m.recoverSeconds = recover;
            m.tellColor = tell;
            m.drops = drops;
            m.body = body;
            m.barHeight = b.max.y + 0.35f;
            extra?.Invoke(m);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{EnemyDir}/{model}.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Monster>();
        }

        static void Enemies(Transform root, Random rng)
        {
            var golem = Enemy("MossGolem", Monster.Kind.Slammer, "moss golem", 140, 22, 70, new Vector2Int(14, 24), 1.2f, 2.8f, 1.0f, 1.6f,
                              new Color(1f, 0.7f, 0.3f), new[] { D("golem_moss", 1, 2), D("dream_shard", chance: 0.15f) },
                              extra: m => { m.poise = true; m.sightRange = 8f; });
            var beetle = Enemy("ThornBeetle", Monster.Kind.Charger, "thorn beetle", 80, 16, 45, new Vector2Int(10, 16), 2f, 2.2f, 0.8f, 1.3f,
                               new Color(1f, 0.35f, 0.3f), new[] { D("thorn", 1, 2), D("glow_jelly", chance: 0.06f) });
            var moth = Enemy("GlitchMoth", Monster.Kind.Flyer, "glitch moth", 30, 8, 30, new Vector2Int(6, 12), 3f, 2f, 0.6f, 1f,
                             new Color(0.6f, 1f, 1f), new[] { D("moth_dust", 1, 2), D("dream_shard", chance: 0.05f) }, hover: 1.4f,
                             glow: new Color(0.6f, 0.9f, 1f));
            var bat = Enemy("NightBat", Monster.Kind.Flyer, "night bat", 25, 9, 28, new Vector2Int(5, 10), 3.6f, 2f, 0.5f, 0.9f,
                            new Color(0.8f, 0.4f, 1f), new[] { D("bat_wing") }, hover: 1.6f, extra: m => m.sightRange = 10f);
            var slug = Enemy("RotSlug", Monster.Kind.Spitter, "rot slug", 50, 10, 35, new Vector2Int(8, 14), 1f, 2f, 0.7f, 1.4f,
                             new Color(0.6f, 1f, 0.3f), new[] { D("rot_slime", 1, 3) }, extra: m => { m.keepAway = 6f; m.projectileSpeed = 7f; });
            var wisp = Enemy("StaticWisp", Monster.Kind.Zapper, "static wisp", 35, 9, 40, new Vector2Int(8, 15), 1.8f, 2f, 0.8f, 1.6f,
                             new Color(0.5f, 0.9f, 1f), new[] { D("static_spark"), D("dream_shard", chance: 0.1f) }, hover: 1.0f,
                             extra: m => { m.keepAway = 7f; m.projectileSpeed = 14f; m.sightRange = 11f; }, glow: new Color(0.5f, 0.85f, 1f));
            var ivy = Enemy("IvySerpent", Monster.Kind.Lunger, "ivy serpent", 45, 14, 38, new Vector2Int(8, 14), 2.4f, 3.5f, 0.45f, 1f,
                            new Color(0.6f, 1f, 0.4f), new[] { D("ivy_leaf", 1, 2) });
            var plush = Enemy("CursedPlush", Monster.Kind.Waddler, "cursed plush", 40, 10, 30, new Vector2Int(6, 12), 1.6f, 2f, 0.7f, 1.4f,
                              new Color(1f, 0.4f, 0.6f), new[] { D("plush_stuffing", 1, 2), D("snack", chance: 0.15f) });
            var knight = Enemy("MushroomKnight", Monster.Kind.Knight, "mushroom knight", 320, 16, 300, new Vector2Int(80, 120), 2.2f, 2.4f, 0.55f, 1.8f,
                               new Color(1f, 0.85f, 0.3f), new[] { D("knight_cap"), D("dream_shard", 2, 3), D("glow_jelly", 1, 2) },
                               extra: m => { m.poise = true; m.sightRange = 11f; m.leashRange = 18f; });

            var spots = new List<Vector3>();
            void Spawns(Monster prefab, string label, int count, float a0, float a1, float minR = 36, float maxR = 70, float respawn = 40f)
            {
                var s = new GameObject("Spawner_" + label).AddComponent<MonsterSpawner>();
                s.transform.SetParent(root);
                s.prefab = prefab;
                s.respawnSeconds = respawn;
                for (int placed = 0, tries = 0; placed < count && tries < 400; tries++)
                {
                    var p = WoodsSpot(rng, minR, maxR, 4f, a0, a1);
                    if (float.IsNaN(p.x) || spots.Any(q => (q - p).magnitude < 7f) || NearHome(p, 11f)) continue;
                    spots.Add(p);
                    var spot = new GameObject("Spot").transform;
                    spot.SetParent(s.transform);
                    spot.SetPositionAndRotation(p, Quaternion.Euler(0, rng.Next(360), 0));
                    placed++;
                }
            }
            // angles around the hollow: 90 = north, 180 = west, 270 = south (the plots are east)
            Spawns(moth, "glitch moths", 3, 60, 150);
            Spawns(bat, "night bats", 3, 60, 150);
            Spawns(wisp, "static wisps", 3, 150, 230);
            Spawns(plush, "cursed plushes", 3, 150, 230);
            Spawns(ivy, "ivy serpents", 3, 150, 230, 32, 60);
            Spawns(beetle, "thorn beetles", 3, 230, 300);
            Spawns(slug, "rot slugs", 3, 230, 300);
            Spawns(golem, "moss golems", 1, 35, 70, 45, 72, 60f);
            Spawns(golem, "moss golems south", 1, 300, 340, 45, 72, 60f);

            // the mushroom knight: one, in its own clearing in the south west woods, ringed by glowcaps
            var k = new GameObject("Spawner_mushroom knight").AddComponent<MonsterSpawner>();
            k.transform.SetParent(root);
            k.prefab = knight;
            k.respawnSeconds = 180f;
            var arena = W(-34, -38);
            var kSpot = new GameObject("Spot").transform;
            kSpot.SetParent(k.transform);
            kSpot.SetPositionAndRotation(arena, Quaternion.Euler(0, 45, 0));
            for (int i = 0; i < 9; i++)
            {
                var p = arena + Quaternion.Euler(0, i * 40, 0) * Vector3.forward * 7.5f;
                Put(root.Find("WoodsProps") ?? root, "GlowcapCluster", p, i * 40, 1.3f, "none", i % 3 == 0 ? new Color(1f, 0.85f, 0.4f) : (Color?)null, 6f);
            }
            var warn = new GameObject("Sign_knight").AddComponent<BuildingSign>();
            warn.transform.SetParent(root);
            warn.transform.position = W(-27, -31);
            warn.title = "a scratched sign";
            warn.message = "TURN BACK. the mushroom knight guards this clearing. (bring snacks, and a good weapon)";
            Place(root, "Signpost", W(-26.6f, -30.6f), 225, 1f, collider: false);
        }

        // The plots (and the home on them) stay clear of monsters.
        static bool NearHome(Vector3 p, float margin)
        {
            float dx = Mathf.Max(32 - p.x, 0, p.x - 62), dz = Mathf.Max(-10 - p.z, 0, p.z - 24);
            return dx * dx + dz * dz < margin * margin;
        }

        // ---------------------------------------------------------------- critters

        static void Critters(Transform root, Random rng)
        {
            var c = new GameObject("Critters").transform;
            c.SetParent(root);
            void Bug(string model, Vector3 p, float scale, bool flies, float speed)
            {
                var holder = new GameObject(model).transform;
                holder.SetParent(c);
                holder.SetPositionAndRotation(p, Quaternion.Euler(0, rng.Next(360), 0));
                var body = new GameObject("Body").transform;
                body.SetParent(holder, false);
                Put(body, "Critters", model, Vector3.zero, 180, scale, "none");
                if (model == "Firefly") AddLight(body, Vector3.up * 0.2f, new Color(1f, 0.95f, 0.5f), 2.5f, 1f);
                var a = holder.gameObject.AddComponent<AmbientCritter>();
                a.flies = flies;
                a.speed = speed;
                a.flyHeight = flies ? 1.2f + (float)rng.NextDouble() * 0.8f : 0f;
                a.wanderRadius = flies ? 4f : 2.5f;
            }
            // a few around town, more in the woods
            foreach (var p in new[] { W(-10, -4), W(9, 20), W(-18, 12), W(18, -3), W(-5, 24) })
                Bug(rng.NextDouble() < 0.5 ? "Ladybug" : "MushroomSnail", p, 0.4f, false, 0.25f);
            for (int i = 0; i < 16; i++)
            {
                var p = WoodsSpot(rng, 28, 66, 0.5f);
                if (float.IsNaN(p.x)) continue;
                switch (i % 5)
                {
                    case 0: Bug("RainbowStagBeetle", p, 0.45f, false, 0.35f); break;
                    case 1: Bug("Ladybug", p, 0.4f, false, 0.3f); break;
                    case 2: Bug("MushroomSnail", p, 0.45f, false, 0.12f); break;
                    case 3: Bug("LunaMoth", p, 0.45f, true, 0.6f); break;
                    default: Bug("Firefly", p, 0.35f, true, 0.5f); break;
                }
            }
        }
    }
}
