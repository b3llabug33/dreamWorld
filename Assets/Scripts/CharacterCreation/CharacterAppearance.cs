using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DreamWorld.CharacterCreation
{
    public enum OutfitCategory { Skin, Eyes, Nose, Mouth, Hair, Top, Bottom, Legwear, Shoes, Neck, Glasses, Headwear }

    // One customizable part of the character. Each entry in `styles` is a child mesh of the
    // Dreamer model; only the selected one is shown. Optional slots get a "none" choice first.
    [Serializable]
    public class OutfitSlot
    {
        public OutfitCategory category;
        public List<GameObject> styles = new List<GameObject>();
        public Color[] colors = { Color.white };
        public bool optional;
        public int styleIndex;
        public int colorIndex;

        // Optional per-style variants, e.g. pants rises {"High","Mid","Low"}: meshes are named
        // Bottom_BaggyJeans_High etc. The style list shows each base style once; the variant is
        // picked separately. Styles without variant meshes (skirts) just ignore it.
        public string[] variants = new string[0];
        public int variantIndex;

        // "Bottom_BaggyJeans_High" -> "BaggyJeans"
        public string BaseName(GameObject style)
        {
            var name = style.name.Substring(style.name.IndexOf('_') + 1);
            foreach (var v in variants)
                if (name.EndsWith("_" + v)) return name.Substring(0, name.Length - v.Length - 1);
            return name;
        }

        public List<string> BaseNames
        {
            get
            {
                var names = new List<string>();
                foreach (var s in styles)
                {
                    var b = BaseName(s);
                    if (!names.Contains(b)) names.Add(b);
                }
                return names;
            }
        }

        public int StyleCount => BaseNames.Count + (optional ? 1 : 0);

        public string CurrentBase
        {
            get
            {
                var names = BaseNames;
                int i = optional ? styleIndex - 1 : styleIndex;
                return i >= 0 && i < names.Count ? names[i] : null;
            }
        }

        // True when the current style comes in variants (so the UI shows the variant buttons).
        public bool CurrentHasVariants
        {
            get
            {
                var b = CurrentBase;
                return b != null && variants.Length > 0 && styles.Exists(s => s.name.EndsWith($"_{b}_{variants[0]}"));
            }
        }

        public GameObject CurrentStyle
        {
            get
            {
                var b = CurrentBase;
                if (b == null) return null;
                if (variants.Length > 0)
                {
                    var v = variants[Mathf.Clamp(variantIndex, 0, variants.Length - 1)];
                    var match = styles.Find(s => s.name.EndsWith($"_{b}_{v}"));
                    if (match != null) return match;
                }
                return styles.Find(s => BaseName(s) == b);
            }
        }

        public void SelectBase(string baseName)
        {
            int i = BaseNames.IndexOf(baseName);
            if (i >= 0) styleIndex = i + (optional ? 1 : 0);
        }

        // "Hair_Twintails" -> "twintails", "Shoes_MaryJanes" -> "mary janes"
        public string CurrentStyleName
        {
            get
            {
                var b = CurrentBase;
                return b == null ? "none" : Regex.Replace(b, "(?<=[a-z])(?=[A-Z])", " ").ToLower();
            }
        }
    }

    // Owns every outfit slot on the character and keeps the visible pieces in sync
    // with the chosen style + color. The UI only talks to this class.
    public class CharacterAppearance : MonoBehaviour
    {
        public List<OutfitSlot> slots = new List<OutfitSlot>();

        [Header("Eye sliders (face shader)")]
        [Range(0.75f, 1.35f)] public float eyeScale = 1f;
        [Range(-0.06f, 0.06f)] public float eyeSpacing = 0f;
        [Range(-0.05f, 0.06f)] public float eyeHeight = 0f;

        // Fired after any style/color change so the UI can refresh its labels.
        public event Action Changed;

        // MaterialPropertyBlock tints a renderer without creating a new material copy.
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EyeScaleId = Shader.PropertyToID("_EyeScale");
        static readonly int EyeSpacingId = Shader.PropertyToID("_EyeSpacing");
        static readonly int EyeHeightId = Shader.PropertyToID("_EyeHeight");
        MaterialPropertyBlock block;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            CharacterSave.Load(this);
            foreach (var slot in slots) Apply(slot);
        }

        public OutfitSlot GetSlot(OutfitCategory category) => slots.Find(s => s.category == category);

        // Re-applies every slot (after loading a look from a save or from the network).
        public void ApplyAll()
        {
            if (block == null) block = new MaterialPropertyBlock();
            foreach (var slot in slots) Apply(slot);
            Changed?.Invoke();
        }

        public void ChangeStyle(OutfitCategory category, int direction)
        {
            var slot = GetSlot(category);
            if (slot == null || slot.StyleCount == 0) return;
            // Wrap around in both directions (the extra + count keeps the result non-negative).
            slot.styleIndex = (slot.styleIndex + direction + slot.StyleCount) % slot.StyleCount;
            Apply(slot);
            Changed?.Invoke();
        }

        public void SetEyes(float scale, float spacing, float height)
        {
            eyeScale = scale;
            eyeSpacing = spacing;
            eyeHeight = height;
            var slot = GetSlot(OutfitCategory.Eyes);
            if (slot != null) Apply(slot);
            Changed?.Invoke();
        }

        public void ResetEyes() => SetEyes(1f, 0f, 0f);

        public void SetVariant(OutfitCategory category, int variant)
        {
            var slot = GetSlot(category);
            if (slot == null || slot.variants.Length == 0) return;
            slot.variantIndex = Mathf.Clamp(variant, 0, slot.variants.Length - 1);
            Apply(slot);
            Changed?.Invoke();
        }

        public void SetColor(OutfitCategory category, int colorIndex)
        {
            var slot = GetSlot(category);
            if (slot == null) return;
            slot.colorIndex = Mathf.Clamp(colorIndex, 0, slot.colors.Length - 1);
            Apply(slot);
            Changed?.Invoke();
        }

        void Apply(OutfitSlot slot)
        {
            slot.styleIndex = Mathf.Clamp(slot.styleIndex, 0, Mathf.Max(0, slot.StyleCount - 1));
            slot.colorIndex = Mathf.Clamp(slot.colorIndex, 0, slot.colors.Length - 1);

            var current = slot.CurrentStyle;
            foreach (var style in slot.styles)
                style.SetActive(style == current);
            if (current == null) return;

            bool eyes = slot.category == OutfitCategory.Eyes;
            foreach (var r in current.GetComponentsInChildren<Renderer>(true))
            {
                // Two-tone pieces (a sailor collar, a sneaker sole...) keep some submeshes untinted.
                var mask = r.GetComponent<TintMask>();
                int count = r.sharedMaterials.Length;
                for (int i = 0; i < count; i++)
                {
                    bool tint = mask == null || mask.Tints(i);
                    if (!tint && !eyes) continue;
                    block.Clear();
                    block.SetColor(ColorId, tint ? slot.colors[slot.colorIndex] : Color.white);
                    if (eyes)   // every face layer moves with the eye sliders
                    {
                        block.SetFloat(EyeScaleId, eyeScale);
                        block.SetFloat(EyeSpacingId, eyeSpacing);
                        block.SetFloat(EyeHeightId, eyeHeight);
                    }
                    r.SetPropertyBlock(block, i);
                }
            }
        }
    }
}
