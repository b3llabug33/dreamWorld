using System.Collections.Generic;
using DreamWorld.Net;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DreamWorld.CharacterCreation
{
    // Builds the "make you" character-creator screen in code when the scene starts.
    // Look: a cozy pixel terminal. Framed panels, pixel fonts, faint CRT scanlines.
    //   left:   == CATEGORY == list (click, or W/S / up/down)
    //   right:  == STYLE == picker (A/D / left/right) and == COLOR == swatches (Q/E)
    //   bottom: turn buttons + key hints, [ READY ] (Enter) saves the character
    public class CharacterCreatorUI : MonoBehaviour
    {
        public CharacterAppearance appearance;
        public CharacterTurntable turntable;
        [Tooltip("Scene loaded by READY.")]
        public string worldScene = "World_MushroomHollow";
        CreatorCameraZoom zoom;

        [Header("Fonts (fall back to Unity's built-in font)")]
        public Font titleFont;
        public Font bodyFont;

        [Header("Theme")]
        public Color panelColor = new Color(0.07f, 0.06f, 0.13f, 0.88f);  // night
        public Color frameColor = new Color(0.78f, 0.72f, 0.98f, 1f);     // lavender line
        public Color textColor = new Color(1f, 0.96f, 0.92f, 1f);         // cream
        public Color dimColor = new Color(0.62f, 0.58f, 0.78f, 1f);       // muted lavender
        public Color accentColor = new Color(0.98f, 0.66f, 0.78f, 1f);    // soft pink
        public Color hiliteColor = new Color(0.98f, 0.66f, 0.78f, 0.18f); // selected row glow

        OutfitCategory currentCategory = OutfitCategory.Hair;
        readonly Dictionary<OutfitCategory, (Text label, Image bar)> rows = new Dictionary<OutfitCategory, (Text, Image)>();
        readonly List<(Image ring, Image swatch)> swatches = new List<(Image, Image)>();
        OutfitCategory[] categories;
        RectTransform swatchGrid;
        Text styleName, styleCount, statusLabel, cursorLabel;
        Sprite frameSprite, solidSprite;
        float cursorTimer;

        GameObject eyeControls;
        Slider eyeSize, eyeSpacing, eyeHeight;
        bool syncingSliders;
        NetMode chosenMode;
        readonly Dictionary<NetMode, Image> modeButtons = new Dictionary<NetMode, Image>();
        InputField addressField;

        void Start()
        {
            // Servers and test clients started with -server/-host/-join skip the creator.
            if (NetSession.FromCommandLine(out _, out _) && Application.CanStreamedLevelBeLoaded(worldScene))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(worldScene);
                return;
            }
            if (titleFont == null) titleFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (bodyFont == null) bodyFont = titleFont;
            frameSprite = MakeFrameSprite();
            solidSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 100f);
            categories = (OutfitCategory[])System.Enum.GetValues(typeof(OutfitCategory));
            zoom = FindAnyObjectByType<CreatorCameraZoom>();

            EnsureEventSystem();
            var canvas = BuildCanvas();
            BuildTitle(canvas);
            BuildCategoryPanel(canvas);
            BuildStylePanel(canvas);
            BuildBottomBar(canvas);
            BuildNetRow(canvas);
            BuildScanlines(canvas);

            appearance.Changed += Refresh;
            SelectCategory(currentCategory);
        }

        void OnDestroy()
        {
            if (appearance != null) appearance.Changed -= Refresh;
        }

        void Update()
        {
            if (cursorLabel == null) return;                               // skipped Start (command-line boot)
            if (addressField != null && addressField.isFocused) return;    // typing an address, not shortcuts
            // blinking terminal cursor
            cursorTimer += Time.deltaTime;
            cursorLabel.text = "> character_setup.exe" + (cursorTimer % 1f < 0.5f ? "_" : " ");

            int c = System.Array.IndexOf(categories, currentCategory);
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                SelectCategory(categories[(c - 1 + categories.Length) % categories.Length]);
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                SelectCategory(categories[(c + 1) % categories.Length]);
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) appearance.ChangeStyle(currentCategory, -1);
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) appearance.ChangeStyle(currentCategory, +1);
            var slot = appearance.GetSlot(currentCategory);
            if (slot != null && Input.GetKeyDown(KeyCode.Q)) appearance.SetColor(currentCategory, (slot.colorIndex - 1 + slot.colors.Length) % slot.colors.Length);
            if (slot != null && Input.GetKeyDown(KeyCode.E)) appearance.SetColor(currentCategory, (slot.colorIndex + 1) % slot.colors.Length);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) OnReady();
            for (int i = 0; i < categories.Length && i < 10; i++)   // 1..9, then 0 for the 10th
                if (Input.GetKeyDown(i < 9 ? KeyCode.Alpha1 + i : KeyCode.Alpha0)) SelectCategory(categories[i]);
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
            var title = MakeText(canvas, "Title", "make you", titleFont, 96, textColor, TextAnchor.UpperLeft);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(64, -36), new Vector2(700, 120), new Vector2(0, 1));
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = accentColor;
            shadow.effectDistance = new Vector2(5, -5);

            cursorLabel = MakeText(canvas, "Cursor", "", bodyFont, 34, dimColor, TextAnchor.UpperLeft);
            Place(cursorLabel.rectTransform, new Vector2(0, 1), new Vector2(70, -150), new Vector2(700, 44), new Vector2(0, 1));
        }

        void BuildCategoryPanel(RectTransform canvas)
        {
            var panel = MakePanel(canvas, "Categories");
            float height = 96 + categories.Length * 54;
            Place(panel, new Vector2(0, 0.5f), new Vector2(64, 10), new Vector2(380, height), new Vector2(0, 0.5f));
            Header(panel, "== CATEGORY ==", -26);

            for (int i = 0; i < categories.Length; i++)
            {
                var category = categories[i];
                var row = MakeButton(panel, $"Row_{category}", () => SelectCategory(category), transparent: true);
                Place((RectTransform)row.transform, new Vector2(0.5f, 1), new Vector2(0, -92 - i * 54), new Vector2(340, 50));
                var bar = row.GetComponent<Image>();
                var label = MakeText((RectTransform)row.transform, "Label", "", bodyFont, 40, textColor, TextAnchor.MiddleLeft);
                Stretch(label.rectTransform, 18, 0);   // side padding only, so the 40px text fits
                rows[category] = (label, bar);
            }
        }

        void BuildStylePanel(RectTransform canvas)
        {
            var panel = MakePanel(canvas, "Style");
            Place(panel, new Vector2(1, 0.5f), new Vector2(-64, 10), new Vector2(460, 680), new Vector2(1, 0.5f));
            Header(panel, "== STYLE ==", -26);

            var prev = MakeButton(panel, "Prev", () => appearance.ChangeStyle(currentCategory, -1));
            Place((RectTransform)prev.transform, new Vector2(0, 1), new Vector2(54, -118), new Vector2(64, 64));
            Stretch(MakeText((RectTransform)prev.transform, "L", "<", bodyFont, 48, textColor, TextAnchor.MiddleCenter).rectTransform, 0);
            var next = MakeButton(panel, "Next", () => appearance.ChangeStyle(currentCategory, +1));
            Place((RectTransform)next.transform, new Vector2(1, 1), new Vector2(-54, -118), new Vector2(64, 64));
            Stretch(MakeText((RectTransform)next.transform, "R", ">", bodyFont, 48, textColor, TextAnchor.MiddleCenter).rectTransform, 0);

            styleName = MakeText(panel, "StyleName", "", bodyFont, 48, accentColor, TextAnchor.MiddleCenter);
            Place(styleName.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(280, 60));
            styleCount = MakeText(panel, "StyleCount", "", bodyFont, 30, dimColor, TextAnchor.MiddleCenter);
            Place(styleCount.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -156), new Vector2(280, 34));

            Header(panel, "== COLOR ==", -216);
            swatchGrid = new GameObject("Swatches", typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<RectTransform>();
            swatchGrid.SetParent(panel, false);
            Place(swatchGrid, new Vector2(0.5f, 1), new Vector2(0, -440), new Vector2(400, 380));
            var grid = swatchGrid.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(50, 50);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.childAlignment = TextAnchor.UpperCenter;

            BuildEyeControls(panel);
        }

        // Eye sliders (only shown on the eyes tab): size, spacing, height + reset.
        void BuildEyeControls(RectTransform panel)
        {
            eyeControls = new GameObject("EyeControls", typeof(RectTransform));
            var rect = (RectTransform)eyeControls.transform;
            rect.SetParent(panel, false);
            Stretch(rect, 0);
            Header(rect, "== SHAPE ==", -452);
            eyeSize = SliderRow(rect, "size", -500, 0.75f, 1.35f);
            eyeSpacing = SliderRow(rect, "spacing", -546, -0.06f, 0.06f);
            eyeHeight = SliderRow(rect, "height", -592, -0.05f, 0.06f);
            var reset = MakeButton(rect, "ResetEyes", () => { appearance.ResetEyes(); SyncSliders(); });
            Place((RectTransform)reset.transform, new Vector2(0.5f, 1), new Vector2(0, -642), new Vector2(220, 46));
            Stretch(MakeText((RectTransform)reset.transform, "L", "reset eyes", bodyFont, 30, textColor, TextAnchor.MiddleCenter).rectTransform, 0);
        }

        Slider SliderRow(RectTransform parent, string label, float y, float min, float max)
        {
            var text = MakeText(parent, label, label, bodyFont, 32, textColor, TextAnchor.MiddleLeft);
            Place(text.rectTransform, new Vector2(0.5f, 1), new Vector2(-130, y), new Vector2(120, 40));

            var go = new GameObject(label + "Slider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, new Vector2(0.5f, 1), new Vector2(60, y), new Vector2(250, 22));
            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(go.transform, false);
            Stretch((RectTransform)track.transform, 0);
            track.GetComponent<Image>().sprite = solidSprite;
            track.GetComponent<Image>().color = new Color(0, 0, 0, 0.55f);

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            Stretch((RectTransform)fillArea.transform, 3);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            ((RectTransform)fill.transform).sizeDelta = Vector2.zero;
            fill.GetComponent<Image>().sprite = solidSprite;
            fill.GetComponent<Image>().color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.6f);

            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            Stretch((RectTransform)handleArea.transform, 0);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            ((RectTransform)handle.transform).sizeDelta = new Vector2(16, 14);
            handle.GetComponent<Image>().sprite = solidSprite;
            handle.GetComponent<Image>().color = textColor;

            var slider = go.GetComponent<Slider>();
            slider.fillRect = (RectTransform)fill.transform;
            slider.handleRect = (RectTransform)handle.transform;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(_ => OnEyeSlider());
            return slider;
        }

        void OnEyeSlider()
        {
            if (!syncingSliders) appearance.SetEyes(eyeSize.value, eyeSpacing.value, eyeHeight.value);
        }

        void SyncSliders()
        {
            syncingSliders = true;
            eyeSize.value = appearance.eyeScale;
            eyeSpacing.value = appearance.eyeSpacing;
            eyeHeight.value = appearance.eyeHeight;
            syncingSliders = false;
        }

        void BuildBottomBar(RectTransform canvas)
        {
            var left = MakeButton(canvas, "TurnLeft", () => turntable.Turn(-1));
            Place((RectTransform)left.transform, new Vector2(0.5f, 0), new Vector2(-215, 84), new Vector2(150, 58));
            var zoomOut = MakeButton(canvas, "ZoomOut", () => { if (zoom) zoom.Zoom(-0.34f); });
            Place((RectTransform)zoomOut.transform, new Vector2(0.5f, 0), new Vector2(-55, 84), new Vector2(110, 58));
            Stretch(MakeText((RectTransform)zoomOut.transform, "L", "zoom -", bodyFont, 32, textColor, TextAnchor.MiddleCenter).rectTransform, 0);
            var zoomIn = MakeButton(canvas, "ZoomIn", () => { if (zoom) zoom.Zoom(+0.34f); });
            Place((RectTransform)zoomIn.transform, new Vector2(0.5f, 0), new Vector2(65, 84), new Vector2(110, 58));
            Stretch(MakeText((RectTransform)zoomIn.transform, "L", "zoom +", bodyFont, 32, textColor, TextAnchor.MiddleCenter).rectTransform, 0);
            Stretch(MakeText((RectTransform)left.transform, "L", "< turn", bodyFont, 36, textColor, TextAnchor.MiddleCenter).rectTransform, 0);
            var right = MakeButton(canvas, "TurnRight", () => turntable.Turn(+1));
            Place((RectTransform)right.transform, new Vector2(0.5f, 0), new Vector2(225, 84), new Vector2(150, 58));
            Stretch(MakeText((RectTransform)right.transform, "R", "turn >", bodyFont, 36, textColor, TextAnchor.MiddleCenter).rectTransform, 0);

            var hint = MakeText(canvas, "Hint", "drag: spin   scroll/Z/X: zoom   W/S: category   A/D: style   Q/E: color   enter: ready",
                bodyFont, 26, dimColor, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1300, 34));

            var ready = MakeButton(canvas, "Ready", OnReady);
            ready.GetComponent<Image>().color = accentColor;
            Place((RectTransform)ready.transform, new Vector2(1, 0), new Vector2(-64, 64), new Vector2(300, 84), new Vector2(1, 0));
            Stretch(MakeText((RectTransform)ready.transform, "Label", "[ READY ]", bodyFont, 52, textColor, TextAnchor.MiddleCenter).rectTransform, 0);

            statusLabel = MakeText(canvas, "Status", "", bodyFont, 32, accentColor, TextAnchor.MiddleRight);
            Place(statusLabel.rectTransform, new Vector2(1, 0), new Vector2(-384, 160), new Vector2(400, 40), new Vector2(1, 0));
        }

        // solo / host / join, plus a server address box (only shown for join)
        void BuildNetRow(RectTransform canvas)
        {
            var modes = new[] { NetMode.Solo, NetMode.Host, NetMode.Join };
            for (int i = 0; i < modes.Length; i++)
            {
                var mode = modes[i];
                var button = MakeButton(canvas, "Mode_" + mode, () => SetMode(mode));
                Place((RectTransform)button.transform, new Vector2(1, 0), new Vector2(-64 - (modes.Length - 1 - i) * 102, 160),
                      new Vector2(96, 46), new Vector2(1, 0));
                Stretch(MakeText((RectTransform)button.transform, "L", mode.ToString().ToLower(), bodyFont, 30, textColor,
                                 TextAnchor.MiddleCenter).rectTransform, 0);
                modeButtons[mode] = button.GetComponent<Image>();
            }

            var field = new GameObject("Address", typeof(RectTransform), typeof(Image), typeof(InputField));
            field.transform.SetParent(canvas, false);
            var image = field.GetComponent<Image>();
            image.sprite = frameSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.5f;
            Place((RectTransform)field.transform, new Vector2(1, 0), new Vector2(-384, 64), new Vector2(276, 84), new Vector2(1, 0));
            var text = MakeText((RectTransform)field.transform, "Text", "", bodyFont, 34, textColor, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform, 16, 0);
            text.supportRichText = false;
            var hint = MakeText((RectTransform)field.transform, "Placeholder", "server address", bodyFont, 34, dimColor, TextAnchor.MiddleLeft);
            Stretch(hint.rectTransform, 16, 0);
            addressField = field.GetComponent<InputField>();
            addressField.textComponent = text;
            addressField.placeholder = hint;
            addressField.text = NetSession.Address;

            SetMode(NetSession.Mode == NetMode.Server ? NetMode.Solo : NetSession.Mode);
        }

        void SetMode(NetMode mode)
        {
            chosenMode = mode;
            foreach (var pair in modeButtons) pair.Value.color = pair.Key == mode ? accentColor : Color.white;
            addressField.gameObject.SetActive(mode == NetMode.Join);
        }

        // Faint horizontal lines over everything, like an old CRT. Ignores clicks.
        void BuildScanlines(RectTransform canvas)
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

        // ---------- behavior ----------

        void SelectCategory(OutfitCategory category)
        {
            currentCategory = category;
            eyeControls.SetActive(category == OutfitCategory.Eyes);
            SyncSliders();
            RebuildSwatches();
            Refresh();
        }

        void RebuildSwatches()
        {
            foreach (var s in swatches) Destroy(s.ring.gameObject);
            swatches.Clear();
            var slot = appearance.GetSlot(currentCategory);
            if (slot == null) return;
            for (int i = 0; i < slot.colors.Length; i++)
            {
                int index = i;
                // square pixel chip with a frame that lights up when selected
                var ring = new GameObject("Swatch" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                ring.transform.SetParent(swatchGrid, false);
                var ringImage = ring.GetComponent<Image>();
                ringImage.sprite = solidSprite;
                ring.GetComponent<Button>().onClick.AddListener(() => appearance.SetColor(currentCategory, index));

                var chip = new GameObject("Chip", typeof(RectTransform), typeof(Image));
                chip.transform.SetParent(ring.transform, false);
                Stretch((RectTransform)chip.transform, 5);
                var chipImage = chip.GetComponent<Image>();
                chipImage.sprite = solidSprite;
                chipImage.color = slot.colors[i];
                chipImage.raycastTarget = false;
                swatches.Add((ringImage, chipImage));
            }
        }

        void Refresh()
        {
            for (int i = 0; i < categories.Length; i++)
            {
                var category = categories[i];
                bool selected = category == currentCategory;
                var (label, bar) = rows[category];
                label.text = $"{(selected ? ">" : " ")} [{(i + 1) % 10}] {category.ToString().ToLower()}";
                label.color = selected ? accentColor : textColor;
                bar.color = selected ? hiliteColor : Color.clear;
            }

            var slot = appearance.GetSlot(currentCategory);
            if (slot == null) return;
            styleName.text = slot.CurrentStyleName;
            styleCount.text = $"{slot.styleIndex + 1} / {slot.StyleCount}";
            for (int i = 0; i < swatches.Count; i++)
                swatches[i].ring.color = i == slot.colorIndex ? textColor : new Color(0, 0, 0, 0.6f);
            statusLabel.text = "";
        }

        void OnReady()
        {
            CharacterSave.Save(appearance);
            NetSession.Mode = chosenMode;
            if (chosenMode == NetMode.Join) NetSession.Address = addressField.text.Trim();
            statusLabel.text = "saved_ok :)";
            Debug.Log("[dreamWorld] Character saved.");
            if (Application.CanStreamedLevelBeLoaded(worldScene))
                UnityEngine.SceneManagement.SceneManager.LoadScene(worldScene);
        }

        // ---------- small UI helpers ----------

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect, float inset, float insetY = -1)
        {
            if (insetY < 0) insetY = inset;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, insetY);
            rect.offsetMax = new Vector2(-inset, -insetY);
        }

        void Header(RectTransform panel, string text, float y)
        {
            var header = MakeText(panel, "Header", text, bodyFont, 34, dimColor, TextAnchor.MiddleCenter);
            Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(400, 40));
        }

        RectTransform MakePanel(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = frameSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.5f;   // draws the 2px frame at 4px on screen
            return (RectTransform)go.transform;
        }

        Text MakeText(RectTransform parent, string name, string content, Font font, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        Button MakeButton(RectTransform parent, string name, UnityEngine.Events.UnityAction onClick, bool transparent = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            if (transparent)
            {
                image.sprite = solidSprite;
                image.color = Color.clear;
            }
            else
            {
                image.sprite = frameSprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 0.5f;
            }
            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.85f, 0.92f);
            colors.pressedColor = accentColor;
            button.colors = colors;
            button.onClick.AddListener(onClick);
            return button;
        }

        // 12x12 pixel frame: 2px lavender border around a translucent night fill, 9-sliced.
        Sprite MakeFrameSprite()
        {
            const int size = 12, border = 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = x < border || y < border || x >= size - border || y >= size - border;
                bool corner = (x < border || x >= size - border) && (y < border || y >= size - border);
                tex.SetPixel(x, y, corner ? Color.clear : edge ? frameColor : panelColor);  // notched pixel corners
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border + 1, border + 1, border + 1, border + 1));
        }
    }
}
