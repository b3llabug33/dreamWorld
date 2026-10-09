using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DreamWorld.CharacterCreation
{
    // Builds the character-creator screen in code when the scene starts:
    //   left:   category tabs (Skin / Hair / Top / Bottom / Shoes)
    //   bottom: "< style 1/3 >" picker + a row of color dots for the selected category
    //   sides:  turn buttons for the turntable
    //   corner: Ready button (saves the character)
    // Building it in code keeps the skeleton easy to change; it can become a prefab once the look settles.
    public class CharacterCreatorUI : MonoBehaviour
    {
        public CharacterAppearance appearance;
        public CharacterTurntable turntable;

        [Header("Theme")]
        public Color panelColor = new Color(0.16f, 0.12f, 0.26f, 0.85f);  // night purple
        public Color buttonColor = new Color(0.36f, 0.30f, 0.52f, 1f);    // dusty lavender
        public Color selectedColor = new Color(0.96f, 0.66f, 0.76f, 1f);  // soft pink
        public Color textColor = new Color(1f, 0.96f, 0.92f, 1f);         // cream

        OutfitCategory currentCategory = OutfitCategory.Hair;
        readonly Dictionary<OutfitCategory, Image> tabImages = new Dictionary<OutfitCategory, Image>();
        readonly List<GameObject> colorDots = new List<GameObject>();
        RectTransform colorRow;
        Text styleLabel;
        Text statusLabel;
        Font font;
        Sprite roundedSprite;
        Sprite circleSprite;

        void Start()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            roundedSprite = MakeRoundedSprite(64, 18);
            circleSprite = MakeRoundedSprite(64, 32);

            EnsureEventSystem();
            var canvas = BuildCanvas();
            BuildTitle(canvas);
            BuildTabs(canvas);
            BuildPickerPanel(canvas);
            BuildTurnButtons(canvas);
            BuildReadyButton(canvas);

            appearance.Changed += Refresh;
            SelectCategory(currentCategory);
        }

        void OnDestroy()
        {
            if (appearance != null) appearance.Changed -= Refresh;
        }

        // ---------- layout ----------

        RectTransform BuildCanvas()
        {
            var go = new GameObject("CreatorCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        void BuildTitle(RectTransform canvas)
        {
            var title = MakeText(canvas, "Title", "make you", 64);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(900, 100));
        }

        void BuildTabs(RectTransform canvas)
        {
            var panel = MakePanel(canvas, "Tabs");
            Place(panel, new Vector2(0f, 0.5f), new Vector2(210, 0), new Vector2(300, 520));

            var categories = (OutfitCategory[])System.Enum.GetValues(typeof(OutfitCategory));
            for (int i = 0; i < categories.Length; i++)
            {
                var category = categories[i];
                var button = MakeButton(panel, category.ToString().ToLower(), 36, () => SelectCategory(category));
                Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0, -70 - i * 95), new Vector2(240, 76));
                tabImages[category] = button.GetComponent<Image>();
            }
        }

        void BuildPickerPanel(RectTransform canvas)
        {
            var panel = MakePanel(canvas, "Picker");
            Place(panel, new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(760, 220));

            var prev = MakeButton(panel, "<", 44, () => appearance.ChangeStyle(currentCategory, -1));
            Place((RectTransform)prev.transform, new Vector2(0.5f, 1f), new Vector2(-230, -60), new Vector2(84, 76));
            var next = MakeButton(panel, ">", 44, () => appearance.ChangeStyle(currentCategory, +1));
            Place((RectTransform)next.transform, new Vector2(0.5f, 1f), new Vector2(230, -60), new Vector2(84, 76));

            styleLabel = MakeText(panel, "StyleLabel", "", 36);
            Place(styleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(360, 76));

            colorRow = new GameObject("Colors", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            colorRow.SetParent(panel, false);
            Place(colorRow, new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(700, 70));
            var layout = colorRow.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        }

        void BuildTurnButtons(RectTransform canvas)
        {
            var left = MakeButton(canvas, "< turn", 32, () => turntable.Turn(-1));
            Place((RectTransform)left.transform, new Vector2(0.5f, 0.5f), new Vector2(-380, 0), new Vector2(170, 70));
            var right = MakeButton(canvas, "turn >", 32, () => turntable.Turn(+1));
            Place((RectTransform)right.transform, new Vector2(0.5f, 0.5f), new Vector2(380, 0), new Vector2(170, 70));
        }

        void BuildReadyButton(RectTransform canvas)
        {
            var ready = MakeButton(canvas, "ready!", 44, OnReady);
            ready.GetComponent<Image>().color = selectedColor;
            ready.GetComponentInChildren<Text>().color = panelColor;
            Place((RectTransform)ready.transform, new Vector2(1f, 0f), new Vector2(-200, 110), new Vector2(260, 100));

            statusLabel = MakeText(canvas, "Status", "", 28);
            Place(statusLabel.rectTransform, new Vector2(1f, 0f), new Vector2(-200, 200), new Vector2(360, 50));
        }

        // ---------- behavior ----------

        void SelectCategory(OutfitCategory category)
        {
            currentCategory = category;
            foreach (var pair in tabImages)
                pair.Value.color = pair.Key == category ? selectedColor : buttonColor;
            RebuildColorDots();
            Refresh();
        }

        void RebuildColorDots()
        {
            foreach (var dot in colorDots) Destroy(dot);
            colorDots.Clear();

            var slot = appearance.GetSlot(currentCategory);
            if (slot == null) return;
            for (int i = 0; i < slot.colors.Length; i++)
            {
                int index = i;
                // A white ring behind each dot; it's only visible on the selected color.
                var ring = new GameObject("Dot" + i, typeof(RectTransform), typeof(Image));
                ring.transform.SetParent(colorRow, false);
                ((RectTransform)ring.transform).sizeDelta = new Vector2(64, 64);
                var ringImage = ring.GetComponent<Image>();
                ringImage.sprite = circleSprite;

                var dot = new GameObject("Color", typeof(RectTransform), typeof(Image), typeof(Button));
                dot.transform.SetParent(ring.transform, false);
                ((RectTransform)dot.transform).sizeDelta = new Vector2(50, 50);
                var dotImage = dot.GetComponent<Image>();
                dotImage.sprite = circleSprite;
                dotImage.color = slot.colors[i];
                dot.GetComponent<Button>().onClick.AddListener(() => appearance.SetColor(currentCategory, index));

                colorDots.Add(ring);
            }
        }

        void Refresh()
        {
            var slot = appearance.GetSlot(currentCategory);
            if (slot == null) return;
            styleLabel.text = slot.styles.Count > 0
                ? $"style {slot.styleIndex + 1} / {slot.styles.Count}"
                : "no styles yet";
            for (int i = 0; i < colorDots.Count; i++)
                colorDots[i].GetComponent<Image>().color = i == slot.colorIndex ? textColor : Color.clear;
            statusLabel.text = "";
        }

        void OnReady()
        {
            CharacterSave.Save(appearance);
            statusLabel.text = "saved!";
            // TODO: load the next scene (house / world) once it exists.
            Debug.Log("[dreamWorld] Character saved.");
        }

        // ---------- small UI helpers ----------

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        RectTransform MakePanel(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = panelColor;
            return (RectTransform)go.transform;
        }

        Text MakeText(RectTransform parent, string name, string content, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = textColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = content;
            return text;
        }

        Button MakeButton(RectTransform parent, string label, int fontSize, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = buttonColor;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var text = MakeText((RectTransform)go.transform, "Label", label, fontSize);
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return button;
        }

        // Generates a white rounded-rectangle sprite (radius = size/2 gives a circle) with
        // 9-slice borders, so we don't need any image files for the placeholder UI.
        static Sprite MakeRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Distance from the pixel to the nearest point of the inner (unrounded) rectangle.
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte alpha = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            float border = Mathf.Min(radius, size / 2f - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }
}
