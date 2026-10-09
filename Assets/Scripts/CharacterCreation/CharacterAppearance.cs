using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DreamWorld.CharacterCreation
{
    public enum OutfitCategory { Skin, Eyes, Nose, Mouth, Hair, Top, Bottom, Legwear, Shoes, Neck, Glasses, Headwear }

    // One extra choice row for a slot, e.g. hair "bangs" {Straight, Choppy, ...} or pants "rise"
    // {High, Mid, Low}. Mesh names carry the chosen values after the base name:
    // Hair_Long_Straight_Medium, Bottom_BaggyJeans_High. Styles that don't come in a value
    // (skirts have no rise, ponytails have one bangs style) just hide that row.
    [Serializable]
    public class OptionAxis
    {
        public string label;
        public string[] values = new string[0];
        public int index;
    }

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
        public List<OptionAxis> axes = new List<OptionAxis>();

        // Full-spectrum color (hair, eyes): when on, customColor replaces the palette color.
        public bool customAllowed;
        public bool useCustom;
        public Color customColor = Color.white;

        public Color CurrentColor => useCustom ? customColor : colors[Mathf.Clamp(colorIndex, 0, colors.Length - 1)];

        // "Hair_Long_Straight_Medium" -> base "Long"
        public string BaseName(GameObject style) => style.name.Split('_')[1];

        bool HasValue(GameObject style, string value)
        {
            var tokens = style.name.Split('_');
            for (int i = 2; i < tokens.Length; i++)
                if (tokens[i] == value) return true;
            return false;
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

        List<GameObject> CurrentBaseStyles
        {
            get
            {
                var b = CurrentBase;
                return b == null ? new List<GameObject>() : styles.FindAll(s => BaseName(s) == b);
            }
        }

        // Which values of an axis the current style actually comes in (the UI shows only these).
        public List<int> AvailableValues(OptionAxis axis)
        {
            var found = new List<int>();
            var mine = CurrentBaseStyles;
            for (int v = 0; v < axis.values.Length; v++)
                if (mine.Exists(s => HasValue(s, axis.values[v]))) found.Add(v);
            return found;
        }

        // The value an axis is really showing: the chosen one if this style has it, else its first.
        public int ShownValue(OptionAxis axis)
        {
            var available = AvailableValues(axis);
            if (available.Count == 0) return -1;
            return available.Contains(axis.index) ? axis.index : available[0];
        }

        public GameObject CurrentStyle
        {
            get
            {
                var candidates = CurrentBaseStyles;
                foreach (var axis in axes)
                {
                    int v = ShownValue(axis);
                    if (v < 0) continue;
                    var narrowed = candidates.FindAll(s => HasValue(s, axis.values[v]));
                    if (narrowed.Count > 0) candidates = narrowed;
                }
                return candidates.Count > 0 ? candidates[0] : null;
            }
        }

        public void SelectBase(string baseName)
        {
            int i = BaseNames.IndexOf(baseName);
            if (i >= 0) styleIndex = i + (optional ? 1 : 0);
        }

        // "Hair_WolfCut" -> "wolf cut", "Shoes_MaryJanes" -> "mary janes"
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

        // The look the character starts with before any save is loaded ("make a new player").
        public string DefaultLook { get; private set; }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            DefaultLook = CharacterSave.ToJson(this);
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

        // Picks a value on one of a slot's option rows (bangs, length, rise).
        public void SetOption(OutfitCategory category, int axis, int value)
        {
            var slot = GetSlot(category);
            if (slot == null || axis < 0 || axis >= slot.axes.Count) return;
            slot.axes[axis].index = Mathf.Clamp(value, 0, slot.axes[axis].values.Length - 1);
            Apply(slot);
            Changed?.Invoke();
        }

        public void SetColor(OutfitCategory category, int colorIndex)
        {
            var slot = GetSlot(category);
            if (slot == null) return;
            slot.colorIndex = Mathf.Clamp(colorIndex, 0, slot.colors.Length - 1);
            slot.useCustom = false;
            Apply(slot);
            Changed?.Invoke();
        }

        public void SetCustomColor(OutfitCategory category, Color color)
        {
            var slot = GetSlot(category);
            if (slot == null || !slot.customAllowed) return;
            slot.customColor = color;
            slot.useCustom = true;
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
                    block.SetColor(ColorId, tint ? slot.CurrentColor : Color.white);
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
