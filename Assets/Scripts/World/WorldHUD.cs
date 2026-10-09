using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DreamWorld.World
{
    // In-world overlay in the same pixel-terminal style as the creator:
    // a typed "> entering: <area>" banner that fades out, and a small controls hint.
    // (Bars, menus and Esc live in GameHUD.)
    public class WorldHUD : MonoBehaviour
    {
        public string areaName = "mushroom_hollow";
        public Font titleFont;
        public Font bodyFont;
        public string creatorScene = "CharacterCreation";

        public Color textColor = new Color(1f, 0.96f, 0.92f, 1f);
        public Color dimColor = new Color(0.78f, 0.74f, 0.92f, 1f);
        public Color accentColor = new Color(0.98f, 0.66f, 0.78f, 1f);

        Text banner, subtitle, status;
        CanvasGroup bannerGroup;
        float timer;
        string fullBanner;

        void Start()
        {
            if (bodyFont == null) bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (titleFont == null) titleFont = bodyFont;

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var canvas = (RectTransform)canvasGo.transform;

            var group = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup));
            group.transform.SetParent(canvas, false);
            bannerGroup = group.GetComponent<CanvasGroup>();
            var groupRect = (RectTransform)group.transform;
            groupRect.anchorMin = groupRect.anchorMax = new Vector2(0.5f, 0.78f);
            groupRect.sizeDelta = new Vector2(1400, 200);

            fullBanner = "> entering: " + areaName;
            banner = MakeText(groupRect, fullBanner, bodyFont, 64, textColor, new Vector2(0, 30));
            banner.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
            subtitle = MakeText(groupRect, "a cozy clearing in the dream woods", bodyFont, 34, dimColor, new Vector2(0, -36));

            var hint = MakeText(canvas, "WASD move   mouse look   space: roll (hold: sprint)   F jump   click: attack   shift+click: heavy   right click: guard   Q lock on   E use   R eat   L level up   esc menu",
                                bodyFont, 22, dimColor, Vector2.zero);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0);
            hint.rectTransform.anchoredPosition = new Vector2(0, 30);
            banner.text = "";

            status = MakeText(canvas, "", bodyFont, 30, accentColor, Vector2.zero);
            status.alignment = TextAnchor.UpperRight;
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(1, 1);
            status.rectTransform.pivot = new Vector2(1, 1);
            status.rectTransform.sizeDelta = new Vector2(700, 40);
            status.rectTransform.anchoredPosition = new Vector2(-40, -30);
        }

        // Small status line in the top-right corner (used for multiplayer: "online: host | players: 2").
        public void SetStatus(string text)
        {
            if (status != null) status.text = text;
        }

        Text MakeText(RectTransform parent, string content, Font font, int size, Color color, Vector2 pos)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = content;
            var r = t.rectTransform;
            r.sizeDelta = new Vector2(1600, size * 1.6f);
            r.anchoredPosition = pos;
            return t;
        }

        void Update()
        {
            timer += Time.deltaTime;
            // type the banner out letter by letter, with a blinking cursor, then fade it all away
            int letters = Mathf.Clamp((int)((timer - 0.5f) * 22f), 0, fullBanner.Length);
            bool cursor = letters < fullBanner.Length || timer % 1f < 0.5f;
            banner.text = fullBanner.Substring(0, letters) + (cursor ? "_" : " ");
            subtitle.color = new Color(dimColor.r, dimColor.g, dimColor.b, Mathf.Clamp01(timer - 2f));
            bannerGroup.alpha = 1f - Mathf.Clamp01((timer - 5.5f) / 1.5f);
        }
    }
}
