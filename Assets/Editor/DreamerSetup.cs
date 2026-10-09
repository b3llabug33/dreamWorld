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
        [Serializable] class Manifest { public Piece[] pieces; }

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
            C(0.92f, 0.92f, 0.96f), C(0.25f, 0.55f, 0.45f), C(0.38f, 0.26f, 0.52f), C(0.62f, 0.42f, 0.58f) };
        static readonly Color[] Clothes = {
            C(0.97f, 0.96f, 0.94f), C(0.96f, 0.66f, 0.76f), C(0.72f, 0.62f, 0.90f), C(0.25f, 0.30f, 0.60f),
            C(0.50f, 0.65f, 0.45f), C(0.98f, 0.94f, 0.85f), C(0.60f, 0.88f, 0.80f), C(0.95f, 0.50f, 0.45f),
            C(0.80f, 0.20f, 0.25f), C(0.95f, 0.85f, 0.45f), C(0.45f, 0.30f, 0.22f), C(0.15f, 0.13f, 0.18f),
            // twilight: midnight indigo, dusk purple, mauve, twilight rose, sunset peach, dusk orange, lilac gray, deep teal
            C(0.16f, 0.14f, 0.36f), C(0.38f, 0.26f, 0.52f), C(0.62f, 0.42f, 0.58f), C(0.85f, 0.52f, 0.62f),
            C(0.98f, 0.70f, 0.55f), C(0.90f, 0.48f, 0.32f), C(0.62f, 0.60f, 0.72f), C(0.15f, 0.32f, 0.40f),
            // denim washes: light, mid, dark, black
            C(0.62f, 0.72f, 0.86f), C(0.36f, 0.46f, 0.68f), C(0.18f, 0.22f, 0.38f), C(0.22f, 0.22f, 0.25f) };
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
                r.sharedMaterials = mats;
                t.gameObject.AddComponent<TintMask>().tintSubmesh = tints;

                if (Enum.TryParse(piece.name.Split('_')[0], out OutfitCategory category))
                    styles[category].Add(t.gameObject);
            }

            appearance.slots = new List<OutfitSlot>
            {
                Slot(OutfitCategory.Skin, styles, Skins, 0, 0),
                Slot(OutfitCategory.Eyes, styles, Irises, 0, 0),
                Slot(OutfitCategory.Hair, styles, HairColors, 0, 0),
                Slot(OutfitCategory.Top, styles, Clothes, Index(styles, "Top_Sweater"), 5),
                Slot(OutfitCategory.Bottom, styles, Clothes, Index(styles, "Bottom_Pleated") + 1, 3, optional: true),
                Slot(OutfitCategory.Legwear, styles, Clothes, 0, 11, optional: true),
                Slot(OutfitCategory.Shoes, styles, Clothes, Index(styles, "Shoes_Loafers"), 10),
                Slot(OutfitCategory.Neck, styles, Accessories, 0, 0, optional: true),
                Slot(OutfitCategory.Glasses, styles, Accessories, 0, 0, optional: true),
                Slot(OutfitCategory.Headwear, styles, Accessories, 0, 2, optional: true),
            };
            foreach (var slot in appearance.slots)
                foreach (var style in slot.styles) style.SetActive(style == slot.CurrentStyle);
            EditorUtility.SetDirty(appearance);

            RelaxArms(model.GetComponent<Animator>());
            SetupCreatorScreen();
            PlayerPrefs.DeleteKey("dreamWorld.character");   // old saves point at old slot layouts
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
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
            }
            var cam = Camera.main;
            cam.transform.position = new Vector3(0, 0.84f, -3.7f);
            cam.transform.eulerAngles = Vector3.zero;
            cam.fieldOfView = 40;
            if (cam.GetComponent<CreatorCameraZoom>() == null) cam.gameObject.AddComponent<CreatorCameraZoom>();
        }

        static int Index(Dictionary<OutfitCategory, List<GameObject>> styles, string name)
        {
            var category = (OutfitCategory)Enum.Parse(typeof(OutfitCategory), name.Split('_')[0]);
            return Mathf.Max(0, styles[category].FindIndex(g => g.name == name));
        }

        static OutfitSlot Slot(OutfitCategory c, Dictionary<OutfitCategory, List<GameObject>> styles,
                               Color[] colors, int style, int color, bool optional = false) =>
            new OutfitSlot { category = c, styles = styles[c], colors = colors, styleIndex = style, colorIndex = color, optional = optional };

        static Material PsxMaterial(string texture)
        {
            var path = $"{MaterialDir}/PSX_{texture}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("DreamWorld/PSX Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ModelDir}/Textures/{texture}.png");
            // Eye atlases and fishnets have see-through pixels; everything else is solid.
            mat.SetFloat("_Cutoff", texture.StartsWith("Eyes") || texture == "Fishnet" ? 0.5f : 0f);
            // Skin and face get gentler shading so the face reads like flat anime coloring.
            mat.SetFloat("_Softness", texture == "Skin" || texture.StartsWith("Eyes") ? 0.7f : 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
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
