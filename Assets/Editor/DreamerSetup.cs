using System;
using System.Collections.Generic;
using System.IO;
using DreamWorld.CharacterCreation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DreamWorld.EditorTools
{
    // dreamWorld > Rebuild Dreamer: run after ArtSource/build_dreamer.py regenerates the model.
    // Rebuilds the "Character" object in the open scene from Dreamer.fbx + Dreamer.manifest.json:
    // PSX materials per texture, two-tone TintMasks, creator slots, palettes and default outfit.
    public static class DreamerSetup
    {
        const string ModelDir = "Assets/Models/Dreamer";
        const string MaterialDir = "Assets/Materials/Dreamer";

        [Serializable] class Submesh { public string texture; public bool tint; }
        [Serializable] class Piece { public string name; public Submesh[] submeshes; }
        [Serializable] class Manifest { public Piece[] pieces; public string[] patterns; }

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static readonly Color[] Skins = {
            C(1.00f, 0.87f, 0.77f), C(0.96f, 0.78f, 0.65f), C(0.84f, 0.64f, 0.49f), C(0.65f, 0.45f, 0.32f),
            C(0.45f, 0.30f, 0.20f), C(0.78f, 0.90f, 0.76f), C(0.82f, 0.76f, 0.96f), C(0.70f, 0.82f, 0.98f) };
        static readonly Color[] Irises = {
            C(0.45f, 0.55f, 1.00f), C(0.70f, 0.50f, 1.00f), C(0.45f, 0.85f, 0.60f), C(1.00f, 0.75f, 0.35f),
            C(1.00f, 0.55f, 0.75f), C(0.90f, 0.30f, 0.35f), C(0.60f, 0.40f, 0.30f), C(0.70f, 0.72f, 0.80f), C(0.35f, 0.85f, 0.85f) };
        static readonly Color[] HairColors = {
            C(0.15f, 0.12f, 0.14f), C(0.40f, 0.26f, 0.18f), C(0.95f, 0.82f, 0.50f), C(0.85f, 0.45f, 0.25f),
            C(0.96f, 0.66f, 0.76f), C(0.72f, 0.62f, 0.90f), C(0.60f, 0.88f, 0.80f), C(0.30f, 0.35f, 0.70f),
            C(0.92f, 0.92f, 0.96f), C(0.25f, 0.55f, 0.45f), C(0.38f, 0.26f, 0.52f), C(0.62f, 0.42f, 0.58f),
            C(0.98f, 0.95f, 0.86f), C(0.98f, 0.88f, 0.62f) };   // platinum (whitish blonde), light blonde
        static readonly Color[] Clothes = {
            C(0.97f, 0.96f, 0.94f), C(0.96f, 0.66f, 0.76f), C(0.72f, 0.62f, 0.90f), C(0.25f, 0.30f, 0.60f),
            C(0.50f, 0.65f, 0.45f), C(0.98f, 0.94f, 0.85f), C(0.60f, 0.88f, 0.80f), C(0.95f, 0.50f, 0.45f),
            C(0.80f, 0.20f, 0.25f), C(0.95f, 0.85f, 0.45f), C(0.45f, 0.30f, 0.22f), C(0.15f, 0.13f, 0.18f),
            // twilight: midnight indigo, dusk purple, mauve, twilight rose, sunset peach, dusk orange, lilac gray, deep teal
            C(0.16f, 0.14f, 0.36f), C(0.38f, 0.26f, 0.52f), C(0.62f, 0.42f, 0.58f), C(0.85f, 0.52f, 0.62f),
            C(0.98f, 0.70f, 0.55f), C(0.90f, 0.48f, 0.32f), C(0.62f, 0.60f, 0.72f), C(0.15f, 0.32f, 0.40f),
            // denim washes: light, mid, dark, black
            C(0.62f, 0.72f, 0.86f), C(0.36f, 0.46f, 0.68f), C(0.18f, 0.22f, 0.38f), C(0.22f, 0.22f, 0.25f) };
        // Extras: blush pinks, freckle/mole browns, ink, metals, pastels. ExtraStart picks each style's first color.
        static readonly Color[] ExtraColors = {
            C(0.97f, 0.60f, 0.68f), C(0.90f, 0.42f, 0.52f), C(1.00f, 0.78f, 0.66f), C(0.62f, 0.42f, 0.32f),
            C(0.30f, 0.20f, 0.18f), C(0.15f, 0.13f, 0.18f), C(0.86f, 0.86f, 0.92f), C(1.00f, 0.82f, 0.35f),
            C(0.72f, 0.62f, 0.90f), C(0.55f, 0.75f, 1.00f), C(0.62f, 0.45f, 0.58f), C(1.00f, 1.00f, 1.00f) };
        static readonly Dictionary<string, int> ExtraStart = new Dictionary<string, int>
        {
            { "Blush", 0 }, { "BlushLines", 1 }, { "HeartBlush", 0 }, { "Freckles", 3 }, { "Mole", 4 }, { "Sparkles", 7 },
            { "UnderEye", 10 }, { "TearTattoo", 5 }, { "Bandaid", 2 }, { "NoseStud", 6 }, { "NoseRing", 6 },
            { "Septum", 6 }, { "LipRing", 6 }, { "BrowPiercing", 6 },
        };
        // Which styles get the "pattern" option (pieces with their own print/plaid/denim are left alone).
        static readonly Dictionary<OutfitCategory, string[]> PatternBases = new Dictionary<OutfitCategory, string[]>
        {
            { OutfitCategory.Top, new[] { "Tee", "LongSleeve", "Tank", "Crop", "Sweater", "Hoodie", "Oversized", "Cardigan", "Sailor", "SlipDress", "ButtonUp" } },
            { OutfitCategory.Bottom, new[] { "Skirt", "Pleated", "RuffleSkirt", "LongSkirt", "Cargo", "Slacks" } },
            { OutfitCategory.Legwear, new[] { "KneeSocks", "ThighHighs", "Tights", "FrillySocks", "LegWarmers" } },
        };

        static readonly Color[] Accessories = {
            C(0.15f, 0.13f, 0.18f), C(0.97f, 0.96f, 0.94f), C(1.00f, 0.82f, 0.35f), C(0.96f, 0.66f, 0.76f),
            C(0.80f, 0.20f, 0.25f), C(0.72f, 0.62f, 0.90f), C(0.60f, 0.88f, 0.80f) };

        [MenuItem("dreamWorld/Rebuild Dreamer")]
        public static void Rebuild()
        {
            AssetDatabase.Refresh();
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ModelDir + "/Dreamer.manifest.json"));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "/Dreamer.fbx");
            if (!AssetDatabase.IsValidFolder(MaterialDir)) AssetDatabase.CreateFolder("Assets/Materials", "Dreamer");

            var root = GameObject.Find("Character");
            var appearance = root.GetComponent<CharacterAppearance>();
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            root.transform.rotation = Quaternion.Euler(0, 180, 0);   // model faces +Z; the camera looks at it from -Z

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var styles = new Dictionary<OutfitCategory, List<GameObject>>();
            foreach (OutfitCategory c in Enum.GetValues(typeof(OutfitCategory))) styles[c] = new List<GameObject>();

            foreach (var piece in manifest.pieces)
            {
                var t = model.transform.Find(piece.name);
                if (t == null) { Debug.LogWarning("[Dreamer] missing mesh " + piece.name); continue; }
                var r = t.GetComponent<SkinnedMeshRenderer>();
                var mats = new Material[piece.submeshes.Length];
                var tints = new bool[piece.submeshes.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = PsxMaterial(piece.submeshes[i].texture);
                    tints[i] = piece.submeshes[i].tint;
                }
                // Eyes: layer 4 is the closed-lid blink pose, hidden until EyeBlink swaps it in.
                if (piece.name.StartsWith("Eyes_") && mats.Length >= 5) mats[4] = HiddenMaterial();
                r.sharedMaterials = mats;
                t.gameObject.AddComponent<TintMask>().tintSubmesh = tints;

                if (Enum.TryParse(piece.name.Split('_')[0], out OutfitCategory category))
                    styles[category].Add(t.gameObject);
            }

            appearance.slots = new List<OutfitSlot>
            {
                Slot(OutfitCategory.Skin, styles, Skins, 0, 0),
                Slot(OutfitCategory.Eyes, styles, Irises, 0, 0),
                Slot(OutfitCategory.Nose, styles, new[] { Color.white }, 0, 0),
                Slot(OutfitCategory.Mouth, styles, new[] { Color.white }, 0, 0),
                Slot(OutfitCategory.Hair, styles, HairColors, 0, 0),
                Slot(OutfitCategory.Top, styles, Clothes, Index(styles, "Top_Sweater"), 5),
                Slot(OutfitCategory.Bottom, styles, Clothes, 0, 3, optional: true),
                Slot(OutfitCategory.Legwear, styles, Clothes, 0, 11, optional: true),
                Slot(OutfitCategory.Shoes, styles, Clothes, Index(styles, "Shoes_Loafers"), 10),
                Slot(OutfitCategory.Neck, styles, Accessories, 0, 0, optional: true),
                Slot(OutfitCategory.Glasses, styles, Accessories, 0, 0, optional: true),
                Slot(OutfitCategory.Headwear, styles, Accessories, 0, 2, optional: true),
                Slot(OutfitCategory.Brows, styles, HairColors, 1, 0, optional: true),
                Slot(OutfitCategory.Extra1, styles, ExtraColors, 1, 0, optional: true),   // blush on by default
                Slot(OutfitCategory.Extra2, styles, ExtraColors, 0, 0, optional: true),
                Slot(OutfitCategory.Extra3, styles, ExtraColors, 0, 0, optional: true),
            };
            // movable face pieces: how far each can slide (face-texture uv) and grow
            Movable(appearance, OutfitCategory.Nose, new Vector2(0.08f, 0.08f), new Vector2(0.6f, 1.6f));
            Movable(appearance, OutfitCategory.Mouth, new Vector2(0.10f, 0.08f), new Vector2(0.6f, 1.6f));
            Movable(appearance, OutfitCategory.Brows, new Vector2(0.08f, 0.12f), new Vector2(0.7f, 1.4f));
            foreach (var c in new[] { OutfitCategory.Extra1, OutfitCategory.Extra2, OutfitCategory.Extra3 })
            {
                var extra = Movable(appearance, c, new Vector2(0.40f, 0.40f), new Vector2(0.5f, 1.8f));
                extra.customAllowed = true;
                extra.baseColors = extra.BaseNames.ConvertAll(b => ExtraStart.TryGetValue(b, out var k) ? k : 0);
            }
            appearance.GetSlot(OutfitCategory.Brows).customAllowed = true;
            // option rows: pants rise; hair bangs + length. Defaults: pleated skirt (mid rise), long hair
            var bottom = appearance.GetSlot(OutfitCategory.Bottom);
            bottom.axes = new List<OptionAxis> { new OptionAxis { label = "rise", values = new[] { "High", "Mid", "Low" }, index = 1 } };
            bottom.SelectBase("Pleated");
            // pattern row (plain = the piece's own fabric) on tops, skirts and socks/tights
            foreach (var pair in PatternBases)
                appearance.GetSlot(pair.Key).axes.Add(PatternAxis(manifest.patterns, pair.Value));
            var hair = appearance.GetSlot(OutfitCategory.Hair);
            hair.axes = new List<OptionAxis>
            {
                new OptionAxis { label = "bangs", values = new[] { "Straight", "Choppy", "Middle", "Left", "Right", "Micro" }, index = 1 },
                new OptionAxis { label = "length", values = new[] { "Short", "Medium", "Long" }, index = 1 },
                new OptionAxis { label = "tie", values = new[] { "High", "Low" }, index = 0 },   // twintails high, pigtails low
            };
            hair.SelectBase("Long");
            // full-spectrum color pickers for hair and eyes
            hair.customAllowed = true;
            appearance.GetSlot(OutfitCategory.Eyes).customAllowed = true;
            foreach (var slot in appearance.slots)
                foreach (var style in slot.styles) style.SetActive(style == slot.CurrentStyle);
            EditorUtility.SetDirty(appearance);

            RelaxArms(model.GetComponent<Animator>());
            model.AddComponent<IdleMotion>();
            // No animation controller yet: a running humanoid Animator would snap the hips to the floor.
            model.GetComponent<Animator>().enabled = false;
            var blink = root.GetComponent<EyeBlink>();
            if (blink == null) blink = root.AddComponent<EyeBlink>();
            blink.appearance = appearance;
            blink.hiddenMaterial = HiddenMaterial();
            EditorUtility.SetDirty(blink);
            SetupCreatorScreen();
            PlayerPrefs.DeleteKey("dreamWorld.character");   // old saves point at old slot layouts
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);

            // The world scenes spawn this same character (her saved look loads in Awake).
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Dreamer.prefab");
            Debug.Log($"[Dreamer] rebuilt: {manifest.pieces.Length} pieces");
        }

        // Pixel fonts render crisp with hinted raster (no smoothing); hook them into the creator UI
        // and frame the camera so she stands between the two side panels.
        static void SetupCreatorScreen()
        {
            Font LoadFont(string path)
            {
                var importer = (TrueTypeFontImporter)AssetImporter.GetAtPath(path);
                if (importer != null && importer.fontRenderingMode != FontRenderingMode.HintedRaster)
                {
                    importer.fontRenderingMode = FontRenderingMode.HintedRaster;
                    importer.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Font>(path);
            }

            var ui = UnityEngine.Object.FindAnyObjectByType<CharacterCreatorUI>();
            if (ui != null)
            {
                ui.titleFont = LoadFont("Assets/Fonts/PixelifySans.ttf");
                ui.bodyFont = LoadFont("Assets/Fonts/VT323-Regular.ttf");
                EditorUtility.SetDirty(ui);

                // log in / your dreamers screens in front of the creator
                var flow = UnityEngine.Object.FindAnyObjectByType<DreamWorld.Account.AccountFlow>();
                if (flow == null) flow = new GameObject("AccountFlow").AddComponent<DreamWorld.Account.AccountFlow>();
                flow.creator = ui;
                flow.appearance = ui.appearance;
                flow.titleFont = ui.titleFont;
                flow.bodyFont = ui.bodyFont;
                EditorUtility.SetDirty(flow);
            }
            var cam = Camera.main;
            cam.transform.position = new Vector3(0, 0.84f, -3.7f);
            cam.transform.eulerAngles = Vector3.zero;
            cam.fieldOfView = 40;
            if (cam.GetComponent<CreatorCameraZoom>() == null) cam.gameObject.AddComponent<CreatorCameraZoom>();
        }

        static OutfitSlot Movable(CharacterAppearance appearance, OutfitCategory category, Vector2 move, Vector2 size)
        {
            var slot = appearance.GetSlot(category);
            slot.movable = true;
            slot.moveRange = move;
            slot.sizeRange = size;
            return slot;
        }

        static OptionAxis PatternAxis(string[] patterns, string[] bases)
        {
            var values = new List<string> { "Plain" };
            var textures = new List<Texture2D> { null };
            foreach (var p in patterns)
            {
                values.Add(p);
                textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>($"{ModelDir}/Textures/Pattern{p}.png"));
            }
            return new OptionAxis { label = "pattern", values = values.ToArray(), textures = textures.ToArray(), bases = bases };
        }

        static int Index(Dictionary<OutfitCategory, List<GameObject>> styles, string name)
        {
            var category = (OutfitCategory)Enum.Parse(typeof(OutfitCategory), name.Split('_')[0]);
            return Mathf.Max(0, styles[category].FindIndex(g => g.name == name));
        }

        static OutfitSlot Slot(OutfitCategory c, Dictionary<OutfitCategory, List<GameObject>> styles,
                               Color[] colors, int style, int color, bool optional = false) =>
            new OutfitSlot { category = c, styles = styles[c], colors = colors, styleIndex = style, colorIndex = color, optional = optional };

        // Fully clipped material: used to hide a submesh (the blink layer).
        static Material HiddenMaterial()
        {
            const string path = MaterialDir + "/PSX_Hidden.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("DreamWorld/PSX Lit"));
                mat.SetFloat("_Cutoff", 1.01f);
                mat.SetColor("_Color", new Color(1, 1, 1, 0));
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        static Material PsxMaterial(string texture)
        {
            var path = $"{MaterialDir}/PSX_{texture}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("DreamWorld/PSX Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            // Eyes use the PSX Face shader (eye sliders); nose/mouth/brows/extras the PSX Decal shader (move sliders).
            bool part = texture.StartsWith("Nose") || texture.StartsWith("Mouth") || texture.StartsWith("Brows") || texture.StartsWith("Extra");
            mat.shader = Shader.Find(texture.StartsWith("Eyes") ? "DreamWorld/PSX Face" : part ? "DreamWorld/PSX Decal" : "DreamWorld/PSX Lit");
            var texPath = $"{ModelDir}/Textures/{texture}.png";
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (part) FitDecal(mat, texPath);
            // Face decals and fishnets have see-through pixels; everything else is solid.
            bool decal = texture.StartsWith("Eyes") || part;
            mat.SetFloat("_Cutoff", decal || texture == "Fishnet" ? 0.5f : 0f);
            // Skin and face get gentler shading so the face reads like flat anime coloring.
            mat.SetFloat("_Softness", texture == "Skin" || decal ? 0.7f : 0.15f);
            // clothes and faces only get a hint of the PS1 affine warp (strong warp bends textures as the camera moves)
            mat.SetFloat("_AffineStrength", decal ? 0f : 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Finds where a face piece sits in its texture (the center its size slider scales around) and whether
        // it's a mirrored left/right pair (brows, blush), which the shader then spreads apart instead of sliding.
        static void FitDecal(Material mat, string texPath)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(texPath));
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            bool Opaque(int x, int y) => px[y * w + x].a > 127;
            int filled = 0, matched = 0;
            int x0 = w, x1 = -1, y0 = h, y1 = -1, rx0 = w, rx1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Opaque(x, y)) continue;
                    filled++;
                    if (w - x < w && Opaque(w - x, y)) matched++;   // the art mirrors around column w/2
                    x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x);
                    y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                    if (x >= w / 2) { rx0 = Mathf.Min(rx0, x); rx1 = Mathf.Max(rx1, x); }
                }
            UnityEngine.Object.DestroyImmediate(tex);
            if (filled == 0) return;
            // a pair has pixels on both sides that mostly mirror each other (and isn't one centered piece)
            bool mirror = matched >= filled * 0.8f && rx0 > w / 2 + 2;
            float cy = (y0 + y1 + 1) / 2f / h;
            float cx = mirror ? (rx0 + rx1 + 1) / 2f / w - 0.5f : (x0 + x1 + 1) / 2f / w;
            mat.SetFloat("_Mirror", mirror ? 1 : 0);
            mat.SetVector("_PartCenter", new Vector4(cx, cy, 0, 0));
        }

        // The model is exported in a T-pose; swing the arms down so she stands naturally.
        static void RelaxArms(Animator anim)
        {
            foreach (var (arm, hand) in new[] {
                (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftHand),
                (HumanBodyBones.RightUpperArm, HumanBodyBones.RightHand) })
            {
                var a = anim.GetBoneTransform(arm);
                var h = anim.GetBoneTransform(hand);
                float before = h.position.y;
                a.RotateAround(a.position, anim.transform.forward, 72f);
                if (h.position.y > before) a.RotateAround(a.position, anim.transform.forward, -144f);
            }
        }
    }
}
