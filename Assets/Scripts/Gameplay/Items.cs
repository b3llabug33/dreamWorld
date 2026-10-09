using System.Collections.Generic;
using UnityEngine;

namespace DreamWorld.Gameplay
{
    public enum ItemKind { Food, Potion, Material }

    // What an item is. Every item in the game is listed in Items below; saves only store the id.
    public class ItemDef
    {
        public string id, name, description;
        public ItemKind kind;
        public int maxStack = 1;
        public float food, heal;     // what using it restores (food/potions)
        public int buyPrice;         // 0 = shops don't sell it
        public int sellPrice;        // 0 = can't be sold
        public bool Usable => kind != ItemKind.Material;
    }

    // The item list. Icons are 64x64 pixel-art PNGs in Resources/Icons/<id>.png (ArtSource/make_icons.py).
    public static class Items
    {
        static readonly Dictionary<string, ItemDef> all = new Dictionary<string, ItemDef>();
        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        static Items()
        {
            Add(new ItemDef { id = "snack", name = "snack", kind = ItemKind.Food, maxStack = 10, food = 35, heal = 15,
                              buyPrice = 10, sellPrice = 4, description = "a cozy little snack from the stand in town" });
            Add(new ItemDef { id = "glow_jelly", name = "glow jelly", kind = ItemKind.Potion, maxStack = 5, heal = 45,
                              buyPrice = 30, sellPrice = 12, description = "wobbly and warm. heals a lot" });
            Add(new ItemDef { id = "spore_cap", name = "spore cap", kind = ItemKind.Material, maxStack = 50,
                              sellPrice = 3, description = "a soft glowing cap a shroomling left behind" });
            Add(new ItemDef { id = "dream_shard", name = "dream shard", kind = ItemKind.Material, maxStack = 20,
                              sellPrice = 25, description = "a sharp little piece of a dream. someone might want this" });
        }

        static void Add(ItemDef def) => all[def.id] = def;

        public static ItemDef Get(string id) => !string.IsNullOrEmpty(id) && all.TryGetValue(id, out var d) ? d : null;

        public static Sprite Icon(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (icons.TryGetValue(id, out var s)) return s;
            var tex = Resources.Load<Texture2D>("Icons/" + id);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;   // keep the pixel art crisp
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f, 100f);
            }
            icons[id] = s;
            return s;
        }
    }
}
