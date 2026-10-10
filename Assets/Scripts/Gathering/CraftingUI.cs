using System.Collections.Generic;
using System.Linq;
using DreamWorld.Account;
using DreamWorld.Gameplay;
using DreamWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace DreamWorld.Gathering
{
    // The workbench screen: recipes down the left, the picked one on the right (what it makes, which wood,
    // what it needs and how many you have), and a craft button. E or Esc closes it.
    public class CraftingUI : MonoBehaviour
    {
        public static CraftingUI Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.panel != null && Instance.panel.gameObject.activeSelf;
        // open, or closed this very frame (so the E that closed it doesn't reopen the bench in GameHUD)
        public static bool Busy => IsOpen || closedFrame == Time.frameCount;
        static int closedFrame = -1;

        TerminalUI ui;
        RectTransform canvas, panel, list, detail;
        Text title, description, woodLabel, needsText, status;
        Image icon;
        Button craftButton;
        readonly List<(Button button, Text label, Recipe recipe)> rows = new List<(Button, Text, Recipe)>();
        Recipe current;
        int woodIndex;
        PlayerController player;
        float openedAt;

        public static void Open()
        {
            if (Instance == null)
            {
                var hud = GameHUD.Instance;
                var go = new GameObject("CraftingUI");
                Instance = go.AddComponent<CraftingUI>();
                Instance.Build(hud != null ? hud.titleFont : null, hud != null ? hud.bodyFont : null);
            }
            Instance.Show(true);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build(Font titleFont, Font bodyFont)
        {
            ui = new TerminalUI(titleFont, bodyFont);
            canvas = ui.Canvas("CraftingUI", 8);
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            panel = (RectTransform)dim.transform;
            panel.SetParent(canvas, false);
            TerminalUI.Stretch(panel, 0);
            dim.GetComponent<Image>().color = new Color(0.03f, 0.02f, 0.07f, 0.55f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));   // opaque behind the frame
            fill.transform.SetParent(panel, false);
            fill.GetComponent<Image>().color = TerminalUI.Panel;
            TerminalUI.Place((RectTransform)fill.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1320, 780));
            var box = ui.PanelBox(panel, "WorkbenchBox");
            TerminalUI.Place(box, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1320, 780));
            var header = ui.Label(box, "== WORKBENCH ==", 40, TerminalUI.Accent, title: true);
            TerminalUI.Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -38), new Vector2(800, 50));

            list = new GameObject("Recipes", typeof(RectTransform)).GetComponent<RectTransform>();
            list.SetParent(box, false);
            TerminalUI.Place(list, new Vector2(0, 1), new Vector2(36, -86), new Vector2(430, 660), new Vector2(0, 1));
            string lastCategory = null;
            float y = 0;
            foreach (var r in Crafting.Recipes)
            {
                if (r.category != lastCategory)
                {
                    var cat = ui.Label(list, r.category, 24, TerminalUI.Dim, TextAnchor.MiddleLeft);
                    TerminalUI.Place(cat.rectTransform, new Vector2(0, 1), new Vector2(6, y - 12), new Vector2(400, 24), new Vector2(0, 0.5f));
                    y -= 26;
                    lastCategory = r.category;
                }
                var rr = r;
                var b = ui.Button(list, r.name, 24, () => Pick(rr));
                TerminalUI.Place((RectTransform)b.transform, new Vector2(0, 1), new Vector2(0, y), new Vector2(420, 33), new Vector2(0, 1));
                var label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                ((RectTransform)label.transform).offsetMin = new Vector2(16, 0);
                rows.Add((b, label, r));
                y -= 36;
            }

            detail = new GameObject("Detail", typeof(RectTransform)).GetComponent<RectTransform>();
            detail.SetParent(box, false);
            TerminalUI.Place(detail, new Vector2(1, 1), new Vector2(-40, -86), new Vector2(780, 660), new Vector2(1, 1));
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(detail, false);
            icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;
            TerminalUI.Place((RectTransform)iconGo.transform, new Vector2(0, 1), new Vector2(0, 0), new Vector2(128, 128), new Vector2(0, 1));
            title = ui.Label(detail, "", 40, TerminalUI.Text, TextAnchor.MiddleLeft, title: true);
            TerminalUI.Place(title.rectTransform, new Vector2(0, 1), new Vector2(150, -30), new Vector2(620, 50), new Vector2(0, 0.5f));
            description = ui.Label(detail, "", 28, TerminalUI.Dim, TextAnchor.UpperLeft);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            TerminalUI.Place(description.rectTransform, new Vector2(0, 1), new Vector2(150, -64), new Vector2(620, 90), new Vector2(0, 1));

            // wood picker: < oak (have 12) >
            var woodRow = new GameObject("WoodRow", typeof(RectTransform)).GetComponent<RectTransform>();
            woodRow.SetParent(detail, false);
            TerminalUI.Place(woodRow, new Vector2(0, 1), new Vector2(0, -170), new Vector2(780, 52), new Vector2(0, 1));
            var woodName = ui.Label(woodRow, "wood", 30, TerminalUI.Text, TextAnchor.MiddleLeft);
            TerminalUI.Place(woodName.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(120, 50), new Vector2(0, 0.5f));
            var left = ui.Button(woodRow, "<", 30, () => StepWood(-1));
            TerminalUI.Place((RectTransform)left.transform, new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(52, 46), new Vector2(0, 0.5f));
            woodLabel = ui.Label(woodRow, "", 30, TerminalUI.Accent);
            TerminalUI.Place(woodLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(180, 0), new Vector2(380, 50), new Vector2(0, 0.5f));
            var right = ui.Button(woodRow, ">", 30, () => StepWood(+1));
            TerminalUI.Place((RectTransform)right.transform, new Vector2(0, 0.5f), new Vector2(566, 0), new Vector2(52, 46), new Vector2(0, 0.5f));

            var needsHeader = ui.Label(detail, "needs", 28, TerminalUI.Dim, TextAnchor.MiddleLeft);
            TerminalUI.Place(needsHeader.rectTransform, new Vector2(0, 1), new Vector2(0, -250), new Vector2(300, 34), new Vector2(0, 0.5f));
            needsText = ui.Label(detail, "", 32, TerminalUI.Text, TextAnchor.UpperLeft);
            needsText.supportRichText = true;
            TerminalUI.Place(needsText.rectTransform, new Vector2(0, 1), new Vector2(20, -276), new Vector2(740, 200), new Vector2(0, 1));

            craftButton = ui.Button(detail, "craft", 36, CraftCurrent);
            TerminalUI.Place((RectTransform)craftButton.transform, new Vector2(0, 0), new Vector2(0, 70), new Vector2(300, 64), new Vector2(0, 0));
            status = ui.Label(detail, "", 28, TerminalUI.Accent, TextAnchor.MiddleLeft);
            TerminalUI.Place(status.rectTransform, new Vector2(0, 0), new Vector2(320, 102), new Vector2(460, 40), new Vector2(0, 0.5f));
            var close = ui.Button(box, "close (E)", 28, () => Show(false));
            TerminalUI.Place((RectTransform)close.transform, new Vector2(1, 0), new Vector2(-40, 30), new Vector2(220, 52), new Vector2(1, 0));

            panel.gameObject.SetActive(false);
            Pick(Crafting.Recipes[0]);
        }

        void Show(bool open)
        {
            if (open && InventoryUI.IsOpen) InventoryUI.Instance.Toggle();
            if (!open && IsOpen) closedFrame = Time.frameCount;
            panel.gameObject.SetActive(open);
            ThirdPersonCamera.CursorFree = open;
            player = PlayerStats.Local != null ? PlayerStats.Local.GetComponent<PlayerController>() : null;
            if (player != null) player.InputEnabled = !open;
            openedAt = Time.time;
            status.text = "";
            if (open) Refresh();
        }

        void Pick(Recipe r)
        {
            current = r;
            status.text = "";
            // start on the wood you have the most of
            var p = PlayerStats.Local;
            if (r.usesWood && p != null)
            {
                int best = 0, most = -1;
                for (int i = 0; i < Items.Woods.Length; i++)
                {
                    int have = Inventory.Count(p.Bag, "wood_" + Items.Woods[i].id);
                    if (have > most) { most = have; best = i; }
                }
                woodIndex = best;
            }
            Refresh();
        }

        void StepWood(int d)
        {
            woodIndex = (woodIndex + d + Items.Woods.Length) % Items.Woods.Length;
            status.text = "";
            Refresh();
        }

        string Wood => current != null && current.usesWood ? Items.Woods[woodIndex].id : null;

        void Refresh()
        {
            if (current == null) return;
            var p = PlayerStats.Local;
            string output = Crafting.OutputId(current, Wood);
            var def = Items.Get(output);
            icon.sprite = Items.Icon(output);
            icon.color = Items.IconTint(output);
            icon.enabled = icon.sprite != null;
            title.text = def != null ? def.name : current.name;
            description.text = current.description + (def != null && def.kind == ItemKind.Weapon ? $"\ndamage {def.damage}, reach {def.reach:0.##}" : "");
            woodLabel.transform.parent.gameObject.SetActive(current.usesWood);
            if (current.usesWood)
            {
                var w = Items.Woods[woodIndex];
                woodLabel.text = $"{w.name} (have {(p != null ? Inventory.Count(p.Bag, "wood_" + w.id) : 0)})";
            }
            var lines = new List<string>();
            foreach (var (item, count) in Crafting.Needs(current, Wood, p))
            {
                int have = p != null ? Inventory.Count(p.Bag, item) : 0;
                string name = Items.Get(item)?.name ?? item;
                if (!current.usesWood && item.StartsWith("wood_") && current.woodCount > 0) name = "any wood";
                string col = have >= count ? "#b8f0c0" : "#f08a9a";
                lines.Add($"<color={col}>{count} {name}   (have {have})</color>");
            }
            needsText.text = string.Join("\n", lines);
            craftButton.interactable = Crafting.CanCraft(current, Wood, p);
            foreach (var row in rows)
                row.label.color = row.recipe == current ? TerminalUI.Accent : (Crafting.CanCraft(row.recipe, row.recipe.usesWood ? Items.Woods[woodIndex].id : null, p) ? TerminalUI.Text : TerminalUI.Dim);
        }

        void CraftCurrent()
        {
            var p = PlayerStats.Local;
            if (current == null || p == null) return;
            string output = Crafting.OutputId(current, Wood);
            if (Crafting.Craft(current, Wood, p))
            {
                status.text = $"made a {Items.Get(output)?.name ?? output}!";
                GameHUD.Instance?.Toast($"crafted {Items.Get(output)?.name ?? output}");
            }
            else status.text = Crafting.CanCraft(current, Wood, p) ? "your bag is full" : "missing something";
            Refresh();
        }

        void Update()
        {
            if (!IsOpen) return;
            if (Time.time - openedAt > 0.2f && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))) Show(false);
        }
    }
}
