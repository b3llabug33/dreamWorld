using System;
using DreamWorld.Account;
using DreamWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace DreamWorld.Gameplay
{
    // The settings panel, opened from the pause menu (Esc -> settings). Same pixel-terminal look as the
    // other menus: each row is "name   < value >" or "name   [on/off]". Every change is applied and
    // saved straight away (Settings). "back" returns to the pause menu; Esc closes everything.
    public class SettingsMenu : MonoBehaviour
    {
        public static SettingsMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.panel != null && Instance.panel.gameObject.activeSelf;

        TerminalUI ui;
        RectTransform canvas, panel;
        GameObject backdrop;
        Action refresh;
        int row;

        const float RowStart = -112f, RowStep = 60f;

        public static void Open(Font titleFont, Font bodyFont)
        {
            if (Instance == null)
            {
                var go = new GameObject("SettingsMenu");
                Instance = go.AddComponent<SettingsMenu>();
                Instance.Build(titleFont, bodyFont);
            }
            Instance.Show(true);
        }

        public static void Close()
        {
            if (Instance != null) Instance.Show(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Build(Font titleFont, Font bodyFont)
        {
            ui = new TerminalUI(titleFont, bodyFont);
            canvas = ui.Canvas("SettingsCanvas", 8);   // above GameHUD (5) and the bag (7)
            canvas.SetParent(transform, false);

            // dims the game and hides the pause menu underneath; also catches stray clicks
            backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(canvas, false);
            backdrop.GetComponent<Image>().color = new Color(0.03f, 0.02f, 0.08f, 0.6f);
            TerminalUI.Stretch((RectTransform)backdrop.transform, 0);

            var size = new Vector2(880, 760);
            panel = ui.PanelBox(canvas, "Settings");
            TerminalUI.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            // the frame panels are slightly see-through; an opaque fill inside keeps the pause menu from showing through
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(panel, false);
            var fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(TerminalUI.Panel.r, TerminalUI.Panel.g, TerminalUI.Panel.b, 1f);
            fillImage.raycastTarget = false;
            TerminalUI.Stretch((RectTransform)fill.transform, 3, 3);
            var header = ui.Label(panel, "== SETTINGS ==", 38, TerminalUI.Dim);
            TerminalUI.Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(size.x - 40, 44));

            Stepper("master volume", () => Percent(Settings.MasterVolume), d => Settings.MasterVolume = Step(Settings.MasterVolume, d * 0.1f, 0f, 1f));
            Stepper("music volume", () => Percent(Settings.MusicVolume), d => Settings.MusicVolume = Step(Settings.MusicVolume, d * 0.1f, 0f, 1f));
            Stepper("sfx volume", () => Percent(Settings.SfxVolume), d => Settings.SfxVolume = Step(Settings.SfxVolume, d * 0.1f, 0f, 1f));
            Stepper("mouse sensitivity", () => Settings.MouseSensitivity.ToString("0.0"), d => Settings.MouseSensitivity = Step(Settings.MouseSensitivity, d * 0.2f, 0.4f, 6f));
            Toggle("invert mouse y", () => Settings.InvertY ? "on" : "off", () => Settings.InvertY = !Settings.InvertY);
            Toggle("sprint (shift)", () => Settings.ToggleSprint ? "toggle" : "hold", () => Settings.ToggleSprint = !Settings.ToggleSprint);
            Stepper("pixel size", () => $"{Settings.PixelNames[Settings.PixelLevel]} {Settings.PixelHeight}",
                    d => Settings.PixelLevel = Mathf.Clamp(Settings.PixelLevel + d, 0, Settings.PixelHeights.Length - 1));
            Toggle("fullscreen", () => Settings.Fullscreen ? "on" : "off", () => Settings.Fullscreen = !Settings.Fullscreen);
            Toggle("controls hint", () => Settings.ShowControlsHint ? "on" : "off", () => Settings.ShowControlsHint = !Settings.ShowControlsHint);

            var back = ui.Button(panel, "back", 34, () => Show(false));
            TerminalUI.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(240, 60));

            refresh?.Invoke();
            Show(false);
        }

        // ---------- rows ----------

        Text RowLabel(string name)
        {
            float y = RowStart - row * RowStep;
            var label = ui.Label(panel, name, 32, TerminalUI.Text, TextAnchor.MiddleLeft);
            TerminalUI.Place(label.rectTransform, new Vector2(0, 1), new Vector2(56, y), new Vector2(420, 48), new Vector2(0, 0.5f));
            return label;
        }

        // name   < value >
        void Stepper(string name, Func<string> value, Action<int> change)
        {
            RowLabel(name);
            float y = RowStart - row * RowStep;
            row++;
            var shown = ui.Label(panel, "", 32, TerminalUI.Accent);
            TerminalUI.Place(shown.rectTransform, new Vector2(1, 1), new Vector2(-206, y), new Vector2(220, 48));
            var less = ui.Button(panel, "<", 34, () => Changed(() => change(-1)));
            TerminalUI.Place((RectTransform)less.transform, new Vector2(1, 1), new Vector2(-354, y), new Vector2(60, 48));
            var more = ui.Button(panel, ">", 34, () => Changed(() => change(1)));
            TerminalUI.Place((RectTransform)more.transform, new Vector2(1, 1), new Vector2(-58, y), new Vector2(60, 48));
            refresh += () => shown.text = value();
        }

        // name   [value]  (click to flip)
        void Toggle(string name, Func<string> value, Action flip)
        {
            RowLabel(name);
            float y = RowStart - row * RowStep;
            row++;
            var button = ui.Button(panel, "", 32, () => Changed(flip));
            TerminalUI.Place((RectTransform)button.transform, new Vector2(1, 1), new Vector2(-206, y), new Vector2(240, 48));
            var text = button.GetComponentInChildren<Text>();
            text.color = TerminalUI.Accent;
            refresh += () => text.text = value();
        }

        void Changed(Action change)
        {
            change();
            Settings.Apply();
            Settings.Save();
            refresh?.Invoke();
        }

        static float Step(float value, float delta, float min, float max) =>
            Mathf.Clamp(Mathf.Round((value + delta) * 100f) / 100f, min, max);

        static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

        // ---------- open / close ----------

        void Show(bool open)
        {
            panel.gameObject.SetActive(open);
            backdrop.SetActive(open);
            if (open) refresh?.Invoke();
        }

        void Update()
        {
            if (!IsOpen) return;
            // The pause menu stays open underneath. If it went away (Esc closes every menu), go with it.
            if (GameHUD.Instance == null || !GameHUD.Instance.MenuOpen) { Show(false); return; }
            ThirdPersonCamera.CursorFree = true;
            var local = PlayerStats.Local;
            var player = local != null ? local.GetComponent<PlayerController>() : null;
            if (player != null) player.InputEnabled = false;
        }
    }
}
