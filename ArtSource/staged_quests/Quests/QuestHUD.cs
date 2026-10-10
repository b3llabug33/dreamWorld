using System.Collections.Generic;
using System.Linq;
using DreamWorld.Account;
using DreamWorld.Gameplay;
using DreamWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace DreamWorld.Quests
{
    // Quest bits of the HUD: a small tracker (top right) with your current objective, a quest log (J),
    // "new quest" banners, and a bobbing "!" over NPCs who have something for you.
    public class QuestHUD : MonoBehaviour
    {
        public Font titleFont, bodyFont;

        TerminalUI ui;
        RectTransform canvas, logPanel, markerRoot;
        Text tracker, logText, banner;
        CanvasGroup bannerGroup;
        float bannerTime = -99f;
        readonly List<Text> markers = new List<Text>();
        QuestManager hooked;

        // NPCs register themselves so markers know where to float (name -> transform)
        public static readonly Dictionary<string, Transform> Npcs = new Dictionary<string, Transform>();

        void Start()
        {
            ui = new TerminalUI(titleFont, bodyFont);
            canvas = ui.Canvas("QuestHUD", 6);
            tracker = ui.Label(canvas, "", 26, TerminalUI.Text, TextAnchor.UpperRight);
            tracker.horizontalOverflow = HorizontalWrapMode.Wrap;
            TerminalUI.Place(tracker.rectTransform, new Vector2(1, 1), new Vector2(-40, -80), new Vector2(560, 160), new Vector2(1, 1));
            tracker.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);

            var bannerGo = new GameObject("QuestBanner", typeof(RectTransform), typeof(CanvasGroup));
            bannerGo.transform.SetParent(canvas, false);
            bannerGroup = bannerGo.GetComponent<CanvasGroup>();
            TerminalUI.Place((RectTransform)bannerGo.transform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1200, 60));
            banner = ui.Label((RectTransform)bannerGo.transform, "", 44, TerminalUI.Accent, title: true);
            TerminalUI.Stretch(banner.rectTransform, 0);
            banner.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            bannerGroup.alpha = 0;

            logPanel = ui.PanelBox(canvas, "QuestLog");
            TerminalUI.Place(logPanel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 620));
            var header = ui.Label(logPanel, "== QUESTS ==", 38, TerminalUI.Dim);
            TerminalUI.Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(800, 44));
            logText = ui.Label(logPanel, "", 30, TerminalUI.Text, TextAnchor.UpperLeft);
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.supportRichText = true;
            TerminalUI.Place(logText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(820, 480), new Vector2(0.5f, 1));
            var close = ui.Label(logPanel, "J: close", 24, TerminalUI.Dim);
            TerminalUI.Place(close.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(400, 30));
            logPanel.gameObject.SetActive(false);

            markerRoot = new GameObject("Markers", typeof(RectTransform)).GetComponent<RectTransform>();
            markerRoot.SetParent(canvas, false);
            TerminalUI.Stretch(markerRoot, 0);
        }

        void OnDestroy()
        {
            if (hooked != null) hooked.Notice -= ShowBanner;
        }

        void ShowBanner(string text)
        {
            banner.text = text;
            bannerTime = Time.time;
        }

        void Update()
        {
            var q = QuestManager.Instance;
            if (q != hooked)
            {
                if (hooked != null) hooked.Notice -= ShowBanner;
                hooked = q;
                if (q != null) q.Notice += ShowBanner;
            }
            bannerGroup.alpha = Mathf.Clamp01(1f - (Time.time - bannerTime - 2.5f));
            if (q == null) { tracker.text = ""; return; }

            bool busy = DialogueUI.IsOpen || InventoryUI.IsOpen;
            var active = q.state.questIds.Where(id => !q.Finished(id)).ToList();
            tracker.text = busy || active.Count == 0 ? "" :
                string.Join("\n", active.Take(2).Select(id => $"<color=#f9a8c8>{QuestBook.Get(id)?.title}</color>\n> {q.CurrentLog(id)}"));
            tracker.supportRichText = true;

            if (Input.GetKeyDown(KeyCode.J) && !DialogueUI.IsOpen && !InventoryUI.IsOpen)
            {
                bool open = !logPanel.gameObject.activeSelf;
                logPanel.gameObject.SetActive(open);
                ThirdPersonCamera.CursorFree = open;
                var pc = PlayerStats.Local != null ? PlayerStats.Local.GetComponent<PlayerController>() : null;
                if (pc != null) pc.InputEnabled = !open;
            }
            if (logPanel.gameObject.activeSelf)
            {
                var lines = new List<string>();
                foreach (var id in q.state.questIds)
                {
                    var def = QuestBook.Get(id);
                    if (def == null) continue;
                    lines.Add(q.Finished(id) ? $"<color=#9c94c8>[done] {def.title}</color>"
                                             : $"<color=#f9a8c8>{def.title}</color>\n   > {q.CurrentLog(id)}");
                }
                logText.text = lines.Count > 0 ? string.Join("\n\n", lines) : "no quests yet. talk to people around town!";
            }
            UpdateMarkers(q, busy);
        }

        void UpdateMarkers(QuestManager q, bool hide)
        {
            var cam = Camera.main;
            var wanted = hide || cam == null ? new List<string>() : q.MarkedNpcs().Distinct().Where(Npcs.ContainsKey).ToList();
            while (markers.Count < wanted.Count)
            {
                var t = ui.Label(markerRoot, "!", 56, new Color(1f, 0.85f, 0.4f), title: true);
                t.gameObject.AddComponent<Outline>().effectColor = new Color(0.2f, 0.1f, 0.2f, 0.9f);
                t.rectTransform.sizeDelta = new Vector2(60, 70);
                markers.Add(t);
            }
            for (int i = 0; i < markers.Count; i++)
            {
                bool on = i < wanted.Count && Npcs[wanted[i]] != null;
                Vector3 s = on ? cam.WorldToScreenPoint(Npcs[wanted[i]].position + Vector3.up * 2.3f) : Vector3.zero;
                on &= s.z > 0;
                markers[i].gameObject.SetActive(on);
                if (!on) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRoot, s, null, out var local);
                markers[i].rectTransform.anchorMin = markers[i].rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                markers[i].rectTransform.anchoredPosition = local + new Vector2(0, Mathf.Sin(Time.time * 3f) * 6f);
            }
        }
    }
}
