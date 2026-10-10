using System.Collections.Generic;
using DreamWorld.Account;
using DreamWorld.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DreamWorld.Gameplay
{
    // The in-world game HUD, in the creator's pixel-terminal style:
    //   top-left: health / magic / stamina bars (Elden Ring style: they get longer as you level), food + snacks,
    //             level and XP;  bottom-right: coins carried and in the bank;  zone name when it changes;
    //   "[E] ..." prompt near things you can use; small health bars over monsters you're fighting;
    //   a lock-on marker. Menus: L = level up, Esc = pause, E at the home computer = bank.
    //   Dying shows "you woke up" while you're sent home.
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }
        public Font titleFont, bodyFont;
        public string creatorScene = "CharacterCreation";

        static readonly Color HealthColor = new Color(0.86f, 0.24f, 0.36f);
        static readonly Color MagicColor = new Color(0.42f, 0.52f, 1f);
        static readonly Color StaminaColor = new Color(0.45f, 0.86f, 0.55f);
        static readonly Color FoodColor = new Color(1f, 0.72f, 0.36f);
        static readonly Color XpColor = new Color(0.98f, 0.66f, 0.78f);
        static readonly Color ChipColor = new Color(1f, 0.92f, 0.6f);
        static readonly Color SafeColor = new Color(0.55f, 1f, 0.7f);
        static readonly Color DangerColor = new Color(1f, 0.5f, 0.6f);

        class Bar { public RectTransform back; public RectTransform fill, chip; public float chipValue = 1f; }

        TerminalUI ui;
        RectTransform canvas;
        Bar health, magic, stamina, food, xp;
        Text levelLabel, foodLabel, coinsLabel, zoneLabel, zoneToast, promptLabel, deathTitle, deathSub;
        CanvasGroup zoneToastGroup, deathGroup;
        RectTransform lockMarker;
        readonly List<(RectTransform root, RectTransform fill)> enemyBars = new List<(RectTransform, RectTransform)>();
        readonly List<(Text text, float time, bool talk)> toasts = new List<(Text, float, bool)>();
        RectTransform toastRoot;
        RectTransform levelPanel, bankPanel, pausePanel;
        Text levelPoints, bankLabel;
        readonly Text[] attributeLabels = new Text[5];
        string lastZone = "?";
        float zoneToastTime = -99f;
        PlayerStats stats;
        PlayerController player;

        static readonly string[] AttributeInfo =
        {
            "health    more max health",
            "stamina   more stamina for rolls, sprints and swings",
            "magic     more magic (spells come later)",
            "strength  hit harder",
            "luck      more coins from everything",
        };

        void Awake() => Instance = this;
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ThirdPersonCamera.CursorFree = false;
            Monster.Killed -= OnKilled;
            if (stats != null) stats.LeveledUp -= OnLevelUp;
        }

        void Start()
        {
            ui = new TerminalUI(titleFont, bodyFont);
            canvas = ui.Canvas("GameHUD", 5);

            health = MakeBar(new Vector2(48, -44), 20, HealthColor);
            magic = MakeBar(new Vector2(48, -72), 12, MagicColor);
            stamina = MakeBar(new Vector2(48, -92), 12, StaminaColor);
            food = MakeBar(new Vector2(48, -118), 8, FoodColor);
            foodLabel = Corner(ui.Label(canvas, "", 24, TerminalUI.Dim, TextAnchor.MiddleLeft), new Vector2(0, 1), new Vector2(262, -122), new Vector2(400, 30));
            levelLabel = Corner(ui.Label(canvas, "", 30, TerminalUI.Text, TextAnchor.MiddleLeft), new Vector2(0, 1), new Vector2(48, -154), new Vector2(500, 36));
            xp = MakeBar(new Vector2(48, -178), 6, XpColor);
            zoneLabel = Corner(ui.Label(canvas, "", 24, SafeColor, TextAnchor.MiddleLeft), new Vector2(0, 1), new Vector2(48, -204), new Vector2(500, 30));
            coinsLabel = Corner(ui.Label(canvas, "", 34, TerminalUI.Text, TextAnchor.MiddleRight), new Vector2(1, 0), new Vector2(-48, 40), new Vector2(700, 40), new Vector2(1, 0));

            var toastGo = new GameObject("ZoneToast", typeof(RectTransform), typeof(CanvasGroup));
            toastGo.transform.SetParent(canvas, false);
            zoneToastGroup = toastGo.GetComponent<CanvasGroup>();
            TerminalUI.Place((RectTransform)toastGo.transform, new Vector2(0.5f, 0.86f), Vector2.zero, new Vector2(1200, 60));
            zoneToast = ui.Label((RectTransform)toastGo.transform, "", 46, TerminalUI.Text);
            TerminalUI.Stretch(zoneToast.rectTransform, 0);
            zoneToast.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);

            promptLabel = ui.Label(canvas, "", 34, TerminalUI.Text);
            TerminalUI.Place(promptLabel.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 188), new Vector2(900, 44));   // just above the hotbar (y 74-146)
            promptLabel.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);

            toastRoot = new GameObject("Toasts", typeof(RectTransform)).GetComponent<RectTransform>();
            toastRoot.SetParent(canvas, false);
            TerminalUI.Place(toastRoot, new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(1000, 200));

            lockMarker = Solid(canvas, "LockOn", new Vector2(14, 14), Color.white);
            lockMarker.gameObject.AddComponent<Outline>().effectColor = TerminalUI.Accent;
            lockMarker.gameObject.SetActive(false);

            var inventory = gameObject.AddComponent<InventoryUI>();
            inventory.titleFont = titleFont;
            inventory.bodyFont = bodyFont;

            BuildLevelPanel();
            BuildBankPanel();
            BuildPausePanel();
            BuildDeathScreen();
        }

        // ---------- building blocks ----------

        RectTransform Solid(RectTransform parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var r = (RectTransform)go.transform;
            r.sizeDelta = size;
            return r;
        }

        Bar MakeBar(Vector2 topLeft, float height, Color color)
        {
            var bar = new Bar();
            bar.back = Solid(canvas, "Bar", new Vector2(100, height + 6), new Color(0.05f, 0.04f, 0.1f, 0.75f));
            TerminalUI.Place(bar.back, new Vector2(0, 1), topLeft, new Vector2(100, height + 6), new Vector2(0, 1));
            bar.back.gameObject.AddComponent<Outline>().effectColor = new Color(0.78f, 0.72f, 0.98f, 0.6f);
            bar.chip = Solid(bar.back, "Chip", Vector2.zero, ChipColor);
            bar.fill = Solid(bar.back, "Fill", Vector2.zero, color);
            foreach (var r in new[] { bar.chip, bar.fill })
            {
                r.anchorMin = new Vector2(0, 0);
                r.anchorMax = new Vector2(0, 1);
                r.pivot = new Vector2(0, 0.5f);
                r.offsetMin = new Vector2(3, 3);
                r.offsetMax = new Vector2(3, -3);
            }
            return bar;
        }

        static void SetBar(Bar bar, float value, float max, float width)
        {
            float f = max > 0 ? Mathf.Clamp01(value / max) : 0f;
            bar.back.sizeDelta = new Vector2(width + 6, bar.back.sizeDelta.y);
            // the yellow "chip" lingers behind the fill, then drains down (shows how much a hit took)
            bar.chipValue = f > bar.chipValue ? f : Mathf.MoveTowards(bar.chipValue, f, Time.deltaTime * 0.5f);
            bar.fill.sizeDelta = new Vector2(width * f, bar.fill.sizeDelta.y);
            bar.chip.sizeDelta = new Vector2(width * bar.chipValue, bar.chip.sizeDelta.y);
        }

        static Text Corner(Text t, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            TerminalUI.Place(t.rectTransform, anchor, pos, size, pivot ?? new Vector2(0, 0.5f));
            t.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);
            return t;
        }

        RectTransform Panel(string name, Vector2 size, string header)
        {
            var p = ui.PanelBox(canvas, name);
            TerminalUI.Place(p, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var h = ui.Label(p, header, 38, TerminalUI.Dim);
            TerminalUI.Place(h.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(size.x - 40, 44));
            p.gameObject.SetActive(false);
            return p;
        }

        void BuildLevelPanel()
        {
            levelPanel = Panel("LevelUp", new Vector2(980, 560), "== LEVEL UP ==");
            levelPoints = ui.Label(levelPanel, "", 32, TerminalUI.Accent);
            TerminalUI.Place(levelPoints.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(900, 40));
            for (int i = 0; i < 5; i++)
            {
                var attribute = (Attribute)i;
                attributeLabels[i] = ui.Label(levelPanel, "", 30, TerminalUI.Text, TextAnchor.MiddleLeft);
                TerminalUI.Place(attributeLabels[i].rectTransform, new Vector2(0, 1), new Vector2(50, -150 - i * 66), new Vector2(760, 50), new Vector2(0, 0.5f));
                var plus = ui.Button(levelPanel, "+", 40, () => { stats?.SpendPoint(attribute); });
                TerminalUI.Place((RectTransform)plus.transform, new Vector2(1, 1), new Vector2(-60, -150 - i * 66), new Vector2(70, 54));
            }
            var done = ui.Button(levelPanel, "done", 34, () => SetPanel(levelPanel, false));
            TerminalUI.Place((RectTransform)done.transform, new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(220, 60));
        }

        void BuildBankPanel()
        {
            bankPanel = Panel("Bank", new Vector2(760, 470), "== HOME COMPUTER ==");
            bankLabel = ui.Label(bankPanel, "", 32, TerminalUI.Text);
            bankLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            TerminalUI.Place(bankLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(680, 110));
            var deposit = ui.Button(bankPanel, "deposit all", 32, () => { stats?.Deposit(); SaveNow(); });
            TerminalUI.Place((RectTransform)deposit.transform, new Vector2(0.5f, 1), new Vector2(-165, -240), new Vector2(300, 60));
            var take10 = ui.Button(bankPanel, "take 10", 32, () => { stats?.Withdraw(10); SaveNow(); });
            TerminalUI.Place((RectTransform)take10.transform, new Vector2(0.5f, 1), new Vector2(165, -240), new Vector2(300, 60));
            var take100 = ui.Button(bankPanel, "take 100", 32, () => { stats?.Withdraw(100); SaveNow(); });
            TerminalUI.Place((RectTransform)take100.transform, new Vector2(0.5f, 1), new Vector2(-165, -312), new Vector2(300, 60));
            var takeAll = ui.Button(bankPanel, "take all", 32, () => { if (stats != null) stats.Withdraw(stats.progress.banked); SaveNow(); });
            TerminalUI.Place((RectTransform)takeAll.transform, new Vector2(0.5f, 1), new Vector2(165, -312), new Vector2(300, 60));
            var storage = ui.Button(bankPanel, "item storage", 32, () => { SetPanel(bankPanel, false); InventoryUI.Instance?.OpenStorage(); });
            TerminalUI.Place((RectTransform)storage.transform, new Vector2(0.5f, 0), new Vector2(-125, 44), new Vector2(230, 58));
            var close = ui.Button(bankPanel, "close", 32, () => SetPanel(bankPanel, false));
            TerminalUI.Place((RectTransform)close.transform, new Vector2(0.5f, 0), new Vector2(125, 44), new Vector2(230, 58));
        }

        void BuildPausePanel()
        {
            pausePanel = Panel("Pause", new Vector2(620, 466), "== PAUSED ==");
            var resume = ui.Button(pausePanel, "resume", 36, () => SetPanel(pausePanel, false));
            TerminalUI.Place((RectTransform)resume.transform, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(400, 66));
            var settings = ui.Button(pausePanel, "settings", 36, () => SettingsMenu.Open(titleFont, bodyFont));
            TerminalUI.Place((RectTransform)settings.transform, new Vector2(0.5f, 1), new Vector2(0, -216), new Vector2(400, 66));
            var back = ui.Button(pausePanel, "back to make you", 36, () =>
            {
                ThirdPersonCamera.CursorFree = false;
                SceneManager.LoadScene(creatorScene);
            });
            TerminalUI.Place((RectTransform)back.transform, new Vector2(0.5f, 1), new Vector2(0, -302), new Vector2(400, 66));
        }

        void BuildDeathScreen()
        {
            var go = new GameObject("Death", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(canvas, false);
            TerminalUI.Stretch((RectTransform)go.transform, 0);
            go.GetComponent<Image>().color = new Color(0.03f, 0.02f, 0.08f, 0.55f);
            deathGroup = go.GetComponent<CanvasGroup>();
            deathGroup.blocksRaycasts = false;
            deathGroup.alpha = 0f;
            // the Elden Ring "YOU DIED" band, but you're dreaming: dying wakes you up
            var band = Solid((RectTransform)go.transform, "Band", new Vector2(4000, 170), new Color(0, 0, 0, 0.7f));
            TerminalUI.Place(band, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(4000, 170));
            deathTitle = ui.Label((RectTransform)go.transform, "YOU WOKE UP", 120, new Color(0.95f, 0.38f, 0.5f), title: true);
            TerminalUI.Place(deathTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1600, 150));
            deathSub = ui.Label((RectTransform)go.transform, "", 34, TerminalUI.Dim);
            TerminalUI.Place(deathSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(1400, 44));
        }

        // ---------- public calls ----------

        public void Toast(string message) => AddToast(message, false);

        // A townsperson talking. Opening the bag or a shop clears these so they never sit on top of it.
        public void Say(string speaker, string line) => AddToast($"{speaker}: {line}", true);

        public void ClearDialogue()
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
                if (toasts[i].talk) { Destroy(toasts[i].text.gameObject); toasts.RemoveAt(i); }
        }

        void AddToast(string message, bool talk)
        {
            if (toastRoot == null) return;
            var t = ui.Label(toastRoot, message, 34, TerminalUI.Text);
            t.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            t.rectTransform.sizeDelta = new Vector2(1000, 40);
            toasts.Add((t, Time.time, talk));
        }

        public void OpenBank() => SetPanel(bankPanel, true);
        public void OpenLevelUp() => SetPanel(levelPanel, true);

        public void ShowDeath(int coinsLost)
        {
            CloseAll();
            deathSub.text = coinsLost > 0 ? $"you dropped {coinsLost} coins. deposit at your home computer to keep them safe"
                                          : "waking up at home...";
            deathTimer = 0f;
            deathShown = true;
        }

        public void HideDeath() => deathShown = false;

        bool deathShown;
        float deathTimer;

        void SaveNow()
        {
            var life = stats != null ? stats.GetComponent<PlayerLife>() : null;
            if (life != null) life.Save();
        }

        bool AnyPanel => levelPanel.gameObject.activeSelf || bankPanel.gameObject.activeSelf || pausePanel.gameObject.activeSelf;
        public bool MenuOpen => levelPanel != null && AnyPanel;   // the bag (InventoryUI) checks this

        void SetPanel(RectTransform panel, bool open)
        {
            if (open) CloseAll();
            panel.gameObject.SetActive(open);
            ThirdPersonCamera.CursorFree = AnyPanel || InventoryUI.IsOpen;
            if (player != null) player.InputEnabled = !AnyPanel && !InventoryUI.IsOpen && (stats == null || !stats.Dead);
        }

        void CloseAll()
        {
            foreach (var p in new[] { levelPanel, bankPanel, pausePanel }) p.gameObject.SetActive(false);
            ThirdPersonCamera.CursorFree = false;
        }

        // ---------- every frame ----------

        void Update()
        {
            if (stats != PlayerStats.Local)
            {
                if (stats != null) stats.LeveledUp -= OnLevelUp;
                stats = PlayerStats.Local;
                if (stats != null) stats.LeveledUp += OnLevelUp;
                player = stats != null ? stats.GetComponent<PlayerController>() : null;
                Monster.Killed -= OnKilled;
                Monster.Killed += OnKilled;
            }
            if (stats == null) return;
            var p = stats.progress;

            SetBar(health, stats.health, stats.MaxHealth, Mathf.Min(900f, 3.2f * stats.MaxHealth + 60f));
            SetBar(magic, stats.magic, stats.MaxMagic, Mathf.Min(900f, 8f * stats.MaxMagic + 40f));
            SetBar(stamina, stats.stamina, stats.MaxStamina, Mathf.Min(900f, 3.6f * stats.MaxStamina + 40f));
            SetBar(food, p.food, stats.MaxFood, 200f);
            SetBar(xp, p.xp, stats.XpToNext, 300f);
            foodLabel.text = stats.Starving ? "starving! eat something (1-4 / I)" : "food";
            foodLabel.color = stats.Starving ? DangerColor : TerminalUI.Dim;
            levelLabel.text = $"lv {p.level}   {p.xp}/{stats.XpToNext} xp" + (p.unspent > 0 ? $"   +{p.unspent} point{(p.unspent > 1 ? "s" : "")} [L]" : "");
            coinsLabel.text = $"coins {p.carried}    bank {p.banked}";

            // zone: name in the corner, a big toast when it changes
            var zone = ZoneMap.At(stats.transform.position);
            bool safe = zone != null && zone.kind == ZoneKind.Safe;
            string zoneName = zone != null ? zone.displayName : "the wilds";
            zoneLabel.text = safe ? $"{zoneName}  (safe)" : $"{zoneName}  (danger: monsters + pvp)";
            zoneLabel.color = safe ? SafeColor : DangerColor;
            if (zoneName != lastZone)
            {
                if (lastZone != "?")
                {
                    zoneToast.text = safe ? $"~ {zoneName} ~  safe zone" : $"~ {zoneName} ~  danger zone";
                    zoneToast.color = safe ? SafeColor : DangerColor;
                    zoneToastTime = Time.time;
                }
                lastZone = zoneName;
            }
            zoneToastGroup.alpha = Mathf.Clamp01(1f - (Time.time - zoneToastTime - 2f) / 1f);

            UpdatePrompt();
            UpdateEnemyBars();
            UpdateToasts();
            UpdatePanels();

            deathTimer += Time.deltaTime;
            deathGroup.alpha = deathShown ? Mathf.Clamp01(deathTimer / 0.8f) : Mathf.MoveTowards(deathGroup.alpha, 0f, Time.deltaTime * 2f);
        }

        void UpdatePrompt()
        {
            bool talking = Quests.DialogueUI.IsOpen;   // a conversation owns the keyboard (E, Esc, 1-6) while it's open
            var near = !stats.Dead && !AnyPanel && !InventoryUI.IsOpen && !talking ? Interactable.Nearest(stats.transform.position) : null;
            promptLabel.text = near != null ? $"[E] {near.Prompt(stats)}" : "";
            // E: use whatever's nearby, otherwise open/close the bag
            if (Input.GetKeyDown(KeyCode.E) && !stats.Dead && !AnyPanel && !talking)
            {
                if (InventoryUI.IsOpen) InventoryUI.Instance.Toggle();
                else if (near != null) near.Interact(stats);
                else InventoryUI.Instance?.Toggle();
            }
        }

        void UpdatePanels()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && !InventoryUI.IsOpen && !Quests.DialogueUI.IsOpen)
            {
                if (AnyPanel) { CloseAll(); SetPanel(pausePanel, false); }
                else SetPanel(pausePanel, true);
            }
            if (Input.GetKeyDown(KeyCode.L) && !stats.Dead && !InventoryUI.IsOpen && !Quests.DialogueUI.IsOpen) SetPanel(levelPanel, !levelPanel.gameObject.activeSelf);

            if (levelPanel.gameObject.activeSelf)
            {
                var p = stats.progress;
                levelPoints.text = $"lv {p.level}   points to spend: {p.unspent}";
                for (int i = 0; i < 5; i++)
                    attributeLabels[i].text = $"{AttributeInfo[i]}   [{p.points[i]}]";
            }
            if (bankPanel.gameObject.activeSelf)
                bankLabel.text = $"carrying {stats.progress.carried} coins\nsafe in the bank: {stats.progress.banked}";
        }

        void UpdateEnemyBars()
        {
            var cam = Camera.main;
            var shown = new List<(Monster m, bool locked)>();
            foreach (var m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
            {
                bool locked = player != null && ReferenceEquals(player.LockTarget, m);
                if (m.Alive && (locked || Time.time - m.LastHitTime < 5f)) shown.Add((m, locked));
            }
            while (enemyBars.Count < shown.Count)
            {
                var back = Solid(canvas, "EnemyBar", new Vector2(110, 12), new Color(0.05f, 0.04f, 0.1f, 0.8f));
                back.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
                var fill = Solid(back, "Fill", new Vector2(106, 8), HealthColor);
                fill.anchorMin = fill.anchorMax = new Vector2(0, 0.5f);
                fill.pivot = new Vector2(0, 0.5f);
                fill.anchoredPosition = new Vector2(2, 0);
                enemyBars.Add((back, fill));
            }
            for (int i = 0; i < enemyBars.Count; i++)
            {
                bool on = i < shown.Count && cam != null;
                Vector3 screen = on ? cam.WorldToScreenPoint(shown[i].m.transform.position + Vector3.up * shown[i].m.barHeight) : Vector3.zero;
                on &= screen.z > 0;
                enemyBars[i].root.gameObject.SetActive(on);
                if (!on) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var local);
                enemyBars[i].root.anchorMin = enemyBars[i].root.anchorMax = new Vector2(0.5f, 0.5f);
                enemyBars[i].root.anchoredPosition = local;
                enemyBars[i].fill.sizeDelta = new Vector2(106 * shown[i].m.HealthFraction, 8);
            }

            // lock-on marker on the target
            var target = player != null ? player.LockTarget : null;
            bool lockOn = target != null && cam != null;
            if (lockOn)
            {
                Vector3 s = cam.WorldToScreenPoint(target.Body.position + Vector3.up * 0.6f);
                lockOn = s.z > 0;
                if (lockOn)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, s, null, out var local);
                    lockMarker.anchorMin = lockMarker.anchorMax = new Vector2(0.5f, 0.5f);
                    lockMarker.anchoredPosition = local;
                }
            }
            lockMarker.gameObject.SetActive(lockOn);
        }

        void UpdateToasts()
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                float age = Time.time - toasts[i].time;
                if (age > 3.5f) { Destroy(toasts[i].text.gameObject); toasts.RemoveAt(i); continue; }
                toasts[i].text.color = new Color(1f, 0.96f, 0.92f, Mathf.Clamp01(3.5f - age));
            }
            // with the bag, a shop or a menu open, messages move up to the strip above the panels
            bool menus = InventoryUI.IsOpen || AnyPanel;
            toastRoot.anchorMin = toastRoot.anchorMax = menus ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.32f);
            toastRoot.anchoredPosition = menus ? new Vector2(0, -115) : Vector2.zero;
            for (int i = 0; i < toasts.Count; i++)   // newest at the bottom, older ones float up
                toasts[i].text.rectTransform.anchoredPosition = new Vector2(0, (toasts.Count - 1 - i) * 42 + (Time.time - toasts[i].time) * 6f);
        }

        void OnLevelUp(int level) => Toast($"LEVEL UP!  you're lv {level}  -  press L to spend your point");

        void OnKilled(Monster m, PlayerStats killer)
        {
            if (killer == stats) Toast($"defeated a {m.displayName}  +{m.xpReward} xp");
        }
    }
}
