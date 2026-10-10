using System.Collections.Generic;
using DreamWorld.Account;
using DreamWorld.Gameplay;
using DreamWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace DreamWorld.Quests
{
    // The conversation box, in the terminal style: speaker name, text that types itself out, and
    // choice buttons (click, or press 1-6). Space / Enter / click skips the typing, then continues.
    // It sits low and centered, and closes the bag when it opens (they never overlap).
    public class DialogueUI : MonoBehaviour
    {
        public static DialogueUI Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.box != null && Instance.box.gameObject.activeSelf;

        public Font titleFont, bodyFont;
        public float lettersPerSecond = 45f;

        TerminalUI ui;
        RectTransform canvas, box;
        Text speaker, body, hint;
        readonly List<Button> choiceButtons = new List<Button>();
        Dialogue current;
        Dialogue.Node node;
        float typed;
        bool choicesShown;
        PlayerController player;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            ui = new TerminalUI(titleFont, bodyFont);
            canvas = ui.Canvas("DialogueUI", 9);
            box = ui.PanelBox(canvas, "DialogueBox");
            TerminalUI.Place(box, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1100, 300), new Vector2(0.5f, 0));
            speaker = ui.Label(box, "", 38, TerminalUI.Accent, TextAnchor.MiddleLeft, title: true);
            TerminalUI.Place(speaker.rectTransform, new Vector2(0, 1), new Vector2(40, -36), new Vector2(900, 44), new Vector2(0, 0.5f));
            body = ui.Label(box, "", 34, TerminalUI.Text, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            TerminalUI.Place(body.rectTransform, new Vector2(0, 1), new Vector2(40, -70), new Vector2(1020, 120), new Vector2(0, 1));
            hint = ui.Label(box, "", 24, TerminalUI.Dim, TextAnchor.MiddleRight);
            TerminalUI.Place(hint.rectTransform, new Vector2(1, 0), new Vector2(-30, 24), new Vector2(600, 30), new Vector2(1, 0.5f));
            box.gameObject.SetActive(false);
        }

        public void Show(Dialogue dialogue)
        {
            if (dialogue == null || dialogue.start == null || box == null) return;
            if (InventoryUI.IsOpen) InventoryUI.Instance.Toggle();   // never on top of the bag
            current = dialogue;
            box.gameObject.SetActive(true);
            player = PlayerStats.Local != null ? PlayerStats.Local.GetComponent<PlayerController>() : null;
            if (player != null) player.InputEnabled = false;
            ThirdPersonCamera.CursorFree = true;
            Go(dialogue.start);
        }

        void Go(string id)
        {
            ClearChoices();
            if (id == null || !current.nodes.TryGetValue(id, out node)) { Close(); return; }
            speaker.text = node.speaker;
            body.text = "";
            typed = 0f;
            choicesShown = false;
            node.onShow?.Invoke();
        }

        void Close()
        {
            box.gameObject.SetActive(false);
            var end = current?.onEnd;
            current = null;
            node = null;
            ClearChoices();
            if (player != null && (PlayerStats.Local == null || !PlayerStats.Local.Dead)) player.InputEnabled = true;
            ThirdPersonCamera.CursorFree = false;
            end?.Invoke();
        }

        void ClearChoices()
        {
            foreach (var b in choiceButtons) Destroy(b.gameObject);
            choiceButtons.Clear();
        }

        void ShowChoices()
        {
            choicesShown = true;
            for (int i = 0; i < node.choices.Count; i++)
            {
                var choice = node.choices[i];
                var b = ui.Button(box, $"{i + 1}. {choice.label}", 28, () => Pick(choice));
                int col = i % 3, row = i / 3;
                TerminalUI.Place((RectTransform)b.transform, new Vector2(0, 0), new Vector2(40 + col * 345, 120 - row * 58),
                                 new Vector2(330, 50), new Vector2(0, 0.5f));
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                TerminalUI.Stretch(b.GetComponentInChildren<Text>().rectTransform, 14);
                choiceButtons.Add(b);
            }
        }

        void Pick(Dialogue.Choice choice)
        {
            choice.onPick?.Invoke();
            Go(choice.next);
        }

        void Update()
        {
            if (!IsOpen || node == null) return;
            int full = node.text.Length;
            if (typed < full)
            {
                typed = Mathf.Min(full, typed + lettersPerSecond * Time.unscaledDeltaTime);
                body.text = node.text.Substring(0, (int)typed);
            }
            bool done = typed >= full;
            bool hasChoices = node.choices.Count > 0;
            if (done && hasChoices && !choicesShown) ShowChoices();
            hint.text = !done ? "space: skip" : hasChoices ? "pick one (1-6)" : node.next != null ? "space: next" : "space: close";

            bool advance = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                           Input.GetKeyDown(KeyCode.KeypadEnter) || (Input.GetMouseButtonDown(0) && !hasChoices);
            if (advance)
            {
                if (!done) { typed = full; body.text = node.text; }
                else if (!hasChoices) Go(node.next);
            }
            if (done && hasChoices)
                for (int i = 0; i < node.choices.Count && i < 6; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { Pick(node.choices[i]); break; }
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }
    }
}
