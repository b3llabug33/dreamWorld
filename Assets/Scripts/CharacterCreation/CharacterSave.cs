using System;
using UnityEngine;

namespace DreamWorld.CharacterCreation
{
    // Stores the chosen style/color per slot in PlayerPrefs as JSON, so later scenes
    // (houses, the world) can rebuild the same character. PlayerPrefs also works in WebGL.
    [Serializable]
    public class CharacterSave
    {
        const string Key = "dreamWorld.character";

        public string[] categories;
        public int[] styleIndexes;
        public int[] colorIndexes;

        public static void Save(CharacterAppearance appearance)
        {
            int n = appearance.slots.Count;
            var data = new CharacterSave
            {
                categories = new string[n],
                styleIndexes = new int[n],
                colorIndexes = new int[n],
            };
            for (int i = 0; i < n; i++)
            {
                var slot = appearance.slots[i];
                data.categories[i] = slot.category.ToString();
                data.styleIndexes[i] = slot.styleIndex;
                data.colorIndexes[i] = slot.colorIndex;
            }
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        // Matches saved entries by category name, so adding/reordering slots later won't scramble old saves.
        public static void Load(CharacterAppearance appearance)
        {
            if (!PlayerPrefs.HasKey(Key)) return;
            var data = JsonUtility.FromJson<CharacterSave>(PlayerPrefs.GetString(Key));
            if (data?.categories == null) return;

            for (int i = 0; i < data.categories.Length; i++)
            {
                if (!Enum.TryParse(data.categories[i], out OutfitCategory category)) continue;
                var slot = appearance.GetSlot(category);
                if (slot == null) continue;
                slot.styleIndex = data.styleIndexes[i];
                slot.colorIndex = data.colorIndexes[i];
            }
        }
    }
}
