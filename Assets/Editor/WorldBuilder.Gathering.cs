using System.Collections.Generic;
using System.Linq;
using DreamWorld.Gathering;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace DreamWorld.EditorTools
{
    // Gathering: about half the woods trees can be chopped (cone trees give pine, round ones oak, birches birch),
    // mossy rocks give stone, and ore veins, cotton bushes, cocoons, webs, berry bushes, mushroom patches, rock
    // piles, rune stones and fallen logs are scattered through the woods. Mistpine Hollow has its own woods
    // (mistpine fir, red cedar logs, mossy maples), silver and dream crystal in the quarry. Workbenches in the
    // town square and by the Mullens' house. Runs after the ground and Mistpine are built.
    public static partial class WorldBuilder
    {
        const string ResDir = "Resources";   // Assets/Models/Resources (code-built, export_unity.py)

        static void BuildGathering(Transform root, Random rng)
        {
            Physics.SyncTransforms();
            var g = new GameObject("Gathering").transform;
            g.SetParent(root);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int nodes = 0;

            // ---- the woods' own trees and rocks become gatherable
            var packs = root.Find("NaturePacks");
            var stumps = new GameObject("Stumps").transform;
            stumps.SetParent(g);
            if (packs != null)
            {
                var owls = packs.Cast<Transform>().Where(t => t.name.StartsWith("AC_Owl")).Select(t => t.position).ToList();
                foreach (Transform t in packs.Cast<Transform>().ToList())
                {
                    string n = t.name;
                    float fromTown = new Vector2(t.position.x, t.position.z - 6).magnitude;
                    if (fromTown < 26f) continue;   // the trees around the square stay put
                    string wood = n.StartsWith("RN_Tree7") || n.StartsWith("RN_Tree8") ? "pine"
                                : n.StartsWith("AC_Birch") ? "birch"
                                : n.StartsWith("RN_Tree") ? "oak" : null;
                    if (wood != null && rng.NextDouble() < 0.6)
                    {
                        TreeNode(t.gameObject, wood, Stump(stumps, wood, t.position, R(0, 360)));
                        nodes++;
                    }
                    else if (n.StartsWith("AC_Rock") && t.localScale.y < 1.6f && !owls.Any(o => Flat(o - t.position).magnitude < 1.5f) && rng.NextDouble() < 0.7)
                    {
                        Node(t.gameObject, "stone", "break", "mossy rock", hits: 4, perHit: new Vector2Int(1, 2), regrow: 200f, range: 2.4f);
                        nodes++;
                    }
                }
            }

            // ---- scattered nodes in the woods around town
            var wild = new GameObject("WoodsNodes").transform;
            wild.SetParent(g);
            Vector3? Spot(float minR, float maxR, float clearance)
            {
                for (int i = 0; i < 80; i++)
                {
                    float a = R(0, Mathf.PI * 2), r = R(minR, maxR);
                    var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r + 6);
                    if (!Free(p, 2f) || NearHome(p, 3f) || InWater(p.x, p.z, 2f)) continue;
                    p.y = GroundY(p.x, p.z, GroundHeight(p.x, p.z));
                    if (Physics.CheckSphere(p + Vector3.up * (clearance + 0.25f), clearance, ~0, QueryTriggerInteraction.Ignore)) continue;
                    return p;
                }
                return null;
            }
            void Scatter(int count, float minR, float maxR, float clearance, System.Func<Vector3, GameObject> make)
            {
                for (int i = 0; i < count; i++)
                {
                    var p = Spot(minR, maxR, clearance);
                    if (p == null) continue;
                    make(p.Value);
                    nodes++;
                    Physics.SyncTransforms();
                }
            }
            Scatter(10, 30, 76, 0.9f, p => Ore(wild, "Ore_Copper", p, R(0, 360), "copper_ore", "copper vein", null));
            Scatter(5, 50, 78, 0.9f, p => Ore(wild, "Ore_Silver", p, R(0, 360), "silver_ore", "silver vein", "pickaxe"));
            Scatter(3, 60, 80, 0.9f, p => Ore(wild, "Ore_DreamCrystal", p, R(0, 360), "dream_crystal", "dream crystal", "pickaxe"));
            Scatter(10, 27, 70, 0.7f, p => Res(wild, "Thread_CottonBush", p, R(0, 360), "thread", "pick", "cotton bush", 3, new Vector2Int(1, 2), 150f));
            Scatter(5, 35, 76, 0.6f, p => Res(wild, "Thread_Cocoons", p, R(0, 360), "thread", "gather", "silk cocoons", 2, new Vector2Int(2, 3), 220f));
            Scatter(4, 40, 78, 0.6f, p => Res(wild, "Thread_Web", p, R(0, 360), "thread", "gather", "spider web", 2, new Vector2Int(1, 3), 200f, collider: "none"));
            Scatter(12, 26, 72, 0.7f, p => Res(wild, "Food_BerryBush", p, R(0, 360), "berries", "pick", "berry bush", 3, new Vector2Int(1, 3), 120f));
            Scatter(8, 28, 76, 0.5f, p => Res(wild, "Food_MushroomPatch", p, R(0, 360), "forest_mushroom", "pick", "mushroom patch", 2, new Vector2Int(1, 2), 160f, collider: "none"));
            Scatter(8, 28, 76, 0.8f, p => Res(wild, "Rock_Pile", p, R(0, 360), "stone", "break", "rock pile", 3, new Vector2Int(2, 3), 180f));
            Scatter(3, 45, 78, 1f, p =>
            {
                var go = Res(wild, "Rock_RuneStone", p, R(0, 360), "stone", "break", "rune stone", 4, new Vector2Int(1, 2), 300f);
                var node = go.GetComponent<ResourceNode>();
                node.bonusItem = "dream_crystal";
                node.bonusChance = 0.2f;
                return go;
            });
            Scatter(6, 28, 76, 1.2f, p => Res(wild, "Log_Fallen", p, R(0, 360), "wood_oak", "chop", "fallen oak log", 3, new Vector2Int(2, 3), 240f, boost: "stone_axe"));

            // ---- Mistpine Hollow: its own woods, the quarry's silver and crystal
            var mist = root.Find("MistpineHollow");
            if (mist != null) nodes += MistpineGathering(mist, rng);

            // ---- workbenches
            Workbench(root.Find("Town") ?? root);
            if (mist != null)
            {
                var bench = Q(mist, "FantasyProps/Workbench", Vector3.zero, 0);
                bench.name = "Workbench_mistpine";
                var p = MW(-26, -1);
                p.y = GroundY(p.x, p.z, 0f);
                bench.transform.position = p;
                bench.transform.rotation = Quaternion.Euler(0, FaceYaw(p, MW(-30, 10)) + 180f, 0) * bench.transform.rotation;
                AddBench(bench, p);
            }
            Debug.Log($"[Gathering] {nodes} gathering spots");
        }

        // ---------------------------------------------------------------- Mistpine

        static int MistpineGathering(Transform mist, Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int nodes = 0;
            var g = new GameObject("Gathering").transform;
            g.SetParent(mist);
            var walk = ((List<object>)MiniJson(System.IO.File.ReadAllText($"{MistDir}/mistpine_layout.json"))["path"])
                .Select(p => ((List<object>)p).Select(System.Convert.ToSingle).ToList()).Select(p => MW(p[0], p[1])).ToList();
            var house = MW(-30, 10);

            // the pack trees in Mistpine: cone trees are mistpine firs, birches stay birch
            var dressing = mist.Find("Dressing");
            if (dressing != null)
                foreach (Transform t in dressing.Cast<Transform>().ToList())
                {
                    string wood = t.name.StartsWith("RN_Tree8") ? "fir" : t.name.StartsWith("AC_Birch") ? "birch" : null;
                    if (wood == null || Flat(t.position - house).magnitude < 22f || rng.NextDouble() > 0.7) continue;
                    TreeNode(t.gameObject, wood, Stump(g, wood, t.position, R(0, 360)));
                    nodes++;
                }

            // somewhere on Mistpine's ground near a layout point (Blender coords), off the path and clear of trunks
            Vector3? Spot(float bx, float by, float spread, float clearance)
            {
                for (int i = 0; i < 80; i++)
                {
                    var p = MW(bx + R(-spread, spread), by + R(-spread, spread));
                    if (Flat(p - MistOrigin).magnitude > 72f || Flat(p - house).magnitude < 20f) continue;
                    if (walk.Count > 1 && DistToPolyline(walk, p.x, p.z, out _) < 4.5f) continue;
                    if (!MistGroundY(p.x, p.z, out p.y)) continue;
                    if (Physics.CheckSphere(p + Vector3.up * (clearance + 0.25f), clearance, ~0, QueryTriggerInteraction.Ignore)) continue;
                    return p;
                }
                return null;
            }
            void Scatter(int count, float bx, float by, float spread, float clearance, System.Func<Vector3, GameObject> make)
            {
                for (int i = 0; i < count; i++)
                {
                    var p = Spot(bx, by, spread, clearance);
                    if (p == null) continue;
                    make(p.Value);
                    nodes++;
                    Physics.SyncTransforms();
                }
            }
            var fir = TintedVC("Fir", new Color(0.62f, 0.66f, 0.7f));
            var moss = TintedVC("MossyMaple", new Color(0.78f, 0.95f, 0.72f));
            var cedar = TintedVC("Cedar", new Color(1f, 0.72f, 0.62f));
            // more firs and mossy maples through the woods, red cedar logs on the ground
            Scatter(8, 0, 0, 60, 1.2f, p => Tint(TreeRes(g, "Tree_Pine", p, R(0, 360), "fir", "mistpine fir"), fir));
            Scatter(8, -10, -20, 55, 1.2f, p => Tint(TreeRes(g, "Tree_Oak", p, R(0, 360), "maple", "mossy maple"), moss));
            Scatter(7, 10, 20, 55, 1.2f, p => Tint(Res(g, "Log_Fallen", p, R(0, 360), "wood_cedar", "chop", "red cedar log", 3, new Vector2Int(2, 3), 260f, boost: "stone_axe"), cedar));
            // the quarry: silver, dream crystal, copper, rock piles
            Scatter(4, 30, -60, 9, 0.9f, p => Ore(g, "Ore_Silver", p, R(0, 360), "silver_ore", "silver vein", "pickaxe"));
            Scatter(3, 30, -60, 10, 0.9f, p => Ore(g, "Ore_DreamCrystal", p, R(0, 360), "dream_crystal", "dream crystal", "pickaxe"));
            Scatter(3, 30, -60, 11, 0.9f, p => Ore(g, "Ore_Copper", p, R(0, 360), "copper_ore", "copper vein", null));
            Scatter(3, 30, -55, 12, 0.8f, p => Res(g, "Rock_Pile", p, R(0, 360), "stone", "break", "rock pile", 3, new Vector2Int(2, 3), 180f));
            // the damp woods: mushrooms, webs, cocoons, a few berry bushes
            Scatter(6, 0, 0, 60, 0.5f, p => Res(g, "Food_MushroomPatch", p, R(0, 360), "forest_mushroom", "pick", "mushroom patch", 2, new Vector2Int(1, 3), 160f, collider: "none"));
            Scatter(5, 0, 0, 60, 0.6f, p => Res(g, "Thread_Web", p, R(0, 360), "thread", "gather", "spider web", 2, new Vector2Int(1, 3), 200f, collider: "none"));
            Scatter(3, 0, 0, 60, 0.6f, p => Res(g, "Thread_Cocoons", p, R(0, 360), "thread", "gather", "silk cocoons", 2, new Vector2Int(2, 3), 220f));
            Scatter(4, -45, -45, 14, 0.7f, p => Res(g, "Food_BerryBush", p, R(0, 360), "berries", "pick", "berry bush", 3, new Vector2Int(1, 3), 120f));
            return nodes;
        }

        // Height of Mistpine's own ground (or the quarry floor) under x, z.
        static bool MistGroundY(float x, float z, out float y)
        {
            y = 0;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, 40f, z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                var n = hit.collider.transform;
                while (n != null && !n.name.StartsWith("Mist_")) n = n.parent;
                if (n == null) return false;              // something else (a trunk, a rock, the house) is in the way
                if (n.name != "Mist_Ground" && n.name != "Mist_Quarry") return false;
                y = hit.point.y;
                return true;
            }
            return false;
        }

        // Height of the hollow's terrain mesh under x, z (the formula's value if nothing's hit).
        static float GroundY(float x, float z, float fallback)
        {
            foreach (var hit in Physics.RaycastAll(new Vector3(x, 40f, z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.name == "Ground" || hit.collider.name.StartsWith("Mist_")) return hit.point.y;
            return fallback;
        }

        // ---------------------------------------------------------------- node helpers

        static ResourceNode Node(GameObject go, string item, string verb, string label, int hits, Vector2Int perHit, float regrow,
                                 float range = 2.3f, float stamina = 6f, string needs = null, string boost = null)
        {
            var node = go.AddComponent<ResourceNode>();
            node.item = item;
            node.verb = verb;
            node.label = label;
            node.hits = hits;
            node.perHit = perHit;
            node.regrowSeconds = regrow;
            node.range = range;
            node.stamina = stamina;
            node.needsTool = needs;
            node.boostTool = boost;
            return node;
        }

        // A pack tree as a wood node; chopped down it shows the stump.
        static void TreeNode(GameObject tree, string wood, GameObject stump)
        {
            var def = DreamWorld.Gameplay.Items.GetWood(wood);
            var node = Node(tree, "wood_" + wood, "chop", (def?.name ?? wood) + " tree", 4, new Vector2Int(1, 2), 300f, range: 2.6f, stamina: 8f, boost: "stone_axe");
            node.used = stump;
            stump.SetActive(false);
        }

        static GameObject Stump(Transform parent, string wood, Vector3 pos, float yaw)
        {
            string model = wood == "birch" ? "Tree_Birch_Used" : wood == "pine" || wood == "fir" ? "Tree_Pine_Used" : "Tree_Oak_Used";
            var s = Put(parent, ResDir, model, pos, yaw, 1f, "box", world: true);
            s.name = "Stump_" + wood;
            return s;
        }

        // A code-built resource model as a node, with its used-up version (if there's one) hidden beside it.
        static GameObject Res(Transform parent, string model, Vector3 pos, float yaw, string item, string verb, string label, int hits,
                              Vector2Int perHit, float regrow, string needs = null, string boost = null, string collider = "box")
        {
            var go = Put(parent, ResDir, model, pos, yaw, 1f, collider, world: true);
            var node = Node(go, item, verb, label, hits, perHit, regrow, needs: needs, boost: boost);
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/{ResDir}/{model}_Used.fbx") != null)
            {
                var used = Put(parent, ResDir, model + "_Used", pos, yaw, 1f, collider == "none" ? "none" : "box", world: true);
                used.transform.SetParent(go.transform, true);
                node.used = used;
                used.SetActive(false);
            }
            return go;
        }

        static GameObject Ore(Transform parent, string model, Vector3 pos, float yaw, string item, string label, string needs)
        {
            var go = Put(parent, ResDir, model, pos, yaw, 1f, "box", world: true);
            var node = Node(go, item, "mine", label, item == "dream_crystal" ? 2 : 3, new Vector2Int(1, item == "copper_ore" ? 2 : 1),
                            item == "dream_crystal" ? 600f : 300f, range: 2.4f, stamina: 9f, needs: needs);
            var used = Put(parent, ResDir, "Ore_Used", pos, yaw, 1f, "box", world: true);
            used.transform.SetParent(go.transform, true);
            node.used = used;
            used.SetActive(false);
            if (item == "dream_crystal") AddLight(go.transform, new Vector3(0, 0.8f, 0), new Color(0.75f, 0.6f, 1f), 4f, 1f);
            return go;
        }

        // A code-built tree (Mistpine's firs and maples): wood, then its stump.
        static GameObject TreeRes(Transform parent, string model, Vector3 pos, float yaw, string wood, string label)
            => Res(parent, model, pos, yaw, "wood_" + wood, "chop", label, 4, new Vector2Int(1, 2), 300f, boost: "stone_axe");

        // A copy of the vertex-color material with a tint (VC_Matte_Fir...), saved so the scene keeps it.
        static Material TintedVC(string name, Color tint)
        {
            var path = $"{VCDir}/VC_Matte_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(VC("Matte"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.CopyPropertiesFromMaterial(VC("Matte"));
            mat.color = tint;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject Tint(GameObject go, Material mat)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = r.sharedMaterials.Select(m => m != null && m.name == "VC_Matte" ? mat : m).ToArray();
            return go;
        }

        // ---------------------------------------------------------------- workbenches

        // In the square, on the paving near the edge, in the first clear spot between the cross paths.
        static void Workbench(Transform town)
        {
            foreach (var a in new[] { 105f, 285f, 165f, 345f, 75f, 195f })   // between the planters (on the diagonals) and the cross paths
            {
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                var p = Plaza + dir * (PlazaRadius - 3.5f);
                if (Physics.CheckBox(p + Vector3.up * 1.1f, new Vector3(1.6f, 0.9f, 1.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                var bench = Q(town, "FantasyProps/Workbench", Vector3.zero, 0);
                bench.name = "Workbench";
                bench.transform.position = p + Vector3.up * 0.035f;
                // its working side (+z of the model... measured in play: the tools face -z) toward the fountain
                bench.transform.rotation = Quaternion.Euler(0, FaceYaw(p, Plaza), 0) * bench.transform.rotation;
                AddBench(bench, p);
                Physics.SyncTransforms();
                return;
            }
            Debug.LogWarning("[Gathering] no room for the town workbench");
        }

        static void AddBench(GameObject bench, Vector3 p)
        {
            var c = new GameObject("CraftingBench").AddComponent<CraftingBench>();
            c.transform.SetParent(bench.transform.parent);
            c.transform.position = p;
            c.range = 2.8f;
        }
    }
}
