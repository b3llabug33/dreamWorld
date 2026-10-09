using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DreamWorld.CharacterCreation
{
    public enum OutfitCategory { Skin, Eyes, Hair, Top, Bottom, Legwear, Shoes, Glasses, Headwear }

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

        public int StyleCount => styles.Count + (optional ? 1 : 0);

        public GameObject CurrentStyle
        {
            get
            {
                int i = optional ? styleIndex - 1 : styleIndex;
                return i >= 0 && i < styles.Count ? styles[i] : null;
            }
        }

        // "Hair_Twintails" -> "twintails", "Shoes_MaryJanes" -> "mary janes"
        public string CurrentStyleName
        {
            get
            {
                var style = CurrentStyle;
                if (style == null) return "none";
                var name = style.name.Substring(style.name.IndexOf('_') + 1);
                return Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ").ToLower();
            }
        }
    }

    // Owns every outfit slot on the character and keeps the visible pieces in sync
    // with the chosen style + color. The UI only talks to this class.
    public class CharacterAppearance : MonoBehaviour
    {
        public List<OutfitSlot> slots = new List<OutfitSlot>();

        // Fired after any style/color change so the UI can refresh its labels.
        public event Action Changed;

        // MaterialPropertyBlock tints a renderer without creating a new material copy.
        static readonly int ColorId = Shader.PropertyToID("_Color");
        MaterialPropertyBlock block;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            CharacterSave.Load(this);
            foreach (var slot in slots) Apply(slot);
        }

        public OutfitSlot GetSlot(OutfitCategory category) => slots.Find(s => s.category == category);

        public void ChangeStyle(OutfitCategory category, int direction)
        {
            var slot = GetSlot(category);
            if (slot == null || slot.StyleCount == 0) return;
            // Wrap around in both directions (the extra + count keeps the result non-negative).
            slot.styleIndex = (slot.styleIndex + direction + slot.StyleCount) % slot.StyleCount;
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

            block.SetColor(ColorId, slot.colors[slot.colorIndex]);
            foreach (var r in current.GetComponentsInChildren<Renderer>(true))
            {
                // Two-tone pieces (a sailor collar, a sneaker sole...) keep some submeshes untinted.
                var mask = r.GetComponent<TintMask>();
                int count = r.sharedMaterials.Length;
                for (int i = 0; i < count; i++)
                    if (mask == null || mask.Tints(i))
                        r.SetPropertyBlock(block, i);
            }
        }
    }
}
