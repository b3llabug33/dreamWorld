using System;
using System.Globalization;
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
        public string[] options;        // per slot: chosen value of each option row, e.g. "1,2"
        public string[] customColors;   // per slot: "" or a hex color (full-spectrum hair/eyes)
        public string[] placements;     // per slot: "" or "x,y,size" for movable face pieces
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
                options = new string[n],
                customColors = new string[n],
                placements = new string[n],
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
                data.options[i] = string.Join(",", slot.axes.ConvertAll(a => a.index.ToString()));
                data.customColors[i] = slot.useCustom ? ColorUtility.ToHtmlStringRGB(slot.customColor) : "";
                data.placements[i] = slot.movable ? string.Join(",", F(slot.moveX), F(slot.moveY), F(slot.size)) : "";
            }
            return JsonUtility.ToJson(data);
        }

        static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

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
                if (data.options != null && i < data.options.Length && !string.IsNullOrEmpty(data.options[i]))
                {
                    var parts = data.options[i].Split(',');
                    for (int a = 0; a < parts.Length && a < slot.axes.Count; a++)
                        if (int.TryParse(parts[a], out int v)) slot.axes[a].index = v;
                }
                slot.moveX = slot.moveY = 0f;
                slot.size = 1f;
                if (slot.movable && data.placements != null && i < data.placements.Length && !string.IsNullOrEmpty(data.placements[i]))
                {
                    var p = data.placements[i].Split(',');
                    if (p.Length == 3)
                    {
                        float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out slot.moveX);
                        float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out slot.moveY);
                        if (!float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out slot.size) || slot.size <= 0)
                            slot.size = 1f;
                    }
                }
                slot.useCustom = false;
                if (data.customColors != null && i < data.customColors.Length && !string.IsNullOrEmpty(data.customColors[i])
                    && ColorUtility.TryParseHtmlString("#" + data.customColors[i], out var c))
                {
                    slot.customColor = c;
                    slot.useCustom = true;
                }
            }
            return true;
        }
    }
}
