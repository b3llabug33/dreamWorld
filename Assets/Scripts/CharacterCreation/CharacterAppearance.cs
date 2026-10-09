using System;
using System.Collections.Generic;
using UnityEngine;

namespace DreamWorld.CharacterCreation
{
    public enum OutfitCategory { Skin, Hair, Top, Bottom, Shoes }

    // One customizable part of the character. Each entry in `styles` is a child object
    // (a placeholder shape now, a real model prefab later); only the selected one is shown.
    [Serializable]
    public class OutfitSlot
    {
        public OutfitCategory category;
        public List<GameObject> styles = new List<GameObject>();
        public Color[] colors = { Color.white };
        public int styleIndex;
        public int colorIndex;

        public GameObject CurrentStyle => styles.Count > 0 ? styles[styleIndex] : null;
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
            if (slot == null || slot.styles.Count == 0) return;
            // Wrap around in both directions (the extra + Count keeps the result non-negative).
            slot.styleIndex = (slot.styleIndex + direction + slot.styles.Count) % slot.styles.Count;
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
            slot.styleIndex = Mathf.Clamp(slot.styleIndex, 0, Mathf.Max(0, slot.styles.Count - 1));
            slot.colorIndex = Mathf.Clamp(slot.colorIndex, 0, slot.colors.Length - 1);

            for (int i = 0; i < slot.styles.Count; i++)
                slot.styles[i].SetActive(i == slot.styleIndex);

            var current = slot.CurrentStyle;
            if (current == null) return;
            block.SetColor(ColorId, slot.colors[slot.colorIndex]);
            foreach (var r in current.GetComponentsInChildren<Renderer>(true))
                r.SetPropertyBlock(block);
        }
    }
}
