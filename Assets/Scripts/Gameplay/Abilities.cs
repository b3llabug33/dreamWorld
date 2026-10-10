using System.Collections.Generic;
using DreamWorld.Account;
using DreamWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace DreamWorld.Gameplay
{
    // A spell: what it costs, how long until you can cast it again, and the level you learn it at.
    public class Spell
    {
        public string id, name, description;
        public KeyCode key;
        public float cost, cooldown;
        public int level;
        public Color color;
    }

    // Magic. Z/X/C cast the spells you've learned (they unlock as you level up), using the blue magic bar:
    //  spark bolt  - a quick zap toward what you're aiming at (or your lock-on target)
    //  mend        - heals you over a couple of seconds
    //  dream burst - a ring of dream light that hits everything around you
    // Points in Magic make the bar bigger and the spells stronger. The spell bar sits to the right of the hotbar.
    [RequireComponent(typeof(PlayerStats))]
    public class Abilities : MonoBehaviour
    {
        public static readonly Spell[] Spells =
        {
            new Spell { id = "spark", name = "spark bolt", key = KeyCode.Z, cost = 3f, cooldown = 0.6f, level = 0, color = new Color(1f, 0.9f, 0.5f),
                        description = "a quick zap toward what you're aiming at" },
            new Spell { id = "mend", name = "mend", key = KeyCode.X, cost = 6f, cooldown = 8f, level = 2, color = new Color(0.55f, 1f, 0.65f),
                        description = "heal about a third of your health over two seconds" },
            new Spell { id = "burst", name = "dream burst", key = KeyCode.C, cost = 8f, cooldown = 6f, level = 4, color = new Color(0.75f, 0.6f, 1f),
                        description = "a ring of dream light that hits everything around you" },
        };

        PlayerStats stats;
        PlayerController controller;
        readonly Dictionary<string, float> readyAt = new Dictionary<string, float>();
        float mendLeft, mendRate;

        // the spell bar
        RectTransform bar;
        readonly List<(Spell spell, Image icon, Image shade, Text label)> slots = new List<(Spell, Image, Image, Text)>();

        public float Power => 1f + 0.12f * stats.P(Attribute.Magic);   // spell damage/heal multiplier

        void Awake()
        {
            stats = GetComponent<PlayerStats>();
            controller = GetComponent<PlayerController>();
            stats.LeveledUp += OnLevel;
        }

        void OnDestroy() { if (stats != null) stats.LeveledUp -= OnLevel; }

        bool Local => PlayerStats.Local == stats;

        void OnLevel(int level)
        {
            if (!Local) return;
            foreach (var s in Spells)
                if (s.level == level)
                    GameHUD.Instance?.Toast($"you learned {s.name}! press {s.key} to cast it");
        }

        public bool Knows(Spell s) => stats.progress.level >= s.level;
        public float CooldownLeft(Spell s) => readyAt.TryGetValue(s.id, out var t) ? Mathf.Max(0f, t - Time.time) : 0f;

        static bool MenusOpen =>
            (GameHUD.Instance != null && GameHUD.Instance.MenuOpen) || InventoryUI.IsOpen || Quests.DialogueUI.IsOpen
            || Gathering.CraftingUI.IsOpen || Town.HomeDecor.Active;

        void Update()
        {
            if (mendLeft > 0f && !stats.Dead)
            {
                float step = Mathf.Min(mendLeft, mendRate * Time.deltaTime);
                stats.health = Mathf.Min(stats.MaxHealth, stats.health + step);
                mendLeft -= step;
            }
            if (!Local) return;
            if (bar == null) BuildBar();
            RefreshBar();
            if (stats.Dead || MenusOpen || (controller != null && !controller.InputEnabled)) return;
            foreach (var s in Spells)
                if (DreamWorld.Gameplay.Keys.Down(s.key)) Cast(s);
        }

        // A spell was just cast (id, where it starts, its direction): NetPlayer shows it to everyone else.
        public event System.Action<string, Vector3, Vector3> SpellCast;

        // Someone else cast a spell: the same effects, but nothing gets hurt or healed (their own game does that).
        public void PlayRemote(string id, Vector3 from, Vector3 dir)
        {
            var s = System.Array.Find(Spells, x => x.id == id);
            if (s == null) return;
            switch (id)
            {
                case "spark":
                    Projectile.Fire(gameObject, from, dir, 20f, 0f, s.color, 0.28f).visualOnly = true;
                    SpellFx.Flash(from, s.color, 0.25f, 3f);
                    DreamWorld.Audio.AudioManager.Play3D("blip", from);
                    break;
                case "mend":
                    SpellFx.Sparkles(transform, s.color, 14, 2f);
                    break;
                case "burst":
                    SpellFx.Ring(transform.position + Vector3.up * 0.15f, s.color, 3.6f, 0.45f);
                    SpellFx.Flash(transform.position + Vector3.up * 0.8f, s.color, 0.35f, 7f);
                    DreamWorld.Audio.AudioManager.Play3D("swing_heavy", transform.position);
                    break;
            }
        }

        // Casts a spell if you know it, it's ready and you have the magic. Public for tests and buttons.
        public bool Cast(Spell s)
        {
            if (!Knows(s)) { GameHUD.Instance?.Toast($"you learn {s.name} at level {s.level}"); return false; }
            if (CooldownLeft(s) > 0f) return false;
            if (!stats.TrySpendMagic(s.cost)) { GameHUD.Instance?.Toast("not enough magic"); return false; }
            readyAt[s.id] = Time.time + s.cooldown;
            switch (s.id)
            {
                case "spark": Spark(s); break;
                case "mend": Mend(s); break;
                case "burst": Burst(s); break;
            }
            return true;
        }

        void Spark(Spell s)
        {
            Vector3 from = transform.position + Vector3.up * 1.25f;
            Vector3 dir;
            var target = controller != null ? controller.LockTarget : null;
            if (target != null) dir = target.Body.position + Vector3.up * 0.6f - from;
            else
            {
                var cam = Camera.main;
                dir = cam != null ? cam.transform.forward : transform.forward;
                dir.y = Mathf.Clamp(dir.y, -0.25f, 0.25f);   // mostly level, so it doesn't hit the ground at your feet
            }
            var flat = new Vector3(dir.x, 0, dir.z);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat);
            from += flat.normalized * 0.5f;
            Projectile.Fire(gameObject, from, dir, 20f, 9f * Power, s.color, 0.28f);
            SpellFx.Flash(from, s.color, 0.25f, 3f);
            SpellCast?.Invoke(s.id, from, dir);
            DreamWorld.Audio.AudioManager.Play3D("blip", from);
        }

        void Mend(Spell s)
        {
            mendRate = stats.MaxHealth * 0.34f * Power / 2f;   // per second, for two seconds
            mendLeft = stats.MaxHealth * 0.34f * Power;
            SpellFx.Sparkles(transform, s.color, 14, 2f);
            DreamWorld.Audio.AudioManager.Play("levelup", 0.5f);
            SpellCast?.Invoke(s.id, transform.position, Vector3.zero);
        }

        void Burst(Spell s)
        {
            var center = transform.position + Vector3.up * 0.8f;
            Hits.Sweep(center, 3.6f, gameObject, 14f * Power, new HashSet<IDamageable>());
            SpellFx.Ring(transform.position + Vector3.up * 0.15f, s.color, 3.6f, 0.45f);
            SpellFx.Flash(center, s.color, 0.35f, 7f);
            DreamWorld.Audio.AudioManager.Play3D("swing_heavy", center);
            SpellCast?.Invoke(s.id, center, Vector3.zero);
        }

        // ---------------------------------------------------------------- the spell bar

        void BuildBar()
        {
            var hud = GameHUD.Instance;
            var ui = new TerminalUI(hud != null ? hud.titleFont : null, hud != null ? hud.bodyFont : null);
            var canvas = ui.Canvas("SpellBar", 4);
            bar = new GameObject("Spells", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(canvas, false);
            // to the right of the hotbar (4 x 80 wide, centered, 74 up)
            TerminalUI.Place(bar, new Vector2(0.5f, 0), new Vector2(196, 74), new Vector2(Spells.Length * 80, 72), new Vector2(0, 0));
            for (int i = 0; i < Spells.Length; i++)
            {
                var s = Spells[i];
                var frame = ui.PanelBox(bar, "Spell_" + s.id);
                TerminalUI.Place(frame, new Vector2(0, 0), new Vector2(i * 80, 0), new Vector2(72, 72), new Vector2(0, 0));
                var fill = frame.GetComponent<Image>();
                fill.color = new Color(1f, 1f, 1f, 0.9f);
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(frame, false);
                var icon = iconGo.GetComponent<Image>();
                icon.sprite = Items.Icon("spell_" + s.id);
                icon.raycastTarget = false;
                TerminalUI.Place((RectTransform)iconGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52, 52));
                // the cooldown: a dark cover that shrinks away from the top
                var shadeGo = new GameObject("Cooldown", typeof(RectTransform), typeof(Image));
                shadeGo.transform.SetParent(frame, false);
                var shade = shadeGo.GetComponent<Image>();
                shade.color = new Color(0.05f, 0.03f, 0.12f, 0.7f);
                shade.raycastTarget = false;
                var sr = (RectTransform)shadeGo.transform;
                sr.anchorMin = new Vector2(0, 0);
                sr.anchorMax = new Vector2(1, 0);
                sr.pivot = new Vector2(0.5f, 0);
                sr.offsetMin = new Vector2(5, 5);
                sr.offsetMax = new Vector2(-5, 5);
                var label = ui.Label(frame, s.key.ToString(), 20, TerminalUI.Text, TextAnchor.UpperLeft);
                TerminalUI.Place(label.rectTransform, new Vector2(0, 1), new Vector2(8, -4), new Vector2(60, 24), new Vector2(0, 1));
                slots.Add((s, icon, shade, label));
            }
        }

        void RefreshBar()
        {
            foreach (var (s, icon, shade, label) in slots)
            {
                bool known = Knows(s);
                float cd = CooldownLeft(s);
                bool affordable = stats.magic >= s.cost;
                icon.color = !known ? new Color(0.35f, 0.35f, 0.4f, 0.6f) : affordable ? Color.white : new Color(0.6f, 0.6f, 0.8f, 0.8f);
                var sr = shade.rectTransform;
                float h = known ? (s.cooldown > 0 ? cd / s.cooldown : 0f) * 62f : 62f;
                sr.sizeDelta = new Vector2(sr.sizeDelta.x, h);
                label.text = known ? s.key.ToString() : $"lv{s.level}";
                label.color = known ? TerminalUI.Text : TerminalUI.Dim;
            }
        }
    }

    // Little PS1-style spell effects: glowing shapes that grow/rise and fade, and a quick light.
    public static class SpellFx
    {
        static Material glow;

        static Material Glow
        {
            get
            {
                if (glow != null) return glow;
                glow = new Material(Shader.Find("DreamWorld/PSX Lit"));
                glow.SetFloat("_Unlit", 1f);
                glow.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                glow.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);   // additive: it glows
                glow.SetFloat("_ZWrite", 0f);
                glow.SetFloat("_Cull", 0f);
                glow.renderQueue = 3100;
                return glow;
            }
        }

        static GameObject Shape(PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            go.transform.localScale = scale;
            Paint(go, color);
            return go;
        }

        // glow material + color right away (so the very first frame isn't plain white)
        static void Paint(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Glow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            r.SetPropertyBlock(block);
        }

        // A flat ring (radius 1, 15% thick), made once.
        static Mesh ringMesh;
        static Mesh RingMesh
        {
            get
            {
                if (ringMesh != null) return ringMesh;
                const int n = 40;
                var v = new Vector3[n * 2];
                var t = new int[n * 6];
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    v[i * 2] = d * 0.85f;
                    v[i * 2 + 1] = d;
                    int j = (i + 1) % n;
                    t[i * 6] = i * 2; t[i * 6 + 1] = j * 2; t[i * 6 + 2] = i * 2 + 1;
                    t[i * 6 + 3] = i * 2 + 1; t[i * 6 + 4] = j * 2; t[i * 6 + 5] = j * 2 + 1;
                }
                ringMesh = new Mesh { name = "SpellRing", vertices = v, triangles = t };
                ringMesh.RecalculateNormals();
                ringMesh.RecalculateBounds();
                return ringMesh;
            }
        }

        public static void Flash(Vector3 pos, Color color, float seconds, float range)
        {
            var go = new GameObject("SpellFlash");
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = 2f;
            l.renderMode = LightRenderMode.ForceVertex;
            var f = go.AddComponent<FxFade>();
            f.life = seconds;
            f.glowLight = l;
        }

        public static void Ring(Vector3 pos, Color color, float radius, float seconds)
        {
            var go = new GameObject("SpellRing", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = RingMesh;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.3f;
            Paint(go, color);
            var f = go.AddComponent<FxFade>();
            f.life = seconds;
            f.color = color;
            f.grow = new Vector3(radius, 1f, radius);
        }

        public static void Sparkles(Transform follow, Color color, int count, float seconds)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f, r = Random.Range(0.3f, 0.6f);
                var go = Shape(PrimitiveType.Cube, follow.position + new Vector3(Mathf.Cos(a) * r, Random.Range(0.1f, 1.4f), Mathf.Sin(a) * r),
                               Vector3.one * Random.Range(0.06f, 0.11f), color);
                go.transform.rotation = Random.rotation;
                var f = go.AddComponent<FxFade>();
                f.life = seconds * Random.Range(0.6f, 1f);
                f.color = color;
                f.rise = Random.Range(0.5f, 1.1f);
                f.follow = follow;
            }
        }
    }

    // Fades a spell effect out over its life (growing, rising, following the caster), then removes it.
    public class FxFade : MonoBehaviour
    {
        public float life = 0.5f, rise;
        public Color color = Color.white;
        public Vector3 grow;
        public Light glowLight;
        public Transform follow;
        float age;
        Vector3 startScale, offset;
        MaterialPropertyBlock block;
        Renderer rend;

        void Start()
        {
            startScale = transform.localScale;
            rend = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            if (follow != null) offset = transform.position - follow.position;
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / life);
            if (grow != Vector3.zero) transform.localScale = Vector3.Lerp(startScale, grow, 1f - (1f - t) * (1f - t));
            if (follow != null) transform.position = follow.position + offset;
            offset += Vector3.up * rise * Time.deltaTime;
            if (rend != null)
            {
                block.SetColor("_Color", new Color(color.r, color.g, color.b, 1f - t));
                rend.SetPropertyBlock(block);
            }
            if (glowLight != null) glowLight.intensity = 2f * (1f - t);
            if (age >= life) Destroy(gameObject);
        }
    }
}
