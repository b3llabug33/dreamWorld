using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DreamWorld.Account
{
    // Small builder for the pixel-terminal UI look (same theme as the character creator):
    // framed night panels, VT323 text, lavender frames, pink accents.
    public class TerminalUI
    {
        public static readonly Color Panel = new Color(0.07f, 0.06f, 0.13f, 0.9f);
        public static readonly Color Frame = new Color(0.78f, 0.72f, 0.98f, 1f);
        public static readonly Color Text = new Color(1f, 0.96f, 0.92f, 1f);
        public static readonly Color Dim = new Color(0.62f, 0.58f, 0.78f, 1f);
        public static readonly Color Accent = new Color(0.98f, 0.66f, 0.78f, 1f);

        public readonly Font titleFont, bodyFont;
        readonly Sprite frame, solid;

        public TerminalUI(Font title, Font body)
        {
            titleFont = title != null ? title : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bodyFont = body != null ? body : titleFont;
            frame = MakeFrameSprite();
            solid = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 100f);
        }

        public RectTransform Canvas(string name, int order)
        {
            if (Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect, float insetX, float insetY = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(insetX, insetY);
            rect.offsetMax = new Vector2(-insetX, -insetY);
        }

        public RectTransform PanelBox(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = frame;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.5f;
            return (RectTransform)go.transform;
        }

        public UnityEngine.UI.Text Label(RectTransform parent, string content, int size, Color color,
                                         TextAnchor anchor = TextAnchor.MiddleCenter, bool title = false)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<UnityEngine.UI.Text>();
            text.font = title ? titleFont : bodyFont;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        public Button Button(RectTransform parent, string label, int size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = frame;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.5f;
            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.85f, 0.92f);
            colors.pressedColor = Accent;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            Stretch(Label((RectTransform)go.transform, label, size, Text).rectTransform, 0);
            return button;
        }

        public InputField Field(RectTransform parent, string placeholder, bool password = false)
        {
            var go = new GameObject("Field_" + placeholder, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = frame;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.5f;
            var text = Label((RectTransform)go.transform, "", 36, Text, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            Stretch(text.rectTransform, 16);
            var hint = Label((RectTransform)go.transform, placeholder, 36, Dim, TextAnchor.MiddleLeft);
            Stretch(hint.rectTransform, 16);
            var field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = InputField.LineType.SingleLine;
            if (password)
            {
                field.contentType = InputField.ContentType.Password;
                field.asteriskChar = '*';
            }
            return field;
        }

        public void Scanlines(RectTransform canvas)
        {
            var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels(new[] { new Color(0, 0, 0, 0.35f), Color.clear, Color.clear, Color.clear });
            tex.Apply();
            var go = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(canvas, false);
            var raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            raw.color = new Color(1, 1, 1, 0.5f);
            raw.uvRect = new Rect(0, 0, 1, 1080 / 4f);
            Stretch(raw.rectTransform, 0);
        }

        static Sprite MakeFrameSprite()
        {
            const int size = 12, border = 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = x < border || y < border || x >= size - border || y >= size - border;
                bool corner = (x < border || x >= size - border) && (y < border || y >= size - border);
                tex.SetPixel(x, y, corner ? Color.clear : edge ? Frame : Panel);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border + 1, border + 1, border + 1, border + 1));
        }
    }
}
