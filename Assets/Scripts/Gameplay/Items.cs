using System.Collections.Generic;
using UnityEngine;

namespace DreamWorld.Gameplay
{
    public enum ItemKind { Food, Potion, Material, Weapon, Shield }

    // What an item is. Every item in the game is listed in Items below; saves only store the id.
    public class ItemDef
    {
        public string id, name, description;
        public ItemKind kind;
        public int maxStack = 1;
        public float food, heal;     // what using it restores (food/potions)
        public int buyPrice;         // 0 = shops don't sell it
        public int sellPrice;        // 0 = can't be sold
        // weapons: damage per light hit, swing speed (1 = normal), reach (1 = sword length), heavy = heavy hit multiplier
        public float damage, speed = 1f, reach = 1f, heavy = 2.2f;
        public bool ranged;          // fires a shot instead of swinging (the slingshot)
        public float guard;          // shields: how much of a hit a guard blocks
        public string model;         // Resources/Weapons/<model> prefab held in the hand
        public float modelScale = 1f;
        public bool Usable => kind != ItemKind.Material;
        public bool Equippable => kind == ItemKind.Weapon || kind == ItemKind.Shield;
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

            // weapons (left click to swing, shift + click for a heavy hit) and the off-hand shield
            Weapon("beetle_horn_sword", "beetle horn sword", "BeetleHornSword", 16, 1f, 1f, 2.2f, 60,
                   "a sword carved from a stag beetle's horn. a good first blade", 0.85f);
            Weapon("umbrella_sword", "umbrella sword", "UmbrellaSword", 15, 1.2f, 1.1f, 2f, 80,
                   "fast and pointy. keeps the rain off between fights", 0.85f);
            Weapon("frying_pan", "frying pan", "FryingPan", 18, 0.95f, 0.85f, 2.5f, 40,
                   "makes a very satisfying bonk");
            Weapon("mushroom_mace", "mushroom mace", "MushroomMace", 22, 0.85f, 0.9f, 2.6f, 90,
                   "a heavy spotted cap on a stick. slow but it hits hard");
            Weapon("keyboard_hammer", "keyboard hammer", "KeyboardHammer", 26, 0.75f, 0.95f, 2.8f, 110,
                   "an old clacky keyboard on a handle. every hit types a letter");
            Weapon("crystal_staff", "crystal staff", "CrystalStaff", 14, 1f, 1.3f, 2f, 120,
                   "a long glowing staff. great reach", 0.8f);
            Weapon("mantis_scythe", "mantis scythe", "MantisScythe", 20, 1.1f, 1.35f, 2.3f, 150,
                   "a curved blade like a praying mantis arm. wide sweeping cuts", 0.85f);
            Weapon("slingshot", "slingshot", "Slingshot", 10, 1.1f, 1f, 2f, 50,
                   "shoots pebbles from a distance. heavy shots fire three", 1.2f, ranged: true);
            Add(new ItemDef { id = "petal_shield", name = "petal shield", kind = ItemKind.Shield, model = "PetalShield", guard = 0.9f,
                              buyPrice = 70, sellPrice = 23, modelScale = 0.8f,
                              description = "a shield of hard flower petals. guard (right click) blocks most of a hit" });

            // monster drops
            Drop("golem_moss", "golem moss", 8, "a soft clump of moss off a moss golem's back");
            Drop("thorn", "beetle thorn", 6, "a sharp thorn from a thorn beetle's shell");
            Drop("moth_dust", "moth dust", 5, "glittery dust that flickers like a broken screen");
            Drop("rot_slime", "rot slime", 4, "smelly green slime. somebody probably wants it");
            Drop("static_spark", "static spark", 9, "a tiny crackling spark in a jar. don't touch");
            Drop("ivy_leaf", "ivy leaf", 5, "a leaf from an ivy serpent. still wiggles a bit");
            Drop("plush_stuffing", "plush stuffing", 6, "fluffy stuffing from a cursed plush. it whispers");
            Drop("bat_wing", "bat wing", 6, "a little leathery wing. it's fine, it grows back");
            // quest items (the Mullens questline)
            Add(new ItemDef { id = "moody_map", name = "moody map", kind = ItemKind.Material, maxStack = 1,
                              description = "Count Snackula's old map of the fog woods. drawn during his moody phase" });
            Add(new ItemDef { id = "fog_cloak", name = "fog cloak", kind = ItemKind.Material, maxStack = 1, sellPrice = 40,
                              description = "a gray cloak that smells like rain. the Wolfpups can't smell you as well in it" });
            Weapon("glimmer_blade", "glimmer blade", "BeetleHornSword", 30, 1.05f, 1.1f, 2.4f, 0,
                   "a gift from the Mullens. it sparkles in the sun. obviously", 0.9f);
            Add(new ItemDef { id = "knight_cap", name = "knight's cap", kind = ItemKind.Material, maxStack = 5, sellPrice = 120,
                              description = "the shiny cap of the mushroom knight. proof you beat it" });
        }

        static void Weapon(string id, string name, string model, float damage, float speed, float reach, float heavy,
                           int buy, string description, float scale = 1f, bool ranged = false)
            => Add(new ItemDef { id = id, name = name, kind = ItemKind.Weapon, model = model, damage = damage, speed = speed,
                                 reach = reach, heavy = heavy, buyPrice = buy, sellPrice = buy / 3, ranged = ranged,
                                 modelScale = scale, description = description });

        static void Drop(string id, string name, int sell, string description)
            => Add(new ItemDef { id = id, name = name, kind = ItemKind.Material, maxStack = 50, sellPrice = sell, description = description });

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
