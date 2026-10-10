using System.Collections.Generic;
using DreamWorld.Account;
using DreamWorld.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DreamWorld.Gameplay
{
    // The bag (E when nothing's nearby to use, or I / Tab), the 1-4 quick-item hotbar, home storage and the shop, in the pixel-terminal style.
    //   bag:      click an item to pick it, click another slot to move/swap/stack it there,
    //             right-click to use it (or send it across to storage while that's open)
    //   details:  use / drop, and 1-4 to put it on a quick slot (or press 1-4 while it's picked)
    //   hotbar:   1-4 uses that item while playing
    //   storage:  56 more slots at your home computer
    //   shop:     buy from the list; pick something in your bag to sell it
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.bagPanel != null && Instance.bagPanel.gameObject.activeSelf;

        public Font titleFont, bodyFont;

        enum Side { None, Storage, Shop }

        class Slot { public Image frame, icon; public Text count; }

        TerminalUI ui;
        RectTransform canvas, bagPanel, storagePanel, shopPanel, hotbarRoot;
        readonly List<Slot> bagSlots = new List<Slot>(), storageSlots = new List<Slot>(), hotbarSlots = new List<Slot>();
        Text detailName, detailText, shopInfo, bagTitle, shopTitle;
        Button useButton, dropButton, sellButton;
        readonly Button[] assignButtons = new Button[4];
        Side side;
        string[] shopStock = new string[0];
        RectTransform shopList;

        // what's picked up: which array and index (-1 = nothing)
        ItemStack[] pickedFrom;
        int picked = -1;
        PlayerStats stats;
        PlayerController player;

        static readonly Color SlotColor = new Color(0.10f, 0.08f, 0.18f, 0.92f);
        static readonly Color PickedColor = new Color(0.98f, 0.66f, 0.78f, 1f);
        static readonly Color EquippedColor = new Color(0.36f, 0.24f, 0.52f, 0.95f);   // what's in your hand

        void Awake() => Instance = this;
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ThirdPersonCamera.Showcase = false;
        }

        void Start()
        {
            ui = new TerminalUI(titleFont, bodyFont);
            canvas = ui.Canvas("InventoryUI", 7);
            BuildBag();
            BuildStorage();
            BuildShop();
            BuildHotbar();
            bagPanel.gameObject.SetActive(false);
            storagePanel.gameObject.SetActive(false);
            shopPanel.gameObject.SetActive(false);
        }

        // ---------- building ----------

        Slot MakeSlot(RectTransform parent, Vector2 pos, float size, System.Action onLeft, System.Action onRight)
        {
            var go = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(SlotClicks));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            TerminalUI.Place(rect, new Vector2(0, 1), pos, new Vector2(size, size), new Vector2(0, 1));
            var frame = go.GetComponent<Image>();
            frame.color = SlotColor;
            go.AddComponent<Outline>().effectColor = new Color(0.78f, 0.72f, 0.98f, 0.7f);
            var clicks = go.GetComponent<SlotClicks>();
            clicks.left = onLeft;
            clicks.right = onRight;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(rect, false);
            var icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            TerminalUI.Stretch(icon.rectTransform, size * 0.12f, size * 0.12f);

            var count = ui.Label(rect, "", Mathf.RoundToInt(size * 0.36f), TerminalUI.Text, TextAnchor.LowerRight);
            TerminalUI.Stretch(count.rectTransform, 5, 2);
            count.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
            return new Slot { frame = frame, icon = icon, count = count };
        }

        void BuildBag()
        {
            bagPanel = ui.PanelBox(canvas, "Bag");
            TerminalUI.Place(bagPanel, new Vector2(1, 0.5f), new Vector2(-60, 20), new Vector2(700, 760), new Vector2(1, 0.5f));
            bagTitle = ui.Label(bagPanel, "== BAG ==", 36, TerminalUI.Dim);
            TerminalUI.Place(bagTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(600, 44));
            for (int i = 0; i < Inventory.BagSize; i++)
            {
                int index = i;
                bagSlots.Add(MakeSlot(bagPanel, new Vector2(30 + (i % 7) * 92, -70 - (i / 7) * 92), 84,
                                      () => ClickSlot(stats.Bag, index), () => RightClick(stats.Bag, index)));
            }
            detailName = ui.Label(bagPanel, "", 36, TerminalUI.Accent, TextAnchor.UpperLeft);
            TerminalUI.Place(detailName.rectTransform, new Vector2(0, 1), new Vector2(34, -452), new Vector2(630, 40), new Vector2(0, 1));
            detailText = ui.Label(bagPanel, "", 28, TerminalUI.Text, TextAnchor.UpperLeft);
            detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            TerminalUI.Place(detailText.rectTransform, new Vector2(0, 1), new Vector2(34, -494), new Vector2(630, 90), new Vector2(0, 1));

            useButton = ui.Button(bagPanel, "use", 30, () => { if (PickedItem != null) Use(PickedItem.id); });
            TerminalUI.Place((RectTransform)useButton.transform, new Vector2(0, 0), new Vector2(34, 104), new Vector2(120, 52), new Vector2(0, 0));
            dropButton = ui.Button(bagPanel, "drop", 30, DropPicked);
            TerminalUI.Place((RectTransform)dropButton.transform, new Vector2(0, 0), new Vector2(166, 104), new Vector2(120, 52), new Vector2(0, 0));
            sellButton = ui.Button(bagPanel, "sell", 30, SellPicked);
            TerminalUI.Place((RectTransform)sellButton.transform, new Vector2(0, 0), new Vector2(298, 104), new Vector2(170, 52), new Vector2(0, 0));
            var quick = ui.Label(bagPanel, "quick slot", 26, TerminalUI.Dim, TextAnchor.MiddleLeft);
            TerminalUI.Place(quick.rectTransform, new Vector2(0, 0), new Vector2(34, 58), new Vector2(160, 40), new Vector2(0, 0.5f));
            for (int k = 0; k < 4; k++)
            {
                int slot = k;
                assignButtons[k] = ui.Button(bagPanel, (k + 1).ToString(), 28, () => AssignHotbar(slot));
                TerminalUI.Place((RectTransform)assignButtons[k].transform, new Vector2(0, 0), new Vector2(190 + k * 62, 58),
                                 new Vector2(54, 44), new Vector2(0, 0.5f));
            }
            var hint = ui.Label(bagPanel, "click: pick / place   right click: use   E / I / tab: close", 22, TerminalUI.Dim);
            TerminalUI.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(660, 30));
        }

        void BuildStorage()
        {
            storagePanel = ui.PanelBox(canvas, "Storage");
            TerminalUI.Place(storagePanel, new Vector2(0, 0.5f), new Vector2(60, 20), new Vector2(700, 760), new Vector2(0, 0.5f));
            var title = ui.Label(storagePanel, "== HOME STORAGE ==", 36, TerminalUI.Dim);
            TerminalUI.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(600, 44));
            for (int i = 0; i < Inventory.StorageSize; i++)
            {
                int index = i;
                storageSlots.Add(MakeSlot(storagePanel, new Vector2(38 + (i % 8) * 79, -70 - (i / 8) * 79), 72,
                                          () => ClickSlot(stats.Storage, index), () => RightClick(stats.Storage, index)));
            }
            var hint = ui.Label(storagePanel, "right click an item to move it between bag and storage", 22, TerminalUI.Dim);
            TerminalUI.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(660, 30));
        }

        void BuildShop()
        {
            shopPanel = ui.PanelBox(canvas, "Shop");
            TerminalUI.Place(shopPanel, new Vector2(0, 0.5f), new Vector2(60, 20), new Vector2(620, 560), new Vector2(0, 0.5f));
            var title = shopTitle = ui.Label(shopPanel, "== SNACK STAND ==", 36, TerminalUI.Dim);
            TerminalUI.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(560, 44));
            shopList = new GameObject("List", typeof(RectTransform)).GetComponent<RectTransform>();
            shopList.SetParent(shopPanel, false);
            TerminalUI.Stretch(shopList, 0);
            shopInfo = ui.Label(shopPanel, "", 26, TerminalUI.Dim);
            shopInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
            TerminalUI.Place(shopInfo.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(560, 70));
        }

        void BuildHotbar()
        {
            hotbarRoot = new GameObject("Hotbar", typeof(RectTransform)).GetComponent<RectTransform>();
            hotbarRoot.SetParent(canvas, false);
            TerminalUI.Place(hotbarRoot, new Vector2(0.5f, 0), new Vector2(0, 74), new Vector2(4 * 80, 72), new Vector2(0.5f, 0));
            for (int k = 0; k < 4; k++)
            {
                int slot = k;
                var s = MakeSlot(hotbarRoot, new Vector2(k * 80, 0), 72, () => UseHotbar(slot), null);
                var key = ui.Label((RectTransform)s.frame.transform, (k + 1).ToString(), 24, TerminalUI.Accent, TextAnchor.UpperLeft);
                TerminalUI.Stretch(key.rectTransform, 5, 1);
                key.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
                hotbarSlots.Add(s);
            }
        }

        // ---------- opening and closing ----------

        public void Toggle() => Open(!IsOpen, Side.None);
        public void OpenStorage() => Open(true, Side.Storage);
        public void OpenShop(string[] stock, string title = "snack stand")
        {
            shopStock = stock;
            shopTitle.text = $"== {title.ToUpper()} ==";
            Open(true, Side.Shop);
        }

        void Open(bool open, Side withSide)
        {
            if (stats == null) return;
            side = open ? withSide : Side.None;
            if (open) GameHUD.Instance?.ClearDialogue();   // townsfolk chatter would sit on top of the panels
            bagPanel.gameObject.SetActive(open);
            storagePanel.gameObject.SetActive(open && side == Side.Storage);
            shopPanel.gameObject.SetActive(open && side == Side.Shop);
            bagTitle.text = side == Side.Shop ? "== BAG ==  (pick an item to sell)" : "== BAG ==";
            if (side == Side.Shop) RebuildShop();
            picked = -1;
            pickedFrom = null;
            ThirdPersonCamera.CursorFree = open || (GameHUD.Instance != null && GameHUD.Instance.MenuOpen);
            ThirdPersonCamera.Showcase = open && side == Side.None;   // just the bag: look at her straight on
            if (player != null) player.InputEnabled = !open && !stats.Dead && (GameHUD.Instance == null || !GameHUD.Instance.MenuOpen);
            if (!open) Save();
            Refresh();
        }

        void RebuildShop()
        {
            foreach (Transform child in shopList) Destroy(child.gameObject);
            bool grid = shopStock.Length > 4;   // big shops (the weapon shop) use two columns of smaller cards
            for (int i = 0; i < shopStock.Length; i++)
            {
                var def = Items.Get(shopStock[i]);
                if (def == null) continue;
                string id = def.id;
                if (grid) { ShopCard(def, i); continue; }
                var row = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(shopList, false);
                TerminalUI.Place(row, new Vector2(0.5f, 1), new Vector2(0, -120 - i * 96), new Vector2(560, 84));
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(row, false);
                iconGo.GetComponent<Image>().sprite = Items.Icon(id);
                iconGo.GetComponent<Image>().raycastTarget = false;
                TerminalUI.Place((RectTransform)iconGo.transform, new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(72, 72), new Vector2(0, 0.5f));
                var label = ui.Label(row, $"{def.name}\n{def.buyPrice} coins", 30, TerminalUI.Text, TextAnchor.MiddleLeft);
                TerminalUI.Place(label.rectTransform, new Vector2(0, 0.5f), new Vector2(100, 0), new Vector2(280, 80), new Vector2(0, 0.5f));
                var buy = ui.Button(row, "buy", 30, () => Buy(id));
                TerminalUI.Place((RectTransform)buy.transform, new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(130, 56), new Vector2(1, 0.5f));
            }
        }

        void ShopCard(ItemDef def, int i)
        {
            string id = def.id;
            var cell = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
            cell.SetParent(shopList, false);
            TerminalUI.Place(cell, new Vector2(0, 1), new Vector2(22 + (i % 2) * 290, -82 - (i / 2) * 80), new Vector2(282, 74), new Vector2(0, 1));
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(cell, false);
            iconGo.GetComponent<Image>().sprite = Items.Icon(id);
            iconGo.GetComponent<Image>().raycastTarget = false;
            TerminalUI.Place((RectTransform)iconGo.transform, new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(64, 64), new Vector2(0, 0.5f));
            var label = ui.Label(cell, def.name, 24, TerminalUI.Text, TextAnchor.UpperLeft);
            TerminalUI.Place(label.rectTransform, new Vector2(0, 1), new Vector2(72, -2), new Vector2(210, 30), new Vector2(0, 1));
            var price = ui.Label(cell, $"{def.buyPrice}c", 24, TerminalUI.Accent, TextAnchor.LowerLeft);
            TerminalUI.Place(price.rectTransform, new Vector2(0, 0), new Vector2(72, 4), new Vector2(90, 30), new Vector2(0, 0));
            var buy = ui.Button(cell, "buy", 24, () => Buy(id));
            TerminalUI.Place((RectTransform)buy.transform, new Vector2(1, 0), new Vector2(-4, 2), new Vector2(90, 38), new Vector2(1, 0));
        }

        // ---------- clicks ----------

        ItemStack PickedItem => pickedFrom != null && picked >= 0 && !pickedFrom[picked].Empty ? pickedFrom[picked] : null;

        void ClickSlot(ItemStack[] slots, int index)
        {
            if (pickedFrom == null || picked < 0)
            {
                if (!slots[index].Empty) { pickedFrom = slots; picked = index; }
            }
            else
            {
                if (!(pickedFrom == slots && picked == index)) Inventory.Move(pickedFrom, picked, slots, index);
                pickedFrom = null;
                picked = -1;
                Save();
            }
            Refresh();
        }

        void RightClick(ItemStack[] slots, int index)
        {
            if (slots[index].Empty) return;
            if (side == Side.Storage)
                Inventory.Transfer(slots, index, slots == stats.Bag ? stats.Storage : stats.Bag);
            else if (slots == stats.Bag)
                Use(slots[index].id);
            pickedFrom = null;
            picked = -1;
            Save();
            Refresh();
        }

        void Use(string id)
        {
            var def = Items.Get(id);
            if (def == null) return;
            if (!def.Usable) { GameHUD.Instance?.Toast($"{def.name} can't be used. sell it or save it for crafting"); return; }
            if (def.Equippable)
            {
                bool on = stats.ToggleEquip(id);
                GameHUD.Instance?.Toast(on ? $"equipped the {def.name}" : $"put away the {def.name}");
                Save();
                Refresh();
                return;
            }
            if (!stats.UseItem(id)) GameHUD.Instance?.Toast(def.food > 0 ? "you're already full" : "you're already at full health");
            Save();
            Refresh();
        }

        void DropPicked()
        {
            var item = PickedItem;
            if (item == null) return;
            GameHUD.Instance?.Toast($"dropped {item.count} {Items.Get(item.id).name}");
            item.Clear();
            pickedFrom = null;
            picked = -1;
            Save();
            Refresh();
        }

        void SellPicked()
        {
            var item = PickedItem;
            if (item == null || side != Side.Shop) return;
            var def = Items.Get(item.id);
            if (def.sellPrice <= 0) { GameHUD.Instance?.Toast("they won't buy that"); return; }
            int coins = def.sellPrice * item.count;
            GameHUD.Instance?.Toast($"sold {item.count} {def.name} for {coins} coins");
            stats.progress.carried += coins;
            item.Clear();
            pickedFrom = null;
            picked = -1;
            Save();
            Refresh();
        }

        void Buy(string id)
        {
            var def = Items.Get(id);
            if (stats.progress.carried < def.buyPrice) { GameHUD.Instance?.Toast("not enough coins"); return; }
            if (Inventory.Add(stats.Bag, id, 1) == 0) { GameHUD.Instance?.Toast("your bag is full"); return; }
            stats.SpendCoins(def.buyPrice);
            GameHUD.Instance?.Toast(def.Equippable ? $"bought a {def.name}! use it from your bag (or a quick slot) to equip it" : $"bought a {def.name}");
            Save();
            Refresh();
        }

        void AssignHotbar(int slot)
        {
            var item = PickedItem;
            if (item == null) return;
            var def = Items.Get(item.id);
            if (!def.Usable) { GameHUD.Instance?.Toast("only food, potions and weapons go on quick slots"); return; }
            for (int k = 0; k < 4; k++) if (stats.progress.hotbar[k] == item.id) stats.progress.hotbar[k] = "";
            stats.progress.hotbar[slot] = item.id;
            Save();
            Refresh();
        }

        void UseHotbar(int slot)
        {
            var id = stats.progress.hotbar[slot];
            if (string.IsNullOrEmpty(id)) return;
            if (Inventory.Count(stats.Bag, id) == 0) { GameHUD.Instance?.Toast($"out of {Items.Get(id)?.name}"); return; }
            Use(id);
        }

        void Save()
        {
            var life = stats != null ? stats.GetComponent<PlayerLife>() : null;
            if (life != null) life.Save();
        }

        // ---------- every frame ----------

        void Update()
        {
            if (stats != PlayerStats.Local)
            {
                stats = PlayerStats.Local;
                player = stats != null ? stats.GetComponent<PlayerController>() : null;
            }
            if (stats == null || stats.Bag == null) { hotbarRoot.gameObject.SetActive(false); return; }
            hotbarRoot.gameObject.SetActive(!stats.Dead);

            bool otherMenu = GameHUD.Instance != null && GameHUD.Instance.MenuOpen;
            if (!otherMenu && !stats.Dead && (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))) Toggle();
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Open(false, Side.None);
            // walked away from the shop or computer: close it
            if (IsOpen && side != Side.None && Interactable.Nearest(stats.transform.position) == null) Open(false, Side.None);

            for (int k = 0; k < 4; k++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha1 + k)) continue;
                if (IsOpen && PickedItem != null) AssignHotbar(k);
                else if (!IsOpen && !otherMenu && !stats.Dead) UseHotbar(k);
            }
            Refresh();
        }

        void Refresh()
        {
            if (stats == null || stats.Bag == null) return;
            for (int k = 0; k < 4; k++)
            {
                var id = stats.progress.hotbar[k];
                int have = string.IsNullOrEmpty(id) ? 0 : Inventory.Count(stats.Bag, id);
                Show(hotbarSlots[k], id, have, false);
                hotbarSlots[k].icon.color = have > 0 ? Color.white : new Color(1, 1, 1, 0.35f);
            }
            if (!IsOpen) return;
            for (int i = 0; i < bagSlots.Count; i++)
            {
                Show(bagSlots[i], stats.Bag[i].id, stats.Bag[i].count, pickedFrom == stats.Bag && picked == i);
                if (!(pickedFrom == stats.Bag && picked == i) && stats.IsEquipped(stats.Bag[i].id)) bagSlots[i].frame.color = EquippedColor;
            }
            if (side == Side.Storage)
                for (int i = 0; i < storageSlots.Count; i++)
                    Show(storageSlots[i], stats.Storage[i].id, stats.Storage[i].count, pickedFrom == stats.Storage && picked == i);

            var item = PickedItem;
            var def = item != null ? Items.Get(item.id) : null;
            detailName.text = def != null ? $"> {def.name}  x{item.count}" : "> nothing picked";
            detailText.text = def != null ? def.description + WeaponLine(def) + (def.sellPrice > 0 ? $"\nsells for {def.sellPrice} coins each" : "")
                                          : "click an item to pick it up. click another slot to move it there.";
            useButton.interactable = def != null && def.Usable;
            useButton.GetComponentInChildren<Text>().text = def != null && def.Equippable ? (stats.IsEquipped(def.id) ? "unequip" : "equip") : "use";
            dropButton.interactable = def != null;
            sellButton.gameObject.SetActive(side == Side.Shop);
            sellButton.interactable = def != null && def.sellPrice > 0;
            sellButton.GetComponentInChildren<Text>().text = def != null && def.sellPrice > 0 ? $"sell {def.sellPrice * item.count}c" : "sell";
            foreach (var b in assignButtons) b.interactable = def != null && def.Usable;
            if (side == Side.Shop) shopInfo.text = $"you have {stats.progress.carried} coins";
        }

        static string WeaponLine(ItemDef def)
        {
            if (def.kind == ItemKind.Shield) return $"\nguard blocks {Mathf.RoundToInt(def.guard * 100)}% of a hit";
            if (def.kind != ItemKind.Weapon) return "";
            string pace = def.speed >= 1.1f ? "fast" : def.speed <= 0.85f ? "slow" : "normal";
            return $"\ndamage {def.damage}  heavy x{def.heavy}  {pace} swings" + (def.reach >= 1.25f ? "  long reach" : "");
        }

        static void Show(Slot slot, string id, int count, bool isPicked)
        {
            var icon = Items.Icon(id);
            slot.icon.sprite = icon;
            slot.icon.enabled = icon != null && count > 0;
            slot.count.text = count > 1 ? count.ToString() : "";
            slot.frame.color = isPicked ? PickedColor : SlotColor;
        }
    }
}
