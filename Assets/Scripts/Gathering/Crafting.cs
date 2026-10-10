using System.Collections.Generic;
using System.Linq;
using DreamWorld.Gameplay;

namespace DreamWorld.Gathering
{
    // A crafting recipe. Wooden recipes are made in the wood you pick (oak stool, red cedar bench...), so the
    // item they make is "<id>_<wood>"; others make one fixed item.
    public class Recipe
    {
        public string id, name, description, category;   // category: decor / tools / weapons / food
        public ItemKind kind = ItemKind.Decor;
        public bool usesWood;
        public int woodCount;
        public (string item, int count)[] needs = new (string, int)[0];
        public string output;      // fixed item id (non-wood recipes that make an existing item); null = generated
        public int sell = 4;       // base sell price of what it makes
        // wooden weapons
        public string model;
        public float damage, speed = 1f, reach = 1f, heavy = 2.2f, modelScale = 1f;
    }

    public static class Crafting
    {
        public static readonly Recipe[] Recipes =
        {
            new Recipe { id = "decor_stool", name = "mushroom stool", category = "decor", usesWood = true, woodCount = 3,
                         needs = new[] { ("thread", 2) }, sell = 6, description = "a little stool with a soft cap cushion" },
            new Recipe { id = "decor_table", name = "little table", category = "decor", usesWood = true, woodCount = 5,
                         sell = 8, description = "a sturdy table for snacks and potions" },
            new Recipe { id = "decor_bookshelf", name = "bookshelf", category = "decor", usesWood = true, woodCount = 8,
                         needs = new[] { ("thread", 2) }, sell = 12, description = "a tall shelf full of cozy books" },
            new Recipe { id = "decor_bench", name = "garden bench", category = "decor", usesWood = true, woodCount = 6,
                         needs = new[] { ("stone", 2) }, sell = 10, description = "a long bench for sitting and watching the fog" },
            new Recipe { id = "decor_planter", name = "planter box", category = "decor", usesWood = true, woodCount = 4,
                         needs = new[] { ("stone", 3), ("berries", 1) }, sell = 9, description = "a planter with a little berry sprout" },
            new Recipe { id = "decor_crystal_lamp", name = "crystal lamp", category = "decor", usesWood = true, woodCount = 2,
                         needs = new[] { ("dream_crystal", 1), ("silver_ore", 1) }, sell = 40, description = "a lamp with a glowing dream crystal" },
            new Recipe { id = "decor_stone_lantern", name = "stone lantern", category = "decor", woodCount = 0,
                         needs = new[] { ("stone", 5), ("copper_ore", 1) }, sell = 12, description = "a garden lantern carved from stone" },
            new Recipe { id = "decor_rug", name = "cozy rug", category = "decor", woodCount = 0,
                         needs = new[] { ("thread", 8) }, sell = 14, description = "a soft woven rug" },

            new Recipe { id = "stone_axe", name = "stone axe", category = "tools", kind = ItemKind.Tool, output = "stone_axe", woodCount = 3,
                         needs = new[] { ("stone", 3) }, description = "keep it in your bag: trees give twice the wood (any wood works)" },
            new Recipe { id = "pickaxe", name = "pickaxe", category = "tools", kind = ItemKind.Tool, output = "pickaxe", woodCount = 3,
                         needs = new[] { ("stone", 4), ("copper_ore", 2) }, description = "keep it in your bag: mine silver and dream crystal (any wood works)" },

            new Recipe { id = "wood_club", name = "wooden club", category = "weapons", kind = ItemKind.Weapon, usesWood = true, woodCount = 5,
                         needs = new[] { ("thread", 1) }, sell = 12, model = "WoodClub", damage = 13, speed = 1.05f, reach = 0.95f, heavy = 2.4f,
                         modelScale = 0.95f, description = "a knotty club with a thread grip. rarer woods hit harder" },
            new Recipe { id = "wood_staff", name = "wooden staff", category = "weapons", kind = ItemKind.Weapon, usesWood = true, woodCount = 4,
                         needs = new[] { ("dream_crystal", 1) }, sell = 30, model = "WoodStaff", damage = 12, speed = 1f, reach = 1.3f, heavy = 2.1f,
                         modelScale = 0.8f, description = "a long staff with a little dream crystal. great reach" },
            new Recipe { id = "silver_sword", name = "silver sword", category = "weapons", kind = ItemKind.Weapon, output = "silver_sword", woodCount = 2,
                         needs = new[] { ("silver_ore", 3), ("thread", 1) }, description = "a bright silver blade (any wood for the handle)" },
            new Recipe { id = "crystal_dagger", name = "crystal dagger", category = "weapons", kind = ItemKind.Weapon, output = "crystal_dagger", woodCount = 0,
                         needs = new[] { ("dream_crystal", 2), ("silver_ore", 1) }, description = "a fast glowing dagger" },

            new Recipe { id = "berry_tart", name = "berry tart", category = "food", kind = ItemKind.Food, output = "berry_tart", woodCount = 0,
                         needs = new[] { ("berries", 4), ("forest_mushroom", 1) }, description = "bake a filling tart (fills a lot of food)" },
        };

        public static Recipe Get(string id) => Recipes.FirstOrDefault(r => r.id == id);
        public static string OutputId(Recipe r, string wood) => r.output ?? (r.usesWood && wood != null ? r.id + "_" + wood : r.id);

        // What the recipe needs, with the wood resolved: wooden recipes need that wood; tools take any wood
        // (the first kind you have enough of).
        public static List<(string item, int count)> Needs(Recipe r, string wood, PlayerStats p)
        {
            var list = r.needs.ToList();
            if (r.woodCount > 0)
            {
                string w = r.usesWood ? wood : AnyWood(r.woodCount, p) ?? "pine";
                list.Insert(0, ("wood_" + w, r.woodCount));
            }
            return list;
        }

        // A wood you have at least `count` of (most first), for "any wood" recipes.
        public static string AnyWood(int count, PlayerStats p)
        {
            if (p == null) return null;
            return Items.Woods.Select(w => w.id).OrderByDescending(w => Inventory.Count(p.Bag, "wood_" + w))
                        .FirstOrDefault(w => Inventory.Count(p.Bag, "wood_" + w) >= count);
        }

        public static bool CanCraft(Recipe r, string wood, PlayerStats p) =>
            p != null && Needs(r, wood, p).All(n => Inventory.Count(p.Bag, n.item) >= n.count);

        // Takes the materials and puts the result in the bag. False (and nothing taken) if something's missing
        // or the bag is full.
        public static bool Craft(Recipe r, string wood, PlayerStats p)
        {
            if (!CanCraft(r, wood, p)) return false;
            string output = OutputId(r, r.usesWood ? wood : null);
            if (Items.Get(output) == null) return false;
            var needs = Needs(r, wood, p);
            foreach (var n in needs) Inventory.Remove(p.Bag, n.item, n.count);
            if (p.AddItem(output, 1) == 0)
            {
                foreach (var n in needs) Inventory.Add(p.Bag, n.item, n.count);   // no room: give everything back
                p.AddItem(output, 0);
                return false;
            }
            return true;
        }
    }
}
