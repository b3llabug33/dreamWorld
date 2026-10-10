using System.Collections.Generic;
using DreamWorld.Account;
using DreamWorld.Gameplay;
using DreamWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace DreamWorld.Net
{
    // Online chat, in the terminal style. Enter opens a text line at the bottom left, Enter sends it, Esc
    // cancels. The last few messages stay above it for a while, then fade. Everyone's messages also pop up as
    // a speech bubble over their head (NameTag.Say). Only exists in online sessions (NetLauncher makes it).
    public class ChatUI : MonoBehaviour
    {
        public static ChatUI Instance { get; private set; }
        public const int MaxLength = 120;
        const int Shown = 7;
        const float Linger = 14f;

        TerminalUI ui;
        RectTransform canvas, logRoot, inputBox;
        InputField input;
        readonly List<(Text text, float time)> lines = new List<(Text, float)>();
        int openedFrame = -1;

        public static void Create()
        {
            if (Instance != null) return;
            var hud = GameHUD.Instance;
            var go = new GameObject("ChatUI");
            Instance = go.AddComponent<ChatUI>();
            Instance.Build(hud != null ? hud.titleFont : null, hud != null ? hud.bodyFont : null);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Keys.Typing = false;
        }

        void Build(Font titleFont, Font bodyFont)
        {
            ui = new TerminalUI(titleFont, bodyFont);
            canvas = ui.Canvas("ChatUI", 6);
            logRoot = new GameObject("ChatLog", typeof(RectTransform)).GetComponent<RectTransform>();
            logRoot.SetParent(canvas, false);
            TerminalUI.Place(logRoot, new Vector2(0, 0), new Vector2(24, 250), new Vector2(620, 260), new Vector2(0, 0));

            inputBox = ui.PanelBox(canvas, "ChatInput");
            TerminalUI.Place(inputBox, new Vector2(0, 0), new Vector2(24, 196), new Vector2(620, 48), new Vector2(0, 0));
            var prompt = ui.Label(inputBox, ">", 28, TerminalUI.Accent, TextAnchor.MiddleLeft);
            TerminalUI.Place(prompt.rectTransform, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(30, 40), new Vector2(0, 0.5f));
            var text = ui.Label(inputBox, "", 26, TerminalUI.Text, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            TerminalUI.Place(text.rectTransform, new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(560, 40), new Vector2(0, 0.5f));
            var hint = ui.Label(inputBox, "say something (enter: send, esc: cancel)", 22, TerminalUI.Dim, TextAnchor.MiddleLeft);
            TerminalUI.Place(hint.rectTransform, new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(560, 40), new Vector2(0, 0.5f));
            input = inputBox.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = MaxLength;
            input.lineType = InputField.LineType.SingleLine;
            inputBox.gameObject.SetActive(false);
        }

        public bool Open => inputBox != null && inputBox.gameObject.activeSelf;

        void Update()
        {
            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (!Open)
            {
                bool busy = (GameHUD.Instance != null && GameHUD.Instance.MenuOpen) || InventoryUI.IsOpen
                            || Quests.DialogueUI.IsOpen || Gathering.CraftingUI.Busy;
                if (enter && !busy && PlayerStats.Local != null) Show(true);
            }
            else if (Time.frameCount != openedFrame)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) Show(false);
                else if (enter)
                {
                    string message = input.text.Trim();
                    if (message.Length > 0) NetPlayer.Mine?.Say(message);
                    Show(false);
                }
            }
            // old messages fade out
            for (int i = 0; i < lines.Count; i++)
            {
                float age = Time.time - lines[i].time;
                var c = lines[i].text.color;
                c.a = Open ? 1f : Mathf.Clamp01((Linger - age) / 2f);
                lines[i].text.color = c;
            }
        }

        void Show(bool open)
        {
            inputBox.gameObject.SetActive(open);
            Keys.Typing = open;
            var me = PlayerStats.Local != null ? PlayerStats.Local.GetComponent<PlayerController>() : null;
            if (me != null) me.InputEnabled = !open;
            if (open)
            {
                openedFrame = Time.frameCount;
                input.text = "";
                input.ActivateInputField();
                input.Select();
            }
            else input.DeactivateInputField();
        }

        // A message arrived (ours too: the server sends everything back to everyone).
        public void Add(string name, string message, bool mine)
        {
            var line = ui.Label(logRoot, "", 26, TerminalUI.Text, TextAnchor.LowerLeft);
            line.supportRichText = false;
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.text = name + ": " + message;
            line.color = mine ? new Color(0.98f, 0.66f, 0.78f) : TerminalUI.Text;
            lines.Add((line, Time.time));
            while (lines.Count > Shown)
            {
                Destroy(lines[0].text.gameObject);
                lines.RemoveAt(0);
            }
            // stack the lines upward from the bottom of the log
            float y = 0f;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var t = lines[i].text;
                TerminalUI.Place(t.rectTransform, new Vector2(0, 0), new Vector2(0, y), new Vector2(620, 30), new Vector2(0, 0));
                float h = Mathf.Max(30f, t.preferredHeight);   // wrapped height at the log's width
                TerminalUI.Place(t.rectTransform, new Vector2(0, 0), new Vector2(0, y), new Vector2(620, h), new Vector2(0, 0));
                y += h + 4f;
            }
        }
    }
}
