using System;
using System.Collections.Generic;
using DreamWorld.CharacterCreation;
using DreamWorld.Gameplay;
using DreamWorld.Town;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace DreamWorld.EditorTools
{
    // The town of Mushroom Hollow: a big safe town square ringed by buildings from Quaternius' (CC0)
    // Medieval Village kit, several you can walk into, townsfolk (vampires, elves, people) and animals,
    // winding paths into whimsical woods, and the plots area east of town where you buy your base.
    public static partial class WorldBuilder
    {
        const string QDir = "Assets/ThirdParty/Quaternius";
        const string QMatDir = "Assets/Materials/Quaternius";
        const float Story = 3.12f;   // one floor of the village kit
        static readonly Vector3 Plaza = new Vector3(0, 0, 8);

        // keep-out areas for scattering trees, monsters, etc. (x0, z0, x1, z1)
        static readonly List<Rect> KeepOut = new List<Rect>();

        // ---------------------------------------------------------------- materials

        static readonly Dictionary<string, string> QTextures = new Dictionary<string, string>
        {
            { "MI_Plaster", "MedievalVillage/Textures/T_Plaster_BaseColor" },
            { "MI_WoodTrim", "MedievalVillage/Textures/T_WoodTrim_BaseColor" },
            { "MI_WoodTrim_Wear", "MedievalVillage/Textures/T_WoodTrim_BaseColor" },
            { "MI_RockTrim", "MedievalVillage/Textures/T_RockTrim_BaseColor" },
            { "MI_UnevenBrick", "MedievalVillage/Textures/T_UnevenBrick_BaseColor" },
            { "MI_Brick", "MedievalVillage/Textures/T_Brick_BaseColor" },
            { "MI_RedBrick", "MedievalVillage/Textures/T_RedBrick_BaseColor" },
            { "MI_RoundTiles", "MedievalVillage/Textures/T_RoundTiles_BaseColor" },
            { "MI_MetalOrnaments", "MedievalVillage/Textures/T_RockTrim_BaseColor" },
            { "MI_Vine", "MedievalVillage/Textures/T_VineLeaf" },
            { "MI_Trim_Furniture", "FantasyProps/Textures/T_Trim_Furniture_BaseColor" },
            { "MI_Trim_Metal", "FantasyProps/Textures/T_Trim_Metal_BaseColor" },
            { "MI_Trim_Cloth", "FantasyProps/Textures/T_Trim_Cloth_BaseColor" },
            { "MI_Banner", "FantasyProps/Textures/T_Trim_Cloth_BaseColor" },
            { "MI_Trim_Props_Vertex", "FantasyProps/Textures/T_Trim_Props_BaseColor" },
        };

        // Which original material a tint applies to (walls / roofs get recolored per building).
        static Color TintFor(string mat, Color wall, Color roof)
        {
            if (mat == "MI_Plaster") return wall;
            if (mat == "MI_RoundTiles") return roof;
            return Color.white;
        }

        static Material QMaterial(Material original, Color tint, string model)
        {
            EnsureFolder(QMatDir);
            string name = original != null ? original.name.Replace(" (Instance)", "") : "none";
            bool flat = !QTextures.ContainsKey(name) && name != "MI_WindowGlass";
            // flat-colored materials share names across models (every animal has a "Main"), so key them by model too
            string key = flat ? $"{model}_{name}_g" : $"{name}_{ColorUtility.ToHtmlStringRGB(tint)}";
            var path = $"{QMatDir}/PSX_Q_{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("DreamWorld/PSX Lit"));
            if (QTextures.TryGetValue(name, out var tex))
            {
                mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{QDir}/{tex}.png");
                mat.color = tint;
                if (name == "MI_Vine") { mat.SetFloat("_Cutoff", 0.5f); mat.color = new Color(0.55f, 0.85f, 0.55f); }
            }
            else if (name == "MI_WindowGlass")
            {
                // windows glow warm at night
                mat.color = new Color(1f, 0.82f, 0.5f);
                mat.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.35f) * 0.9f);
            }
            else
            {
                // untextured models (animals): keep their original flat color
                // the model stores linear colors; convert so they don't come out muddy dark
                var c = original != null && original.HasProperty("_Color") ? original.color : Color.white;
                mat.color = c.gamma * tint;
            }
            mat.SetFloat("_AffineStrength", 0.1f);
            mat.SetFloat("_Softness", 0.12f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void FixTextureImport(string folder)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp.filterMode == FilterMode.Point && imp.maxTextureSize == 256) continue;
                imp.filterMode = FilterMode.Point;
                imp.maxTextureSize = 256;
                imp.mipmapEnabled = true;
                imp.SaveAndReimport();
            }
        }

        // Animal models: legacy animation so the Wanderer can CrossFade "Walk" / "Idle" clips, all looping.
        static void FixAnimalImport(string path)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            if (imp.animationType == ModelImporterAnimationType.Legacy && imp.clipAnimations.Length > 0) return;
            imp.animationType = ModelImporterAnimationType.Legacy;
            imp.SaveAndReimport();
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips) c.wrapMode = WrapMode.Loop;
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }

        // ---------------------------------------------------------------- pieces

        static readonly Color White = Color.white;

        // One Quaternius model ("MedievalVillage/Wall_Plaster_Straight"), PSX materials, optional mesh collider.
        static GameObject Q(Transform parent, string piece, Vector3 localPos, float yaw, float scale = 1f,
                            Color? wall = null, Color? roof = null, bool collider = true, float tilt = 0f)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{QDir}/{piece}.fbx");
            if (src == null) { Debug.LogWarning("[Town] missing " + piece); return new GameObject("missing " + piece); }
            var go = (GameObject)UnityEngine.Object.Instantiate(src, parent);
            go.name = piece.Substring(piece.IndexOf('/') + 1);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(tilt, yaw, 0) * src.transform.localRotation;
            go.transform.localScale = src.transform.localScale * scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name : "none";
                    mats[i] = QMaterial(mats[i], TintFor(n, wall ?? White, roof ?? White), go.name);
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (collider)
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            return go;
        }

        static float FaceYaw(Vector3 from, Vector3 to) => Mathf.Atan2(-(to.x - from.x), -(to.z - from.z)) * Mathf.Rad2Deg;

        // ---------------------------------------------------------------- buildings

        class Style
        {
            public string family = "Plaster";            // Plaster or UnevenBrick
            public Color wall = White, roof = White;
            public string floor = "Floor_WoodDark";
            public bool woodGridSides;
        }

        // A rectangular building, w x d meters (multiples of 2), front (door) facing local -z.
        // floors > 1 stacks stories. enterable leaves the doorway open and lights the inside.
        static Transform Building(Transform parent, string name, Vector3 pos, float yaw, int w, int d, int floors, Style st,
                                  bool enterable = true, bool chimney = false, string roofPiece = null)
        {
            var b = new GameObject(name).transform;
            b.SetParent(parent);
            b.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            string M = "MedievalVillage/";
            string fam = st.family;
            string straight = $"{M}Wall_{fam}_Straight";
            string window = $"{M}Wall_{fam}_Window_Wide_Round";
            string door = $"{M}Wall_{fam}_Door_Round";
            for (int f = 0; f < floors; f++)
            {
                float y = f * Story;
                int nx = w / 2, nz = d / 2;
                for (int i = 0; i < nx; i++)
                {
                    float x = -w / 2f + 1 + 2 * i;
                    bool mid = i == nx / 2;
                    string front = f == 0 && mid ? door : (i % 2 == 0 ? window : straight);
                    Q(b, front, new Vector3(x, y, -d / 2f), 0, wall: st.wall);
                    Q(b, i % 2 == 1 ? window : straight, new Vector3(x, y, d / 2f), 180, wall: st.wall);
                }
                for (int j = 0; j < nz; j++)
                {
                    float z = -d / 2f + 1 + 2 * j;
                    string side = st.woodGridSides && fam == "Plaster" ? $"{M}Wall_Plaster_WoodGrid" : (j % 2 == 1 ? window : straight);
                    Q(b, side, new Vector3(-w / 2f, y, z), 90, wall: st.wall);
                    Q(b, j % 2 == 0 ? window : straight, new Vector3(w / 2f, y, z), -90, wall: st.wall);
                }
                string corner = fam == "Plaster" ? $"{M}Corner_Exterior_Wood" : $"{M}Corner_Exterior_Brick";
                foreach (var c in new[] { new Vector3(-w / 2f, y, -d / 2f), new Vector3(w / 2f, y, -d / 2f), new Vector3(-w / 2f, y, d / 2f), new Vector3(w / 2f, y, d / 2f) })
                    Q(b, corner, c, 0, collider: false);
                // floor tiles (ground floor) and ceilings between stories
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                        Q(b, M + (f == 0 ? st.floor : "Floor_WoodDark"), new Vector3(-w / 2f + 1 + 2 * i, y + 0.01f, -d / 2f + 1 + 2 * j), 0,
                          collider: f == 0);
            }
            float top = floors * Story;
            string roof = roofPiece ?? $"Roof_RoundTiles_{w}x{d}";
            Q(b, M + roof, new Vector3(0, top, 0), 0, roof: st.roof, wall: st.wall);
            if (roofPiece == null && w <= 8)
            {
                Q(b, $"{M}Roof_Front_Brick{w}", new Vector3(0, top, -d / 2f), 0, wall: st.wall);
                Q(b, $"{M}Roof_Front_Brick{w}", new Vector3(0, top, d / 2f), 180, wall: st.wall);
            }
            if (chimney) Q(b, $"{M}Prop_Chimney", new Vector3(w / 2f - 1, top, d / 4f), 0);
            if (enterable)
                AddLight(b, new Vector3(0, Story * 0.8f, 0), new Color(1f, 0.78f, 0.5f), Mathf.Max(w, d) + 2, 1.3f);
            // a warm lamp by the door, outside
            AddLight(b, new Vector3(0, 2.6f, -d / 2f - 1.2f), Warm, 5f, 0.9f);
            return b;
        }

        // A hanging sign above a door: a plank with the shop's name on it.
        static void DoorSign(Transform building, float d, string text, string title, string message)
        {
            var plank = Block(building, "SignBoard", new Vector3(0, 3.0f, -d / 2f - 0.45f), new Vector3(2.2f, 0.5f, 0.08f), "Planks");
            UnityEngine.Object.DestroyImmediate(plank.GetComponent<Collider>());
            var label = new GameObject("SignText", typeof(TextMesh));
            label.transform.SetParent(plank.transform.parent, false);
            label.transform.localPosition = new Vector3(0, 3.0f, -d / 2f - 0.5f);
            var tm = label.GetComponent<TextMesh>();
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/VT323-Regular.ttf");
            tm.font = font;
            tm.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = 0.035f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 0.92f, 0.75f);
            var sign = new GameObject("Sign_" + title).AddComponent<BuildingSign>();
            sign.transform.SetParent(building, false);
            sign.transform.localPosition = new Vector3(0, 0, -d / 2f - 1.3f);
            sign.title = title;
            sign.message = message;
            sign.range = 2.2f;
        }

        // ---------------------------------------------------------------- townsfolk + animals

        static GameObject Npc(Transform parent, string name, Vector3 pos, float yaw, string look, string[] lines,
                              bool elf = false, float wander = 0f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), parent);
            go.name = "NPC_" + name;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var turntable = go.GetComponent<CharacterTurntable>();
            if (turntable != null) UnityEngine.Object.DestroyImmediate(turntable);
            var npcLook = go.AddComponent<NpcLook>();
            npcLook.look = look;
            npcLook.elfEars = elf;
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.82f, 0);
            col.height = 1.6f;
            col.radius = 0.25f;
            if (lines != null)
            {
                var talk = go.AddComponent<NpcTalk>();
                talk.npcName = name;
                talk.lines = lines;
                talk.range = 2.3f;
            }
            if (wander > 0)
            {
                var w = go.AddComponent<Wanderer>();
                w.radius = wander;
                w.speed = 0.9f;
            }
            return go;
        }

        // Height of each Quaternius animal model at scale 1 (measured in Play mode).
        static readonly Dictionary<string, float> AnimalHeights = new Dictionary<string, float>
        {
            { "Fox", 2.98f }, { "Deer", 4.49f }, { "ShibaInu", 3.6f }, { "Husky", 3.39f },
            { "Horse_White", 4.96f }, { "Alpaca", 5.53f }, { "Donkey", 4.09f },
        };

        // A Quaternius animal, scaled to a height in meters, wandering around where it's placed.
        static GameObject Animal(Transform parent, string kind, Vector3 pos, float height, float wander, string name = null, string[] lines = null)
        {
            var path = $"{QDir}/Animals/{kind}.fbx";
            FixAnimalImport(path);
            var holder = new GameObject(name ?? kind).transform;
            holder.SetParent(parent);
            holder.position = pos;
            holder.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0);
            var model = Q(holder, "Animals/" + kind, Vector3.zero, 0, collider: false);
            // skinned bounds aren't reliable at build time for these legacy rigs, so use measured model heights
            model.transform.localScale *= height / AnimalHeights[kind];
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
            var anim = model.GetComponent<Animation>();
            var clips = AnimationUtility.GetAnimationClips(model);
            string Clip(string suffix)
            {
                foreach (var c in clips) if (c != null && c.name.EndsWith("|" + suffix)) return c.name;
                return "";
            }
            var w = holder.gameObject.AddComponent<Wanderer>();
            w.radius = wander;
            w.speed = Mathf.Clamp(height * 0.9f, 0.6f, 1.6f);
            w.walkClip = Clip("Walk");
            w.idleClip = Clip("Idle");
            w.idle2Clip = Clip("Eating") != "" ? Clip("Eating") : Clip("Idle_2");
            if (anim != null && w.idleClip != "") anim.clip = anim.GetClip(w.idleClip);
            var col = holder.gameObject.AddComponent<CapsuleCollider>();
            col.radius = height * 0.3f;
            col.height = height;
            col.center = new Vector3(0, height / 2, 0);
            if (lines != null)
            {
                var talk = holder.gameObject.AddComponent<NpcTalk>();
                talk.npcName = name ?? kind.ToLower();
                talk.lines = lines;
                talk.range = 2.2f;
            }
            return holder.gameObject;
        }

        // ---------------------------------------------------------------- helpers for scattering

        static bool Free(Vector3 p, float margin = 1.5f)
        {
            foreach (var r in KeepOut)
                if (p.x > r.xMin - margin && p.x < r.xMax + margin && p.z > r.yMin - margin && p.z < r.yMax + margin) return false;
            return true;
        }

        static void Keep(float x0, float z0, float x1, float z1) => KeepOut.Add(Rect.MinMaxRect(Mathf.Min(x0, x1), Mathf.Min(z0, z1), Mathf.Max(x0, x1), Mathf.Max(z0, z1)));

        // A winding path through points: paving + a safe strip + keep-out, with lanterns every few meters.
        static void Trail(Transform root, string name, Vector3[] pts, float width, float lanternEvery = 0f, bool safe = true)
        {
            float sinceLamp = 0;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                PathStrip(root, pts[i], pts[i + 1], width);
                if (safe) SafeStrip(root, name, pts[i], pts[i + 1], width + 1.4f);
                Keep(Mathf.Min(pts[i].x, pts[i + 1].x) - width, Mathf.Min(pts[i].z, pts[i + 1].z) - width,
                     Mathf.Max(pts[i].x, pts[i + 1].x) + width, Mathf.Max(pts[i].z, pts[i + 1].z) + width);
                if (lanternEvery <= 0) continue;
                Vector3 dir = (pts[i + 1] - pts[i]);
                float len = dir.magnitude;
                dir /= len;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                for (float t = 0; t < len; t += 1f)
                {
                    sinceLamp += 1f;
                    if (sinceLamp < lanternEvery) continue;
                    sinceLamp = 0;
                    var p = pts[i] + dir * t + side * (width / 2 + 0.8f) * ((int)(t + i) % 2 == 0 ? 1 : -1);
                    var lamp = Place(root, "Lamp_Post", p, UnityEngine.Random.Range(0, 360), 0.9f, collider: false);
                    AddLight(lamp.transform, new Vector3(0, 2.1f, 0), Warm, 6f, 1.2f);
                }
            }
        }

        // ---------------------------------------------------------------- the town

        static readonly string[] Mushrooms = { "Mushroom_Purple", "Mushroom_Blue", "Mushroom_Pink" };

        static void BuildTown(Transform root, Random rng, out Transform homeContents)
        {
            KeepOut.Clear();
            UnityEngine.Random.InitState(7);
            FixTextureImport(QDir);
            var town = new GameObject("Town").transform;
            town.SetParent(root);

            // --- the town square: big round plaza + cross paths, all a safe zone
            SafeZone(root, "town square", new Vector3(0, 0, 8), new Vector3(54, 0, 50), 0);
            Keep(-27, -17, 27, 33);
            var plaza = Place(town, "Stage", new Vector3(0, -0.06f, 8), 0, 5.2f, collider: false);   // the round paved plaza
            plaza.transform.localScale = new Vector3(5.2f, 2.6f, 5.2f);   // wide but still just a small step up
            PathStrip(town, new Vector3(0, 0, -17), new Vector3(0, 0, 33), 3.2f);
            PathStrip(town, new Vector3(-27, 0, 8), new Vector3(27, 0, 8), 3.2f);
            Place(town, "Well", Plaza, 20);
            foreach (var a in new[] { 30f, 150f, 210f, 330f })
            {
                var p = Plaza + Quaternion.Euler(0, a, 0) * Vector3.forward * 5.2f;
                Place(town, "Bench", p, a + 180, 1f, collider: false);
            }
            for (int i = 0; i < 8; i++)
            {
                var p = Plaza + Quaternion.Euler(0, 22.5f + i * 45, 0) * Vector3.forward * 12.5f;
                var lamp = Place(town, "Lamp_Post", p, i * 45, 1f, collider: false);
                AddLight(lamp.transform, new Vector3(0, 2.3f, 0), Warm, 8f, 1.4f);
            }
            var townSpawn = new GameObject("TownSpawn").AddComponent<RespawnPoint>();
            townSpawn.transform.SetParent(town);
            townSpawn.transform.SetPositionAndRotation(new Vector3(0, 0.05f, -10), Quaternion.identity);

            // --- buildings around the plaza, all facing the well
            var peach = new Color(1f, 0.86f, 0.74f);
            var mint = new Color(0.78f, 1f, 0.88f);
            var pink = new Color(1f, 0.8f, 0.9f);
            var lavender = new Color(0.86f, 0.8f, 1f);
            var butter = new Color(1f, 0.95f, 0.7f);
            var redRoof = White;
            var tealRoof = new Color(0.55f, 0.9f, 0.95f);
            var purpleRoof = new Color(0.8f, 0.6f, 1f);
            var nightRoof = new Color(0.55f, 0.55f, 0.8f);

            // tavern (north)
            var tavernPos = new Vector3(0, 0, 27);
            var tavern = Building(town, "Tavern", tavernPos, FaceYaw(tavernPos, Plaza), 8, 8, 1,
                                  new Style { wall = peach, roof = redRoof, woodGridSides = true }, chimney: true);
            DoorSign(tavern, 8, "the sleepy spore", "the sleepy spore", "a cozy tavern. drinks + stories coming soon!");
            Q(tavern, "FantasyProps/Table_Large", new Vector3(-1.5f, 0, 1.5f), 90);
            Q(tavern, "FantasyProps/Table_Large", new Vector3(1.8f, 0, 1.5f), 90);
            foreach (var x in new[] { -2.3f, -0.7f, 1f, 2.6f })
                Q(tavern, "FantasyProps/Stool", new Vector3(x, 0, 0.4f), 0);
            Q(tavern, "FantasyProps/Mug", new Vector3(-1.5f, 0.82f, 1.2f), 30, collider: false);
            Q(tavern, "FantasyProps/Mug", new Vector3(1.8f, 0.82f, 1.8f), 80, collider: false);
            Q(tavern, "FantasyProps/Barrel", new Vector3(-3.2f, 0, 3.2f), 0);
            Q(tavern, "FantasyProps/Barrel", new Vector3(-2.4f, 0, 3.3f), 40);
            Q(tavern, "FantasyProps/Barrel_Apples", new Vector3(3.2f, 0, 3.2f), 0);
            Q(tavern, "FantasyProps/Chandelier", new Vector3(0, Story - 0.05f, 1f), 0, collider: false);
            Q(tavern, "FantasyProps/Bookcase_2", new Vector3(3.6f, 0, -1f), -90);
            Q(tavern, "MedievalVillage/Prop_Wagon", new Vector3(5.5f, 0, -2f), 20);

            // general store (north east)
            var storePos = new Vector3(15, 0, 23);
            var store = Building(town, "GeneralStore", storePos, FaceYaw(storePos, Plaza), 6, 6, 1,
                                 new Style { wall = mint, roof = tealRoof });
            DoorSign(store, 6, "general store", "general store", "coming soon! (for now, the snack stand by the well sells food)");
            Q(store, "FantasyProps/Shelf_Small_Bottles", new Vector3(-2.6f, 1.2f, 1.5f), 90, collider: false);
            Q(store, "FantasyProps/Shelf_Small_Bottles", new Vector3(2.6f, 1.2f, 1.5f), -90, collider: false);
            Q(store, "FantasyProps/Barrel_Apples", new Vector3(-2f, 0, 2.2f), 0);
            Q(store, "FantasyProps/FarmCrate_Apple", new Vector3(2f, 0, 2.3f), 0);
            Q(store, "FantasyProps/Table_Large", new Vector3(0, 0, 1.2f), 0);
            Q(store, "FantasyProps/Coin_Pile", new Vector3(0.6f, 0.82f, 1.2f), 0, collider: false);
            Q(store, "FantasyProps/Crate_Wooden", new Vector3(-2.2f, 0, -1.8f), 15);

            // fashion studio (north west): for the fashion design to come
            var tailorPos = new Vector3(-16, 0, 23);
            var tailor = Building(town, "FashionStudio", tailorPos, FaceYaw(tailorPos, Plaza), 6, 8, 1,
                                  new Style { family = "UnevenBrick", wall = pink, roof = purpleRoof, floor = "Floor_WoodLight" });
            DoorSign(tailor, 8, "fashion studio", "fashion studio", "coming soon! design and sell your own clothes here");
            Q(tailor, "FantasyProps/Dummy", new Vector3(-1.8f, 0, 2.6f), 160);
            Q(tailor, "FantasyProps/Dummy", new Vector3(0, 0, 3f), 180);
            Q(tailor, "FantasyProps/Dummy", new Vector3(1.8f, 0, 2.6f), 200);
            Q(tailor, "FantasyProps/Workbench", new Vector3(2.4f, 0, -1f), -90);
            Q(tailor, "FantasyProps/Banner_1", new Vector3(-2.75f, 0.5f, 0f), 90, collider: false);
            Q(tailor, "FantasyProps/Mug", new Vector3(2.4f, 0.95f, -0.6f), 0, collider: false);

            // land office (east), right by the path to the plots
            var officePos = new Vector3(21, 0, 4);
            var office = Building(town, "LandOffice", officePos, FaceYaw(officePos, Plaza), 6, 6, 1,
                                  new Style { wall = lavender, roof = nightRoof, floor = "Floor_RedBrick" });
            DoorSign(office, 6, "land office", "land office", "buy your own plot of land inside! (100 coins)");
            Q(office, "FantasyProps/Table_Large", new Vector3(0, 0, 0.8f), 0);
            Q(office, "FantasyProps/Book_Stack_1", new Vector3(-0.8f, 0.82f, 0.8f), 20, collider: false);
            Q(office, "FantasyProps/CandleStick_Triple", new Vector3(0.9f, 0.82f, 0.8f), 0, collider: false);
            Q(office, "FantasyProps/Bookcase_2", new Vector3(-2.5f, 0, 2.6f), 0);
            Q(office, "FantasyProps/Bookcase_2", new Vector3(1.2f, 0, 2.6f), 0);

            // wizard tower (west): three stories, a potion shop downstairs
            var towerPos = new Vector3(-22, 0, 7);
            var tower = Building(town, "WizardTower", towerPos, FaceYaw(towerPos, Plaza), 4, 4, 3,
                                 new Style { family = "UnevenBrick", wall = lavender, roof = purpleRoof, floor = "Floor_UnevenBrick" },
                                 roofPiece: "Roof_Tower_RoundTiles");
            DoorSign(tower, 4, "potions", "potion shop", "coming soon! the wizard is out picking mushrooms");
            Q(tower, "FantasyProps/Cauldron", new Vector3(0, 0, 0.6f), 0);
            Q(tower, "FantasyProps/Potion_1", new Vector3(-1.1f, 0, 1.3f), 0, collider: false);
            Q(tower, "FantasyProps/Potion_2", new Vector3(1.2f, 0, 1.2f), 0, collider: false);
            AddLight(tower, new Vector3(0, 1f, 0.6f), new Color(0.6f, 1f, 0.6f), 4f, 1.4f);   // green cauldron glow

            // the wobbly house (south west): three crooked stories, nobody knows who lives there
            var wobblyPos = new Vector3(-16, 0, -8);
            var wobbly = new GameObject("WobblyHouse").transform;
            wobbly.SetParent(town);
            wobbly.SetPositionAndRotation(wobblyPos, Quaternion.Euler(0, FaceYaw(wobblyPos, Plaza), 0));
            for (int f = 0; f < 3; f++)
            {
                var floor = Building(wobbly, "Floor" + f, Vector3.zero, 0, 4, 4, 1,
                                     new Style { wall = f % 2 == 0 ? butter : pink, roof = tealRoof }, enterable: f == 0);
                floor.localPosition = new Vector3(f * 0.35f, f * Story, f * -0.2f);
                floor.localRotation = Quaternion.Euler(f * 2.5f, f * 9f, -f * 3f);
                if (f < 2)   // only the top floor keeps its roof
                    foreach (Transform c in floor)
                        if (c.name.StartsWith("Roof_")) UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
            DoorSign(wobbly, 4, "???", "the wobbly house", "nobody knows who lives here. the lights are always on");

            // bakery / empty shop (south east)
            var shopPos = new Vector3(16, 0, -8);
            var shop = Building(town, "EmptyShop", shopPos, FaceYaw(shopPos, Plaza), 6, 6, 1,
                                new Style { wall = butter, roof = redRoof, woodGridSides = true }, chimney: true);
            DoorSign(shop, 6, "for rent", "empty shop", "coming soon! this one's waiting for someone with big ideas");
            Q(shop, "FantasyProps/Stall_Cart_Empty", new Vector3(0, 0, 1.2f), 0);
            Q(shop, "FantasyProps/Crate_Wooden", new Vector3(-2f, 0, 2f), 30);
            Q(shop, "FantasyProps/Crate_Wooden", new Vector3(-2f, 0.75f, 2f), 70);

            // mushroom houses from the original village tucked between
            Place(town, "Mushroom_House", new Vector3(-23, 0, -3), 60, 1.2f);
            Place(town, "Mushroom_House", new Vector3(24, 0, 15), -110, 1.1f);
            Place(town, "Mushroom_House", new Vector3(8, 0, -14), -20, 0.9f);

            // market stalls, carts and odds + ends around the plaza
            foreach (var (p, label) in new[] { (new Vector3(-8, 0, 15), "flower stall"), (new Vector3(8, 0, 16), "trinket stall"),
                                               (new Vector3(-9, 0, 0), "rock stall") })
            {
                var stall = Q(town, "FantasyProps/Stall_Empty", p, FaceYaw(p, Plaza));
                var sign = new GameObject("Sign_" + label).AddComponent<BuildingSign>();
                sign.transform.SetParent(town);
                sign.transform.position = p + (Plaza - p).normalized * 1.3f;
                sign.title = label;
                sign.message = label == "rock stall" ? "just rocks. very nice rocks though" : "empty for now. a player could run this someday";
            }
            Q(town, "FantasyProps/Barrel", new Vector3(-6.5f, 0, 16.3f), 0);
            Q(town, "FantasyProps/Crate_Wooden", new Vector3(9.6f, 0, 17f), 20);
            Q(town, "FantasyProps/Crate_Wooden", new Vector3(9.8f, 0, 17.1f) + Vector3.up * 0.76f, 55);
            Q(town, "FantasyProps/Crate_Wooden", new Vector3(9.7f, 1.5f, 17f), 10, tilt: 8);   // the teetering stack
            Q(town, "MedievalVillage/Prop_Crate", new Vector3(-10.5f, 0, -1f), 40);
            BuildSnackStand(town, new Vector3(5.5f, 0, 1.5f), -150);

            // silly: a giant mug used as a planter, overflowing with mushrooms
            var mugPos = new Vector3(-6, 0, -5);
            Q(town, "FantasyProps/Mug", mugPos, 200, 9f);
            Place(town, "Mushroom_Pink", mugPos + new Vector3(0, 1.2f, 0), 0, 0.8f, collider: false);
            Place(town, "Mushroom_Cluster", mugPos + new Vector3(0.3f, 1.25f, 0.2f), 60, 0.9f, collider: false);
            var mugSign = new GameObject("Sign_mug").AddComponent<BuildingSign>();
            mugSign.transform.SetParent(town);
            mugSign.transform.position = mugPos + new Vector3(0, 0, -1.8f);
            mugSign.title = "the world's largest mug";
            mugSign.message = "donated by a very thirsty giant";
            // vines on a few walls
            foreach (var (b, x) in new[] { (tavern, -3f), (tailor, 1.5f), (shop, 2f) })
                Q(b, "MedievalVillage/Prop_Vine2", new Vector3(x, Story, -(b == tavern ? 4f : b == tailor ? 4f : 3f) - 0.31f), 0, collider: false);

            // --- townsfolk
            var npcs = new GameObject("Townsfolk").transform;
            npcs.SetParent(root);
            Npc(npcs, "Count Snackula", new Vector3(7.3f, 0, 0.4f), -120,
                "Skin=#EEEAF4;Eyes=CatEye #D01E32;Mouth=Fang;Brows=Angled 0;Hair=Ponytail 0;Top=Cardigan 11;Bottom=SkinnyJeans 11 rise:High;Legwear=none;Shoes=Boots 11;Neck=Chain 2;Headwear=none;Glasses=none;Extra1=none",
                new[] { "i vant to sell you... a snack. ten coins.", "fruit punch only. what else would i drink?", "garlic? never heard of her.",
                        "i sleep all day in the wobbly house. kidding. or am i" });
            Npc(npcs, "Vesper", new Vector3(-4, 0, 12), 0,
                "Skin=#E8E6F2;Eyes=Doe #B0122A;Mouth=Fang;Brows=Thin 0;Hair=Long 0 bangs:Straight length:Long;Top=SlipDress 12;Bottom=none;Legwear=Fishnets 11;Shoes=Platforms 11;Neck=Choker 11;Headwear=none;Glasses=none;Extra1=TearTattoo",
                new[] { "the moon is so pretty in mushroom hollow.", "don't mind me, just haunting.", "ever tried being a bat? overrated." },
                wander: 6f);
            Npc(npcs, "Elowen", tailor.TransformPoint(new Vector3(0, 0, 0.5f)), tailor.eulerAngles.y + 180,
                "Skin=5;Eyes=Almond #3FA86A;Brows=Soft 1;Hair=Long 13 bangs:Middle length:Long;Top=PuffBlouse 6;Bottom=LongSkirt 4;Legwear=none;Shoes=MaryJanes 10;Headwear=StarClips 2;Neck=none;Glasses=none;Extra1=Freckles",
                new[] { "welcome to my studio! soon you'll design clothes here.", "elves have the best fashion sense. it's in the ears.",
                        "stripes AND leopard? bold. i love it." }, elf: true);
            Npc(npcs, "Fennel", new Vector3(-12, 0, 4), 90,
                "Skin=1;Eyes=Upturned #8A5CF0;Brows=Straight 8;Hair=Pixie 8;Top=Hoodie 4;Bottom=Cargo 10 rise:Mid;Legwear=none;Shoes=Boots 10;Glasses=Round 2;Headwear=none;Neck=none;Extra1=none",
                new[] { "the woods to the west glow at night. go look!", "i'm an elf. yes, the ears are real.", "have you seen a fox around? he owes me a mushroom." },
                elf: true, wander: 5f);
            Npc(npcs, "Mayor Mothwick", Plaza + new Vector3(2.4f, 0, -2.2f), -40,
                "Skin=2;Eyes=Round 6;Brows=Thick 8;Hair=Bob 8 bangs:Left length:Short;Top=Sweater 13;Bottom=Pleated 12 rise:Mid;Legwear=KneeSocks 11;Shoes=Loafers 11;Glasses=Slim 2;Headwear=Beret 13;Neck=none;Mouth=Smile;Extra1=none",
                new[] { "welcome to mushroom hollow, the coziest town in the dream!", "the town square is a safe zone. no fighting, please.",
                        "want your own home? the land office east of the well sells plots.", "beware the shroomlings past the paths." });
            Npc(npcs, "Pip", office.TransformPoint(new Vector3(0, 0, 1.6f)), office.eulerAngles.y + 180,
                "Skin=3;Eyes=Puppy 1;Brows=Soft 0;Hair=Twintails 3 bangs:Choppy length:Short tie:Low;Top=Sailor 3;Bottom=Skirt 3;Legwear=KneeSocks 0;Shoes=MaryJanes 11;Glasses=Round 11;Headwear=none;Neck=none;Mouth=Tiny;Extra1=Blush",
                null).AddComponent<LandOffice>().range = 3.2f;
            Npc(npcs, "Bartholomew", tavern.TransformPoint(new Vector3(0, 0, 2.6f)), tavern.eulerAngles.y + 180,
                "Skin=4;Eyes=Toon 6;Brows=Thick 0;Hair=Buns 0;Top=Flannel 8;Bottom=BaggyJeans 21 rise:Mid;Legwear=none;Shoes=Combat 11;Mouth=Grin;Headwear=Beanie 11;Neck=none;Glasses=none;Extra1=none",
                new[] { "the sleepy spore is open! well, almost. no drinks yet.", "i make a mean mushroom soup. someday.", "pull up a stool!" });
            Npc(npcs, "Juniper", Plaza + new Vector3(-3, 0, -4), 30,
                "Skin=0;Eyes=Sparkly 4;Brows=Arched 4;Hair=Twintails 4 bangs:Straight length:Medium tie:High;Top=Oversized 1;Bottom=Shorts 22 rise:High;Legwear=FrillySocks 0;Shoes=Sneakers 0;Mouth=Cat;Headwear=Halo 9;Neck=none;Glasses=none;Extra1=HeartBlush",
                new[] { "tag! you're it!", "i saw a snail wearing a mushroom. for real!", "when i grow up i want a plot with a pink house." },
                wander: 7f);
            Npc(npcs, "Grandma Moss", Plaza + new Vector3(-4.6f, 0, 2.6f), 120,
                "Skin=1;Eyes=Monolid 4;Brows=Soft 8;Hair=Buns 8;Top=Cardigan 4;Bottom=LongSkirt 10;Legwear=none;Shoes=Loafers 10;Glasses=CatEye 2;Headwear=none;Neck=Ribbon 3;Mouth=Smile;Extra1=Blush",
                new[] { "back in my day the mushrooms didn't glow.", "eat your snacks, dear. food keeps you healthy.", "bank your coins at home before you go wandering!" });
            Npc(npcs, "Sir Wobbles", wobblyPos + new Vector3(3, 0, -3), 200,
                "Skin=7;Eyes=PixelHex 9;Brows=Dots 6;Hair=WolfCut 6;Top=Tank 17;Bottom=FlareJeans 20 rise:Low;Legwear=none;Shoes=Platforms 1;Glasses=Shutter 1;Headwear=CatEars 1;Neck=none;Mouth=Wavy;Extra1=Sparkles",
                new[] { "is the house leaning or am i?", "i'm blue, it's a whole thing.", "the house told me a secret. i can't tell you." });

            // --- animals around town
            var animals = new GameObject("Animals").transform;
            animals.SetParent(root);
            Animal(animals, "ShibaInu", Plaza + new Vector3(5, 0, 6), 0.55f, 7f, "Biscuit", new[] { "woof!", "*wags tail*", "*sniffs your bag for snacks*" });
            Animal(animals, "Donkey", tavernPos + new Vector3(6, 0, -5), 1.3f, 3f, "Doug the donkey", new[] { "hee-haw.", "*judges you quietly*" });
            Animal(animals, "Horse_White", new Vector3(4, 0, -26), 1.6f, 4f, "Moonbeam", new[] { "*neighs majestically*", "*has never once been ridden*" });

            // --- paths out of town (safe) into the woods
            Trail(root, "the south path", new[] { new Vector3(0, 0, -17), new Vector3(-3, 0, -28), new Vector3(2, 0, -40), new Vector3(-2, 0, -52) }, 2.6f, 9f);
            Trail(root, "the north path", new[] { new Vector3(0, 0, 33), new Vector3(5, 0, 44), new Vector3(1, 0, 56) }, 2.6f, 9f);
            Trail(root, "the glow trail", new[] { new Vector3(-27, 0, 8), new Vector3(-36, 0, 15), new Vector3(-45, 0, 11), new Vector3(-54, 0, 19) }, 2.2f, 6f);
            Trail(root, "the plot road", new[] { new Vector3(27, 0, 8), new Vector3(33, 0, 6) }, 3f, 0f);

            // --- the plots: buy one at the land office for 100 coins
            BuildPlots(root, out homeContents);

            // --- whimsical woods all around
            var woods = new GameObject("Woods").transform;
            woods.SetParent(root);
            int trees = 0;
            for (int tries = 0; tries < 2000 && trees < 140; tries++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2), r = 30 + (float)rng.NextDouble() * 42;
                var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r + 6);
                if (!Free(p, 2f)) continue;
                Place(woods, rng.NextDouble() < 0.5 ? "Tree_Pine" : "Tree_Round", p, rng.Next(360), 0.9f + (float)rng.NextDouble() * 0.8f, collider: true);
                trees++;
            }
            int giants = 0;
            for (int tries = 0; tries < 600 && giants < 22; tries++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2), r = 29 + (float)rng.NextDouble() * 30;
                var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r + 6);
                if (!Free(p, 1f)) continue;
                string kind = Mushrooms[rng.Next(3)];
                var m = Place(woods, kind, p, rng.Next(360), 1.8f + (float)rng.NextDouble() * 2f, collider: false);
                if (giants % 2 == 0) AddLight(m.transform, new Vector3(0, 1.6f, 0), MushroomGlow[kind], 7f, 1.3f);
                giants++;
            }
            for (int i = 0, n = 0; i < 400 && n < 60; i++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2), r = 26 + (float)rng.NextDouble() * 40;
                var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r + 6);
                if (!Free(p, 0.5f)) continue;
                string kind = n % 4 == 0 ? "Bush" : n % 4 == 1 ? "Flowers" : n % 4 == 2 ? "Mushroom_Cluster" : (n % 8 == 3 ? "Rock_A" : "Rock_B");
                Place(woods, kind, p, rng.Next(360), 0.8f + (float)rng.NextDouble() * 0.8f, collider: kind.StartsWith("Rock"));
                n++;
            }
            // flowers inside town too
            for (int i = 0, n = 0; i < 300 && n < 26; i++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 48 - 24), 0, (float)(rng.NextDouble() * 44 - 14));
                if ((p - Plaza).magnitude < 11 || Mathf.Abs(p.x) < 2.5f || Mathf.Abs(p.z - 8) < 2.5f) continue;
                Place(town, "Flowers", p, rng.Next(360), 0.9f + (float)rng.NextDouble() * 0.5f, collider: false);
                n++;
            }
            // random silly things in the woods
            var witch = new Vector3(-48, 0, 24);
            Q(woods, "FantasyProps/Cauldron", witch, 0, 2.2f);
            AddLight(woods, witch + Vector3.up * 1.6f, new Color(0.5f, 1f, 0.5f), 7f, 1.6f);
            var potionPos = new Vector3(-40, 0, -14);
            Q(woods, "FantasyProps/Potion_1", potionPos, 25, 7f);
            AddLight(woods, potionPos + Vector3.up * 1.5f, new Color(1f, 0.5f, 0.8f), 7f, 1.5f);
            Q(woods, "FantasyProps/Cage_Small", new Vector3(14, 0, 44), 40, 3f);
            Q(woods, "FantasyProps/Coin_Pile", new Vector3(-30, 0, -24), 0, 3f, collider: false);   // treasure!
            Q(woods, "FantasyProps/Dummy", new Vector3(-26, 0, 30), 120, 1.4f);
            Animal(animals, "Fox", new Vector3(-42, 0, 4), 0.55f, 8f, "a fox", new[] { "*stares at you, holding a mushroom*", "yip!" });
            Animal(animals, "Fox", new Vector3(-36, 0, 26), 0.5f, 8f, "another fox", new[] { "*yawns*" });
            Animal(animals, "Deer", new Vector3(12, 0, -38), 1.3f, 9f, "a deer", new[] { "*blinks slowly*" });
            Animal(animals, "Deer", new Vector3(-16, 0, 42), 1.3f, 9f, "a deer", new[] { "*nibbles a glowing mushroom*" });
        }

        // Four plots east of town. HomeBase (cottage + home computer + home zone) appears on the one you buy.
        static void BuildPlots(Transform root, out Transform contents)
        {
            var plots = new GameObject("Plots").transform;
            plots.SetParent(root);
            var area = new GameObject("Zone_the plots").AddComponent<Zone>();
            area.kind = ZoneKind.Home;
            area.displayName = "the plots";
            area.transform.SetParent(root);
            area.transform.SetPositionAndRotation(new Vector3(47, 3, 7), Quaternion.identity);
            area.transform.localScale = new Vector3(30, 8, 34);
            Keep(32, -10, 62, 24);
            PathStrip(plots, new Vector3(33, 0, 6), new Vector3(61, 0, 6), 2.6f);
            var centers = new[] { new Vector3(40, 0, -2), new Vector3(54, 0, -2), new Vector3(40, 0, 15), new Vector3(54, 0, 15) };
            for (int i = 0; i < centers.Length; i++)
            {
                var plotGo = new GameObject("Plot" + (i + 1));
                plotGo.transform.SetParent(plots);
                bool north = centers[i].z > 6;
                plotGo.transform.SetPositionAndRotation(centers[i], Quaternion.Euler(0, north ? 180 : 0, 0));
                var plot = plotGo.AddComponent<Plot>();
                plot.index = i;
                // little fence corners mark it out
                foreach (var c in new[] { new Vector3(-5, 0, -5), new Vector3(5, 0, -5), new Vector3(-5, 0, 5), new Vector3(5, 0, 5) })
                    Place(plotGo.transform, "Fence", c, c.x > 0 ? 0 : 90, 0.8f, collider: false);
                var sale = new GameObject("ForSale").transform;
                sale.SetParent(plotGo.transform, false);
                Place(sale, "Signpost", new Vector3(0, 0, -4.2f), 180, 1f, collider: false);
                sale.localPosition = Vector3.zero;
                var sign = sale.gameObject.AddComponent<BuildingSign>();
                sign.title = $"plot #{i + 1} for sale";
                sign.message = "100 coins at the land office in town";
                sign.range = 2.5f;
                plot.forSaleSign = sale.gameObject;
            }
            var welcome = new GameObject("Sign_plots").AddComponent<BuildingSign>();
            welcome.transform.SetParent(plots);
            welcome.transform.position = new Vector3(33.5f, 0, 8.5f);
            welcome.title = "the plots";
            welcome.message = "build your dream base here! plots are 100 coins at the land office. (no safe zone past this sign)";
            Place(plots, "Signpost", new Vector3(33.5f, 0, 9.2f), 90, 1f, collider: false);
            Animal(plots, "Alpaca", new Vector3(47, 0, 22), 1.4f, 4f, "Professor Fluff", new[] { "*chews thoughtfully*", "*has opinions about your fence*" });
            Animal(plots, "Husky", new Vector3(46, 0, -9), 0.6f, 5f, "Pancake", new[] { "awoo!", "*rolls over*" });

            // the home that appears on your plot (built at the origin; HomeBase moves it onto the plot)
            var homeRoot = new GameObject("HomeBase");
            homeRoot.transform.SetParent(root);
            var home = homeRoot.AddComponent<HomeBase>();
            var c0 = new GameObject("YourHome").transform;
            c0.SetParent(homeRoot.transform);
            home.contents = c0.gameObject;
            Building(c0, "Cottage", new Vector3(0, 0, 2f), 0, 6, 6, 1,
                     new Style { wall = new Color(1f, 0.85f, 0.95f), roof = purpleRoofHome, woodGridSides = true }, chimney: true);
            BuildHomeComputer(c0, new Vector3(-1.8f, 0, -2.6f), 0);   // just outside the door, screen facing out
            var homeZone = new GameObject("Zone_home").AddComponent<Zone>();
            homeZone.kind = ZoneKind.Home;
            homeZone.displayName = "your home";
            homeZone.transform.SetParent(c0);
            homeZone.transform.localPosition = new Vector3(0, 3, 0);
            homeZone.transform.localScale = new Vector3(11, 8, 11);
            contents = c0;
            c0.gameObject.SetActive(false);
        }

        static readonly Color purpleRoofHome = new Color(0.8f, 0.6f, 1f);
    }
}
