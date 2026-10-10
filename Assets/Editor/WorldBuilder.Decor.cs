using System.Collections.Generic;
using System.Linq;
using DreamWorld.Town;
using UnityEditor;
using UnityEngine;

namespace DreamWorld.EditorTools
{
    // The furniture you can place at home (Town/HomeDecor.cs): one prefab per decor recipe in Resources/Decor,
    // each with a box collider, its wood-colored materials listed (so an oak bench and a red cedar bench differ),
    // and a light for the glowing ones.
    public static partial class WorldBuilder
    {
        const string DecorDir = "Assets/Resources/Decor";

        // recipe id -> (model: "Props/<name>" code-built or "Q:<Quaternius piece>", scale, wood strength, light)
        static readonly Dictionary<string, (string model, float scale, float wood, Color? light)> DecorModels =
            new Dictionary<string, (string, float, float, Color?)>
        {
            { "decor_stool", ("Props/MushroomStool", 1f, 0.55f, null) },
            { "decor_table", ("Q:FantasyProps/Table_Large", 0.6f, 1f, null) },
            { "decor_bookshelf", ("Props/Bookshelf", 1f, 0.6f, null) },
            { "decor_bench", ("Q:FantasyProps/Bench", 0.7f, 1f, null) },
            { "decor_planter", ("Props/FlowerWindowBox", 1f, 0.5f, null) },
            { "decor_crystal_lamp", ("Props/MoonflowerLamp", 1f, 0.6f, new Color(0.8f, 0.65f, 1f)) },
            { "decor_stone_lantern", ("Props/FireflyLantern", 1.6f, 0f, new Color(1f, 0.85f, 0.55f)) },
            { "decor_rug", ("Props/RoundRug", 1f, 0f, null) },
        };

        static void BuildDecorPrefabs()
        {
            EnsureFolder(DecorDir);
            var holder = new GameObject("DecorBuild").transform;
            foreach (var r in DreamWorld.Gathering.Crafting.Recipes.Where(r => r.kind == DreamWorld.Gameplay.ItemKind.Decor))
            {
                if (!DecorModels.TryGetValue(r.id, out var spec)) { Debug.LogWarning("[Decor] no model for " + r.id); continue; }
                var root = new GameObject(r.id);
                root.transform.SetParent(holder, false);
                GameObject model;
                if (spec.model.StartsWith("Q:")) model = Q(root.transform, spec.model.Substring(2), Vector3.zero, 0, spec.scale, collider: false);
                else
                {
                    var parts = spec.model.Split('/');
                    model = Put(root.transform, parts[0], parts[1], Vector3.zero, 0, spec.scale, "none");
                }
                // sit it on the ground at its pivot, centered
                var b = LocalBounds(root.transform);
                model.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
                b = LocalBounds(root.transform);
                var box = root.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = new Vector3(Mathf.Max(b.size.x, 0.1f), Mathf.Max(b.size.y, 0.04f), Mathf.Max(b.size.z, 0.1f));
                foreach (var rend in root.GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var piece = root.AddComponent<DecorPiece>();
                piece.woodStrength = spec.wood;
                var rs = new List<Renderer>();
                var slots = new List<int>();
                if (spec.wood > 0f)
                    foreach (var rend in root.GetComponentsInChildren<Renderer>())
                    {
                        var mats = rend.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                            if (mats[i] != null && (mats[i].name.StartsWith("VC_Matte") || mats[i].name.Contains("Furniture")))
                            {
                                rs.Add(rend);
                                slots.Add(i);
                            }
                    }
                piece.woodRenderers = rs.ToArray();
                piece.woodSlots = slots.ToArray();
                if (spec.light.HasValue) AddLight(root.transform, new Vector3(0, b.size.y * 0.8f, 0), spec.light.Value, 3.5f, 1f);
                PrefabUtility.SaveAsPrefabAsset(root, $"{DecorDir}/{r.id}.prefab");
            }
            Object.DestroyImmediate(holder.gameObject);
        }
    }
}
