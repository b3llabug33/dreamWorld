using DreamWorld.Gameplay;
using UnityEngine;

namespace DreamWorld.Gathering
{
    // Something you gather from: a tree (wood), a rock (stone), an ore vein, a cotton bush, a berry bush...
    // Press E on it: it shakes, you get a little of its material (more with the right tool), it costs a bit of
    // stamina, and after a few goes it's used up (a stump, rubble, a picked bush) until it grows back.
    public class ResourceNode : Interactable
    {
        public string item = "stone";          // what it gives
        public string verb = "break";          // "chop", "mine", "pick"...
        public string label = "rock";          // "pine tree", "copper vein"
        public int hits = 4;                   // goes until it's used up
        public Vector2Int perHit = new Vector2Int(1, 2);
        public float stamina = 6f;
        public float regrowSeconds = 180f;
        public string needsTool;               // e.g. "pickaxe" for silver and dream crystal
        public string boostTool;               // e.g. "stone_axe" doubles wood
        public string bonusItem;               // a rare extra (a dream crystal from a rune stone...)
        [Range(0, 1)] public float bonusChance;
        public GameObject full, used;          // shown before / after it's used up (used may be null: shrink instead)

        int left;
        float regrowAt = -1f;
        float shake;
        float cooldown;
        Vector3 baseScale;
        Quaternion baseRot;

        public bool Depleted => regrowAt > 0f;

        void Awake()
        {
            left = hits;
            if (full == null) full = gameObject;
            baseScale = full.transform.localScale;
            baseRot = full.transform.localRotation;
            if (used != null) used.SetActive(false);
        }

        public override string Prompt(PlayerStats player)
        {
            if (Depleted) return $"{label} (growing back...)";
            if (!string.IsNullOrEmpty(needsTool) && Inventory.Count(player.Bag, needsTool) == 0)
                return $"{verb} {label} (needs a {Items.Get(needsTool)?.name ?? needsTool})";
            return $"{verb} {label}";
        }

        public override void Interact(PlayerStats player)
        {
            if (Depleted || Time.time < cooldown) return;
            if (!string.IsNullOrEmpty(needsTool) && Inventory.Count(player.Bag, needsTool) == 0)
            {
                GameHUD.Instance?.Toast($"you need a {Items.Get(needsTool)?.name ?? needsTool} for this. craft one at a workbench");
                return;
            }
            if (!player.TrySpendStamina(stamina))
            {
                GameHUD.Instance?.Toast("too tired. catch your breath first");
                return;
            }
            cooldown = Time.time + 0.35f;
            int amount = Random.Range(perHit.x, perHit.y + 1);
            if (!string.IsNullOrEmpty(boostTool) && Inventory.Count(player.Bag, boostTool) > 0) amount *= 2;
            int got = player.AddItem(item, amount);
            var def = Items.Get(item);
            if (got > 0) GameHUD.Instance?.Toast($"+{got} {def?.name ?? item}");
            else GameHUD.Instance?.Toast("your bag is full");
            if (!string.IsNullOrEmpty(bonusItem) && Random.value < bonusChance && player.AddItem(bonusItem, 1) > 0)
                GameHUD.Instance?.Toast($"+1 {Items.Get(bonusItem)?.name ?? bonusItem}!");
            shake = 1f;
            if (--left <= 0) Deplete();
        }

        void Deplete()
        {
            regrowAt = Time.time + regrowSeconds * Random.Range(0.85f, 1.15f);
            if (used != null)
            {
                ShowFull(false);
                used.SetActive(true);
            }
            else full.transform.localScale = baseScale * 0.45f;   // rubble / a picked-over bush
        }

        void Regrow()
        {
            regrowAt = -1f;
            left = hits;
            if (used != null) used.SetActive(false);
            ShowFull(true);
            full.transform.localScale = baseScale;
        }

        // Hides/shows the full model's renderers and colliders (never the GameObject itself: `full` may be this
        // node, and a disabled node couldn't grow back).
        void ShowFull(bool on)
        {
            foreach (var r in full.GetComponentsInChildren<Renderer>(true))
                if (used == null || !r.transform.IsChildOf(used.transform)) r.enabled = on;
            foreach (var c in full.GetComponentsInChildren<Collider>(true))
                if (used == null || !c.transform.IsChildOf(used.transform)) c.enabled = on;
            foreach (var l in full.GetComponentsInChildren<Light>(true))
                if (used == null || !l.transform.IsChildOf(used.transform)) l.enabled = on;
        }

        void Update()
        {
            if (Depleted && Time.time >= regrowAt) Regrow();
            if (shake > 0f && !Depleted)
            {
                shake = Mathf.Max(0f, shake - Time.deltaTime * 4f);
                float a = Mathf.Sin(Time.time * 45f) * 3f * shake;
                full.transform.localRotation = baseRot * Quaternion.Euler(a, 0, a * 0.6f);
            }
            else if (!Depleted) full.transform.localRotation = baseRot;
        }
    }
}
