using DreamWorld.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace DreamWorld.Net
{
    // A player's name floating over their head (other players only), in the game's pixel font. Always faces
    // the camera, and fades out far away so a crowded town stays readable.
    public class NameTag : MonoBehaviour
    {
        Text label;
        Text bubble, bubbleShadow;
        CanvasGroup group;
        float bubbleUntil;

        public static NameTag Create(Transform player, string name)
        {
            var go = new GameObject("NameTag", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(player, false);
            go.transform.localPosition = new Vector3(0, 2.05f, 0);
            go.transform.localScale = Vector3.one * 0.006f;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(600, 80);
            var tag = go.AddComponent<NameTag>();
            tag.group = go.GetComponent<CanvasGroup>();

            var shadow = MakeText(go.transform, new Color(0.08f, 0.04f, 0.16f, 0.9f));
            ((RectTransform)shadow.transform).anchoredPosition = new Vector2(4, -4);
            tag.label = MakeText(go.transform, new Color(1f, 0.96f, 0.92f));
            tag.Set(name);
            // a speech bubble line above the name for chat messages
            tag.bubbleShadow = MakeText(go.transform, new Color(0.08f, 0.04f, 0.16f, 0.9f));
            tag.bubble = MakeText(go.transform, new Color(0.98f, 0.8f, 0.9f));
            foreach (var t in new[] { tag.bubbleShadow, tag.bubble })
            {
                t.fontSize = 40;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.alignment = TextAnchor.LowerCenter;
                ((RectTransform)t.transform).offsetMin = new Vector2(-200, 70);
                ((RectTransform)t.transform).offsetMax = new Vector2(200, 260);
                t.gameObject.SetActive(false);
            }
            ((RectTransform)tag.bubbleShadow.transform).anchoredPosition += new Vector2(3, -3);
            return tag;
        }

        static Text MakeText(Transform parent, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            var hud = GameHUD.Instance;
            text.font = hud != null && hud.titleFont != null ? hud.titleFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 52;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        public void Set(string name)
        {
            name = string.IsNullOrEmpty(name) ? "..." : name;
            foreach (var t in GetComponentsInChildren<Text>(true))
                if (t != bubble && t != bubbleShadow) t.text = name;
        }

        // A chat message shown over the head for a few seconds (longer messages stay a bit longer).
        public void Say(string message)
        {
            bubble.text = bubbleShadow.text = message;
            bubble.gameObject.SetActive(true);
            bubbleShadow.gameObject.SetActive(true);
            bubbleUntil = Time.time + 4f + message.Length * 0.05f;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            float d = Vector3.Distance(cam.transform.position, transform.position);
            group.alpha = Mathf.Clamp01((28f - d) / 8f);
            if (bubble.gameObject.activeSelf && Time.time > bubbleUntil)
            {
                bubble.gameObject.SetActive(false);
                bubbleShadow.gameObject.SetActive(false);
            }
        }
    }
}
