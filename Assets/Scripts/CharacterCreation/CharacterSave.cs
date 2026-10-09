using System;
using UnityEngine;

namespace DreamWorld.CharacterCreation
{
    // The character's look as a tiny JSON (style + color per slot). It's saved in PlayerPrefs
    // so later scenes rebuild the same character, and it's what multiplayer sends to other
    // players. PlayerPrefs also works in WebGL.
    [Serializable]
    public class CharacterSave
    {
        const string Key = "dreamWorld.character";

        public string[] categories;
        public int[] styleIndexes;
        public int[] colorIndexes;
        public float eyeScale = 1f, eyeSpacing, eyeHeight;

        public static string SavedJson => PlayerPrefs.GetString(Key, "");

        public static string ToJson(CharacterAppearance appearance)
        {
            int n = appearance.slots.Count;
            var data = new CharacterSave
            {
                categories = new string[n],
                styleIndexes = new int[n],
                colorIndexes = new int[n],
                eyeScale = appearance.eyeScale,
                eyeSpacing = appearance.eyeSpacing,
                eyeHeight = appearance.eyeHeight,
            };
            for (int i = 0; i < n; i++)
            {
                var slot = appearance.slots[i];
                data.categories[i] = slot.category.ToString();
                data.styleIndexes[i] = slot.styleIndex;
                data.colorIndexes[i] = slot.colorIndex;
            }
            return JsonUtility.ToJson(data);
        }

        public static void Save(CharacterAppearance appearance)
        {
            PlayerPrefs.SetString(Key, ToJson(appearance));
            PlayerPrefs.Save();
        }

        // Reads the saved look into the slots (CharacterAppearance applies it in Awake).
        public static void Load(CharacterAppearance appearance)
        {
            if (PlayerPrefs.HasKey(Key)) Read(appearance, PlayerPrefs.GetString(Key));
        }

        // Applies a look received from someone else (multiplayer) and refreshes the meshes.
        public static void ApplyJson(CharacterAppearance appearance, string json)
        {
            if (Read(appearance, json)) appearance.ApplyAll();
        }

        // Matches entries by category name, so adding/reordering slots later won't scramble old saves.
        static bool Read(CharacterAppearance appearance, string json)
        {
            CharacterSave data;
            try { data = JsonUtility.FromJson<CharacterSave>(json); }
            catch (ArgumentException) { return false; }
            if (data?.categories == null) return false;
            appearance.eyeScale = data.eyeScale > 0 ? data.eyeScale : 1f;   // older saves have no eye values
            appearance.eyeSpacing = data.eyeSpacing;
            appearance.eyeHeight = data.eyeHeight;

            for (int i = 0; i < data.categories.Length; i++)
            {
                if (!Enum.TryParse(data.categories[i], out OutfitCategory category)) continue;
                var slot = appearance.GetSlot(category);
                if (slot == null) continue;
                slot.styleIndex = data.styleIndexes[i];
                slot.colorIndex = data.colorIndexes[i];
            }
            return true;
        }
    }
}
