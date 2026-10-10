using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DreamWorld.CharacterCreation
{
    // New categories go at the end (saves store names, but a stable order keeps things tidy).
    public enum OutfitCategory { Skin, Eyes, Nose, Mouth, Hair, Top, Bottom, Legwear, Shoes, Neck, Glasses, Headwear,
                                 Brows, Extra1, Extra2, Extra3 }

    // One extra choice row for a slot, e.g. hair "bangs" {Straight, Choppy, ...} or pants "rise"
    // {High, Mid, Low}. Mesh names carry the chosen values after the base name:
    // Hair_Long_Straight_Medium, Bottom_BaggyJeans_High. Styles that don't come in a value
    // (skirts have no rise, ponytails have one bangs style) just hide that row.
    // A texture axis (clothing "pattern") doesn't pick a mesh: value 0 keeps the piece's own fabric,
    // the others swap in textures[value] on its tinted parts. `bases` limits it to some styles.
    [Serializable]
    public class OptionAxis
    {
        public string label;
        public string[] values = new string[0];
        public int index;
        public Texture2D[] textures;
        public string[] bases = new string[0];
        public bool IsTexture => textures != null && textures.Length > 0;
    }

    // One customizable part of the character. Each entry in `styles` is a child mesh of the
    // Dreamer model; only the selected one is shown. Optional slots get a "none" choice first.
    [Serializable]
    public class OutfitSlot
    {
        public OutfitCategory category;
        public List<GameObject> styles = new List<GameObject>();
        public Color[] colors = { Color.white };
        public bool optional;
        public int styleIndex;
        public int colorIndex;
        public List<OptionAxis> axes = new List<OptionAxis>();

        // Full-spectrum color (hair, eyes): when on, customColor replaces the palette color.
        public bool customAllowed;
        public bool useCustom;
        public Color customColor = Color.white;

        public Color CurrentColor => useCustom ? customColor : colors[Mathf.Clamp(colorIndex, 0, colors.Length - 1)];

        // Face pieces that can be moved (nose, mouth, brows, extras): offsets in face-texture uv + size.
        public bool movable;
        public Vector2 moveRange = new Vector2(0.08f, 0.08f);   // max |x| and |y| offset
        public Vector2 sizeRange = new Vector2(0.7f, 1.4f);
        public float moveX, moveY, size = 1f;

        // Optional: the color each style starts in (by base name order), e.g. blush -> pink, piercings -> silver.
        public List<int> baseColors = new List<int>();

        // "Hair_Long_Straight_Medium" -> base "Long"
        public string BaseName(GameObject style) => style.name.Split('_')[1];

        bool HasValue(GameObject style, string value)
        {
            var tokens = style.name.Split('_');
            for (int i = 2; i < tokens.Length; i++)
                if (tokens[i] == value) return true;
            return false;
        }

        public List<string> BaseNames
        {
            get
            {
                var names = new List<string>();
                foreach (var s in styles)
                {
                    var b = BaseName(s);
                    if (!names.Contains(b)) names.Add(b);
                }
                return names;
            }
        }

        public int StyleCount => BaseNames.Count + (optional ? 1 : 0);

        public string CurrentBase
        {
            get
            {
                var names = BaseNames;
                int i = optional ? styleIndex - 1 : styleIndex;
                return i >= 0 && i < names.Count ? names[i] : null;
            }
        }

        List<GameObject> CurrentBaseStyles
        {
            get
            {
                var b = CurrentBase;
                return b == null ? new List<GameObject>() : styles.FindAll(s => BaseName(s) == b);
            }
        }

        // Which values of an axis the current style actually comes in (the UI shows only these).
        public List<int> AvailableValues(OptionAxis axis)
        {
            var found = new List<int>();
            if (axis.IsTexture)
            {
                var b = CurrentBase;
                if (b != null && (axis.bases.Length == 0 || Array.IndexOf(axis.bases, b) >= 0))
                    for (int v = 0; v < axis.values.Length; v++) found.Add(v);
                return found;
            }
            var mine = CurrentBaseStyles;
            for (int v = 0; v < axis.values.Length; v++)
                if (mine.Exists(s => HasValue(s, axis.values[v]))) found.Add(v);
            return found;
        }

        // The value an axis is really showing: the chosen one if this style has it, else its first.
        public int ShownValue(OptionAxis axis)
        {
            var available = AvailableValues(axis);
            if (available.Count == 0) return -1;
            return available.Contains(axis.index) ? axis.index : available[0];
        }

        public GameObject CurrentStyle
        {
            get
            {
                var candidates = CurrentBaseStyles;
                foreach (var axis in axes)
                {
                    if (axis.IsTexture) continue;
                    int v = ShownValue(axis);
                    if (v < 0) continue;
                    var narrowed = candidates.FindAll(s => HasValue(s, axis.values[v]));
                    if (narrowed.Count > 0) candidates = narrowed;
                }
                return candidates.Count > 0 ? candidates[0] : null;
            }
        }

        public void SelectBase(string baseName)
        {
            int i = BaseNames.IndexOf(baseName);
            if (i >= 0) styleIndex = i + (optional ? 1 : 0);
        }

        // "Hair_WolfCut" -> "wolf cut", "Shoes_MaryJanes" -> "mary janes"
        public string CurrentStyleName
        {
            get
            {
                var b = CurrentBase;
                return b == null ? "none" : Regex.Replace(b, "(?<=[a-z])(?=[A-Z])", " ").ToLower();
            }
        }
    }

    // Owns every outfit slot on the character and keeps the visible pieces in sync
    // with the chosen style + color. The UI only talks to this class.
    public class CharacterAppearance : MonoBehaviour
    {
        public List<OutfitSlot> slots = new List<OutfitSlot>();

        [Header("Eye sliders (face shader)")]
        [Range(0.75f, 1.35f)] public float eyeScale = 1f;
        [Range(-0.06f, 0.06f)] public float eyeSpacing = 0f;
        [Range(-0.05f, 0.06f)] public float eyeHeight = 0f;

        [Header("Body")]
        public int bodySex;                          // 0 = woman's body, 1 = man's body
        public int bodyType = 1;                     // index into BodyTypes(bodySex): slim / average / curvy|muscle / plus
        [Range(0.9f, 1.1f)] public float bodyHeight = 1f;

        static readonly string[] WomanTypes = { "slim", "average", "curvy", "plus" };
        static readonly string[] ManTypes = { "slim", "average", "muscle", "plus" };
        public static string[] BodyTypes(int sex) => sex == 1 ? ManTypes : WomanTypes;
        public string BodyTypeName => BodyTypes(bodySex)[Mathf.Clamp(bodyType, 0, 3)];

        // Fired after any style/color change so the UI can refresh its labels.
        public event Action Changed;

        // MaterialPropertyBlock tints a renderer without creating a new material copy.
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EyeScaleId = Shader.PropertyToID("_EyeScale");
        static readonly int EyeSpacingId = Shader.PropertyToID("_EyeSpacing");
        static readonly int EyeHeightId = Shader.PropertyToID("_EyeHeight");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int MoveXId = Shader.PropertyToID("_MoveX");
        static readonly int MoveYId = Shader.PropertyToID("_MoveY");
        static readonly int PartScaleId = Shader.PropertyToID("_PartScale");
        MaterialPropertyBlock block;

        // The look the character starts with before any save is loaded ("make a new player").
        public string DefaultLook { get; private set; }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            DefaultLook = CharacterSave.ToJson(this);
            CharacterSave.Load(this);
            foreach (var slot in slots) Apply(slot);
            ApplyBody();
        }

        public OutfitSlot GetSlot(OutfitCategory category) => slots.Find(s => s.category == category);

        // Re-applies every slot (after loading a look from a save or from the network).
        public void ApplyAll()
        {
            if (block == null) block = new MaterialPropertyBlock();
            foreach (var slot in slots) Apply(slot);
            ApplyBody();
            Changed?.Invoke();
        }

        // Body type: a woman's or man's body, a type (slim / average / curvy or muscle / plus) and a height.
        // Every body, clothing and hair mesh carries the same blend shapes (Masc, Slim, Curvy, Plus, Muscle; see
        // ArtSource/build_dreamer.py + body_shapes.py), so every outfit stays fitted on every body.
        public void SetBody(int sex, int type, float height)
        {
            bodySex = Mathf.Clamp(sex, 0, 1);
            bodyType = Mathf.Clamp(type, 0, 3);
            bodyHeight = Mathf.Clamp(height, 0.9f, 1.1f);
            ApplyBody();
            Changed?.Invoke();
        }

        static readonly string[] ShapeNames = { "Masc", "Slim", "Curvy", "Plus", "Muscle" };

        void ApplyBody()
        {
            string type = BodyTypeName;
            var weights = new float[ShapeNames.Length];
            weights[0] = bodySex == 1 ? 100f : 0f;
            if (type == "slim") weights[1] = 100f;
            else if (type == "curvy") weights[2] = 100f;
            else if (type == "plus") weights[3] = 100f;
            else if (type == "muscle") weights[4] = 100f;
            // a man's body stands a little taller; height is on the model root under this object (never this object)
            var any = GetComponentInChildren<SkinnedMeshRenderer>(true);
            var model = any != null && any.rootBone != null ? any.rootBone.parent : null;
            if (model != null && model != transform)
                model.localScale = Vector3.one * (bodyHeight * (bodySex == 1 ? 1.04f : 1f));
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                for (int i = 0; i < ShapeNames.Length; i++)
                {
                    int index = mesh.GetBlendShapeIndex(ShapeNames[i]);
                    if (index >= 0) smr.SetBlendShapeWeight(index, weights[i]);
                }
            }
        }

        public void ChangeStyle(OutfitCategory category, int direction)
        {
            var slot = GetSlot(category);
            if (slot == null || slot.StyleCount == 0) return;
            // Wrap around in both directions (the extra + count keeps the result non-negative).
            slot.styleIndex = (slot.styleIndex + direction + slot.StyleCount) % slot.StyleCount;
            int baseIndex = slot.optional ? slot.styleIndex - 1 : slot.styleIndex;
            if (baseIndex >= 0 && baseIndex < slot.baseColors.Count)   // e.g. a new piercing starts silver
            {
                slot.colorIndex = slot.baseColors[baseIndex];
                slot.useCustom = false;
            }
            Apply(slot);
            Changed?.Invoke();
        }

        public void SetEyes(float scale, float spacing, float height)
        {
            eyeScale = scale;
            eyeSpacing = spacing;
            eyeHeight = height;
            var slot = GetSlot(OutfitCategory.Eyes);
            if (slot != null) Apply(slot);
            Changed?.Invoke();
        }

        public void ResetEyes() => SetEyes(1f, 0f, 0f);

        // Moves/resizes a face piece (nose, mouth, brows, an extra).
        public void SetPlacement(OutfitCategory category, float x, float y, float size)
        {
            var slot = GetSlot(category);
            if (slot == null || !slot.movable) return;
            slot.moveX = Mathf.Clamp(x, -slot.moveRange.x, slot.moveRange.x);
            slot.moveY = Mathf.Clamp(y, -slot.moveRange.y, slot.moveRange.y);
            slot.size = Mathf.Clamp(size, slot.sizeRange.x, slot.sizeRange.y);
            Apply(slot);
            Changed?.Invoke();
        }

        public void ResetPlacement(OutfitCategory category) => SetPlacement(category, 0f, 0f, 1f);

        // Picks a value on one of a slot's option rows (bangs, length, rise).
        public void SetOption(OutfitCategory category, int axis, int value)
        {
            var slot = GetSlot(category);
            if (slot == null || axis < 0 || axis >= slot.axes.Count) return;
            slot.axes[axis].index = Mathf.Clamp(value, 0, slot.axes[axis].values.Length - 1);
            Apply(slot);
            Changed?.Invoke();
        }

        public void SetColor(OutfitCategory category, int colorIndex)
        {
            var slot = GetSlot(category);
            if (slot == null) return;
            slot.colorIndex = Mathf.Clamp(colorIndex, 0, slot.colors.Length - 1);
            slot.useCustom = false;
            Apply(slot);
            Changed?.Invoke();
        }

        public void SetCustomColor(OutfitCategory category, Color color)
        {
            var slot = GetSlot(category);
            if (slot == null || !slot.customAllowed) return;
            slot.customColor = color;
            slot.useCustom = true;
            Apply(slot);
            Changed?.Invoke();
        }

        void Apply(OutfitSlot slot)
        {
            slot.styleIndex = Mathf.Clamp(slot.styleIndex, 0, Mathf.Max(0, slot.StyleCount - 1));
            slot.colorIndex = Mathf.Clamp(slot.colorIndex, 0, slot.colors.Length - 1);

            var current = slot.CurrentStyle;
            foreach (var style in slot.styles)
                style.SetActive(style == current);
            if (current == null) return;

            bool eyes = slot.category == OutfitCategory.Eyes;
            Texture2D pattern = null;   // clothing pattern chosen on a texture axis (0 = the piece's own fabric)
            foreach (var axis in slot.axes)
                if (axis.IsTexture)
                {
                    int v = slot.ShownValue(axis);
                    if (v > 0 && v < axis.textures.Length) pattern = axis.textures[v];
                }
            foreach (var r in current.GetComponentsInChildren<Renderer>(true))
            {
                // Two-tone pieces (a sailor collar, a sneaker sole...) keep some submeshes untinted.
                var mask = r.GetComponent<TintMask>();
                int count = r.sharedMaterials.Length;
                for (int i = 0; i < count; i++)
                {
                    bool tint = mask == null || mask.Tints(i);
                    if (!tint && !eyes && !slot.movable) continue;
                    block.Clear();
                    block.SetColor(ColorId, tint ? slot.CurrentColor : Color.white);
                    if (tint && pattern != null) block.SetTexture(MainTexId, pattern);
                    if (slot.movable)
                    {
                        block.SetFloat(MoveXId, slot.moveX);
                        block.SetFloat(MoveYId, slot.moveY);
                        block.SetFloat(PartScaleId, slot.size);
                    }
                    if (eyes)   // every face layer moves with the eye sliders
                    {
                        block.SetFloat(EyeScaleId, eyeScale);
                        block.SetFloat(EyeSpacingId, eyeSpacing);
                        block.SetFloat(EyeHeightId, eyeHeight);
                    }
                    r.SetPropertyBlock(block, i);
                }
            }
        }
    }
}
