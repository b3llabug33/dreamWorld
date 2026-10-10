using DreamWorld.CharacterCreation;
using DreamWorld.Gameplay;
using UnityEngine;

namespace DreamWorld.World
{
    // Holds the equipped weapon in her right hand (and the shield on her left arm) and poses the arms
    // for weapon swings, on top of IdleMotion (it runs after it every frame):
    //   light 1: wind up over the right shoulder, slash diagonally down to the left, body twisting into it
    //   light 2: backhand from over the left shoulder, slashing across to the right
    //   light 3: pull back and thrust straight ahead (the finisher)
    //   heavy:   both hands over the head, a little hop, and a slam straight down
    //   guard:   shield arm up in front (or the blade held across the body with no shield)
    // Each swing is a few key poses (arm directions in character space) blended with easing, and the
    // SwingTrail behind the blade shows the arc. Put it on the player next to PlayerController.
    [DefaultExecutionOrder(50)]
    public class WeaponPose : MonoBehaviour
    {
        public Color lightTrail = new Color(1f, 0.62f, 0.9f, 0.85f);
        public Color heavyTrail = new Color(1f, 0.85f, 0.35f, 0.95f);

        // One key pose. Directions are in character space: x = her right, y = up, z = forward.
        struct Pose
        {
            public Vector3 upR, loR, blade, upL, loL;
            public float wR, wL;          // how strongly each arm is driven (0 = leave IdleMotion's pose)
            public float twoHand;         // 0..1: left hand reaches for the weapon (heavy, guard without a shield)
            public float leftOnBlade;     // where along the weapon the left hand goes (meters from the grip)
            public float twist, lean;     // chest turn (+ = to her right) and bow (+ = forward), degrees
            public float stance;          // 0..1 lunge stance (left foot forward)
        }

        struct Key { public float t; public Pose pose; public Ease ease; public Key(float t, Pose p, Ease e) { this.t = t; pose = p; ease = e; } }
        enum Ease { Smooth, In, Out }

        PlayerController controller;
        PlayerStats stats;
        IdleMotion motion;
        Transform model, spine, chest, neck, upR, loR, handR, upL, loL, handL, legUL, legUR, legLL, legLR;
        string weaponId = "", shieldId = "";
        Transform weapon, shield;
        float weaponLength = 1f;
        SwingTrail trail;
        Pose current, snapshot;
        bool hasCurrent, wasSwinging;
        int lastCombo = -1;
        bool lastHeavy;
        float lastT;
        Vector3 edge = Vector3.forward, lastBlade;

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z).normalized;

        void Start()
        {
            controller = GetComponent<PlayerController>();
            stats = GetComponent<PlayerStats>();
        }

        bool FindBones()
        {
            motion = GetComponentInChildren<IdleMotion>();
            if (motion == null) return false;
            model = motion.transform;
            Transform B(string n)
            {
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
                return null;
            }
            spine = B("Spine"); chest = B("Chest"); neck = B("Neck");
            upR = B("UpperArm.R"); loR = B("LowerArm.R"); handR = B("Hand.R");
            upL = B("UpperArm.L"); loL = B("LowerArm.L"); handL = B("Hand.L");
            legUL = B("UpperLeg.L"); legUR = B("UpperLeg.R"); legLL = B("LowerLeg.L"); legLR = B("LowerLeg.R");
            return upR != null && loR != null && handR != null && upL != null && loL != null && handL != null && chest != null;
        }

        // ---------- the held models ----------

        void SyncModels()
        {
            var w = stats != null ? stats.Weapon : null;
            string wantW = w != null ? w.id : "";
            if (wantW != weaponId)
            {
                weaponId = wantW;
                if (weapon != null) Destroy(weapon.gameObject);
                weapon = Spawn(w, out weaponLength);
            }
            var s = stats != null ? stats.Shield : null;
            string wantS = s != null ? s.id : "";
            if (wantS != shieldId)
            {
                shieldId = wantS;
                if (shield != null) Destroy(shield.gameObject);
                shield = Spawn(s, out _);
            }
        }

        Transform Spawn(ItemDef def, out float length)
        {
            length = 1f;
            if (def == null || string.IsNullOrEmpty(def.model)) return null;
            var prefab = Resources.Load<GameObject>("Weapons/" + def.model);
            if (prefab == null) { Debug.LogWarning("[WeaponPose] no model Resources/Weapons/" + def.model); return null; }
            // the imported model keeps its own rotation under a holder whose +Y is the blade (the shield's face)
            var go = new GameObject("Held_" + def.model);
            var model = Instantiate(prefab, go.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = prefab.transform.localRotation;
            go.transform.localScale = Vector3.one * def.modelScale;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            // length along the blade (+Y from the grip), for the trail
            float top = 0.5f;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                top = Mathf.Max(top, r.bounds.max.y - go.transform.position.y);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            length = top;
            return go.transform;
        }

        void OnDestroy()
        {
            if (weapon != null) Destroy(weapon.gameObject);
            if (shield != null) Destroy(shield.gameObject);
            if (trail != null) Destroy(trail.gameObject);
        }

        void OnDisable()
        {
            if (weapon != null) weapon.gameObject.SetActive(false);
            if (shield != null) shield.gameObject.SetActive(false);
        }

        // ---------- poses ----------

        Pose Ready()
        {
            bool guard = controller.Guarding;
            bool hasShield = shield != null;
            var p = new Pose
            {
                upR = V(0.15f, -0.95f, 0.22f), loR = V(0.12f, -0.3f, 0.95f), blade = V(0.08f, 0.6f, 0.8f),
                upL = V(-0.1f, -1f, 0.05f), loL = V(-0.05f, -0.8f, 0.6f),
                wR = weapon != null ? 0.85f : 0f, wL = 0f,
            };
            if (motion != null && motion.moveAmount > 0.6f)   // sprinting: blade swept back low
            {
                float k = Mathf.InverseLerp(0.6f, 1f, motion.moveAmount);
                p.upR = Vector3.Slerp(p.upR, V(0.25f, -0.9f, -0.35f), k);
                p.loR = Vector3.Slerp(p.loR, V(0.2f, -0.7f, 0.55f), k);
                p.blade = Vector3.Slerp(p.blade, V(0.25f, 0.25f, -0.95f), k);
                p.wR *= 1f - 0.4f * k;
            }
            if (guard && hasShield)
            {
                p.upL = V(-0.3f, -0.15f, 0.94f); p.loL = V(0.8f, 0.35f, 0.45f); p.wL = 1f;
                p.upR = V(0.3f, -0.8f, -0.1f); p.loR = V(0.1f, -0.1f, 1f); p.blade = V(0.2f, 0.85f, 0.45f);
                p.twist = -8f; p.lean = 6f; p.stance = 0.5f;
            }
            else if (guard && weapon != null)   // no shield: the blade held across, left hand bracing it
            {
                p.upR = V(0.35f, -0.3f, 0.88f); p.loR = V(-0.75f, 0.4f, 0.55f); p.blade = V(-0.95f, 0.25f, 0.12f);
                p.wR = 1f; p.twoHand = 1f; p.leftOnBlade = 0.42f * weaponLength;
                p.twist = 10f; p.lean = 6f; p.stance = 0.5f;
            }
            return p;
        }

        Key[] Swing(int combo, bool heavy, Pose start, Pose end)
        {
            if (heavy)
                return new[]
                {
                    new Key(0f, start, Ease.Smooth),
                    new Key(0.36f, new Pose { upR = V(0.2f, 0.95f, -0.2f), loR = V(-0.35f, 0.5f, -0.8f), blade = V(0f, 0.25f, -0.97f),
                                              upL = V(-0.2f, 0.95f, -0.2f), loL = V(0.35f, 0.5f, -0.8f),
                                              wR = 1f, twoHand = 1f, leftOnBlade = -0.1f, lean = -16f, stance = 0.3f }, Ease.In),
                    new Key(0.5f, new Pose { upR = V(0.12f, 0.98f, 0.15f), loR = V(0f, 0.9f, 0.45f), blade = V(0f, 0.9f, 0.45f),
                                             wR = 1f, twoHand = 1f, leftOnBlade = -0.1f, lean = -4f, stance = 0.6f }, Ease.Out),
                    new Key(0.6f, new Pose { upR = V(0.12f, -0.15f, 0.98f), loR = V(0f, -0.5f, 0.86f), blade = V(0f, -0.72f, 0.7f),
                                             wR = 1f, twoHand = 1f, leftOnBlade = -0.1f, lean = 24f, stance = 1f }, Ease.Out),
                    new Key(0.76f, new Pose { upR = V(0.1f, -0.4f, 0.9f), loR = V(0f, -0.78f, 0.62f), blade = V(0f, -0.93f, 0.36f),
                                              wR = 1f, twoHand = 1f, leftOnBlade = -0.1f, lean = 28f, stance = 1f }, Ease.Smooth),
                    new Key(1f, end, Ease.Smooth),
                };
            switch (combo)
            {
                case 0:   // right to left: over the right shoulder, diagonally down across to the left
                    return new[]
                    {
                        new Key(0f, start, Ease.Smooth),
                        new Key(0.3f, new Pose { upR = V(0.5f, 0.6f, -0.6f), loR = V(-0.1f, 0.95f, 0.2f), blade = V(0.35f, 0.55f, -0.75f),
                                                 upL = V(-0.25f, -0.55f, 0.8f), loL = V(0.2f, -0.1f, 0.97f),
                                                 wR = 1f, wL = 0.7f, twist = 35f, lean = -4f, stance = 0.4f }, Ease.In),
                        new Key(0.42f, new Pose { upR = V(0.3f, 0.2f, 0.93f), loR = V(0f, 0.3f, 0.95f), blade = V(0.55f, 0.45f, 0.7f),
                                                  upL = V(-0.3f, -0.75f, 0.3f), loL = V(-0.1f, -0.6f, 0.75f),
                                                  wR = 1f, wL = 0.7f, twist = 5f, lean = 4f, stance = 0.8f }, Ease.Out),
                        new Key(0.52f, new Pose { upR = V(-0.35f, -0.4f, 0.85f), loR = V(-0.7f, -0.5f, 0.5f), blade = V(-0.7f, -0.62f, 0.36f),
                                                  upL = V(-0.35f, -0.85f, -0.4f), loL = V(-0.3f, -0.9f, -0.2f),
                                                  wR = 1f, wL = 0.7f, twist = -35f, lean = 12f, stance = 1f }, Ease.Out),
                        new Key(0.7f, new Pose { upR = V(-0.5f, -0.7f, 0.5f), loR = V(-0.5f, -0.85f, 0.15f), blade = V(-0.45f, -0.85f, -0.3f),
                                                 upL = V(-0.35f, -0.85f, -0.4f), loL = V(-0.3f, -0.9f, -0.2f),
                                                 wR = 1f, wL = 0.6f, twist = -45f, lean = 15f, stance = 1f }, Ease.Smooth),
                        new Key(1f, end, Ease.Smooth),
                    };
                case 1:   // left to right: a backhand from over the left shoulder, across to the right
                    return new[]
                    {
                        new Key(0f, start, Ease.Smooth),
                        new Key(0.3f, new Pose { upR = V(-0.75f, 0.35f, 0.55f), loR = V(-0.6f, 0.75f, -0.25f), blade = V(-0.5f, 0.6f, -0.62f),
                                                 upL = V(-0.3f, -0.85f, -0.4f), loL = V(-0.25f, -0.9f, -0.1f),
                                                 wR = 1f, wL = 0.6f, twist = -50f, lean = -3f, stance = 0.4f }, Ease.In),
                        new Key(0.42f, new Pose { upR = V(-0.1f, 0.2f, 0.97f), loR = V(0.1f, 0.35f, 0.93f), blade = V(-0.55f, 0.4f, 0.73f),
                                                  upL = V(-0.4f, -0.8f, 0.2f), loL = V(-0.3f, -0.7f, 0.6f),
                                                  wR = 1f, wL = 0.6f, twist = -8f, lean = 5f, stance = 0.8f }, Ease.Out),
                        new Key(0.52f, new Pose { upR = V(0.55f, -0.2f, 0.8f), loR = V(0.85f, -0.2f, 0.48f), blade = V(0.85f, -0.25f, 0.45f),
                                                  upL = V(-0.5f, -0.6f, 0.6f), loL = V(-0.2f, -0.3f, 0.93f),
                                                  wR = 1f, wL = 0.7f, twist = 35f, lean = 10f, stance = 1f }, Ease.Out),
                        new Key(0.7f, new Pose { upR = V(0.75f, -0.4f, 0.5f), loR = V(0.85f, -0.45f, 0f), blade = V(0.7f, -0.35f, -0.6f),
                                                 upL = V(-0.5f, -0.6f, 0.6f), loL = V(-0.2f, -0.3f, 0.93f),
                                                 wR = 1f, wL = 0.6f, twist = 45f, lean = 12f, stance = 1f }, Ease.Smooth),
                        new Key(1f, end, Ease.Smooth),
                    };
                default:  // the finisher: pull the blade back to the hip, then thrust straight ahead
                    return new[]
                    {
                        new Key(0f, start, Ease.Smooth),
                        new Key(0.34f, new Pose { upR = V(0.45f, -0.35f, -0.82f), loR = V(0.05f, 0.1f, 0.99f), blade = V(0.05f, 0.08f, 1f),
                                                  upL = V(-0.2f, -0.3f, 0.93f), loL = V(0.1f, 0f, 1f),
                                                  wR = 1f, wL = 0.8f, twist = 30f, lean = -5f, stance = 0.3f }, Ease.In),
                        new Key(0.46f, new Pose { upR = V(0.12f, 0.05f, 0.99f), loR = V(0.02f, 0.03f, 1f), blade = V(0f, 0f, 1f),
                                                  upL = V(-0.35f, -0.8f, -0.5f), loL = V(-0.3f, -0.85f, -0.4f),
                                                  wR = 1f, wL = 0.8f, twist = -22f, lean = 16f, stance = 1f }, Ease.Out),
                        new Key(0.72f, new Pose { upR = V(0.12f, 0f, 0.99f), loR = V(0.02f, -0.05f, 1f), blade = V(0f, -0.08f, 1f),
                                                  upL = V(-0.35f, -0.8f, -0.5f), loL = V(-0.3f, -0.85f, -0.4f),
                                                  wR = 1f, wL = 0.7f, twist = -24f, lean = 16f, stance = 1f }, Ease.Smooth),
                        new Key(1f, end, Ease.Smooth),
                    };
            }
        }

        static Pose Evaluate(Key[] keys, float t)
        {
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (t > keys[i + 1].t) continue;
                float u = Mathf.InverseLerp(keys[i].t, keys[i + 1].t, t);
                switch (keys[i + 1].ease)
                {
                    case Ease.In: u = u * u; break;                          // accelerate into the strike
                    case Ease.Out: u = 1f - (1f - u) * (1f - u); break;      // snap through, then slow down
                    default: u = u * u * (3f - 2f * u); break;
                }
                return Blend(keys[i].pose, keys[i + 1].pose, u);
            }
            return keys[keys.Length - 1].pose;
        }

        // Blends two poses. An arm direction that isn't driven (weight 0) borrows the other pose's.
        static Pose Blend(Pose a, Pose b, float u)
        {
            Vector3 Dir(Vector3 x, Vector3 y, float wx, float wy)
            {
                if (wx <= 0f || x == Vector3.zero) return y;
                if (wy <= 0f || y == Vector3.zero) return x;
                return Vector3.Slerp(x, y, u);
            }
            return new Pose
            {
                upR = Dir(a.upR, b.upR, a.wR, b.wR), loR = Dir(a.loR, b.loR, a.wR, b.wR), blade = Vector3.Slerp(a.blade, b.blade, u),
                upL = Dir(a.upL, b.upL, a.wL + a.twoHand, b.wL + b.twoHand), loL = Dir(a.loL, b.loL, a.wL + a.twoHand, b.wL + b.twoHand),
                wR = Mathf.Lerp(a.wR, b.wR, u), wL = Mathf.Lerp(a.wL, b.wL, u), twoHand = Mathf.Lerp(a.twoHand, b.twoHand, u),
                leftOnBlade = Mathf.Lerp(a.leftOnBlade, b.leftOnBlade, u),
                twist = Mathf.Lerp(a.twist, b.twist, u), lean = Mathf.Lerp(a.lean, b.lean, u), stance = Mathf.Lerp(a.stance, b.stance, u),
            };
        }

        // ---------- every frame, after IdleMotion ----------

        void LateUpdate()
        {
            if (controller == null || stats == null) return;
            if ((model == null || upR == null) && !FindBones()) return;
            SyncModels();
            bool armed = weapon != null;
            if (!armed && shield == null) { hasCurrent = false; return; }

            // which pose: a swing (keys from wherever the arms are now) or the ready/guard stance
            bool swinging = armed && controller.Swinging && !(stats.Weapon != null && stats.Weapon.ranged);
            float t = controller.SwingT;
            bool restarted = swinging && (!wasSwinging || controller.Combo != lastCombo || controller.HeavySwing != lastHeavy || t < lastT - 0.01f);
            if (restarted) snapshot = hasCurrent ? current : Ready();
            Pose target = swinging ? Evaluate(Swing(controller.Combo, controller.HeavySwing, snapshot, Ready()), t) : Ready();
            if (!armed) target.wR = 0f;

            // a slingshot shot: just aim the arm forward for a moment
            if (armed && controller.Swinging && stats.Weapon.ranged)
            {
                float k = Mathf.Sin(t * Mathf.PI);
                target.upR = Vector3.Slerp(target.upR, V(0.1f, 0.15f, 1f), k);
                target.loR = Vector3.Slerp(target.loR, V(0f, 0.1f, 1f), k);
                target.blade = Vector3.Slerp(target.blade, V(0f, 1f, 0.15f), k);
                target.upL = V(-0.05f, 0.1f, 1f); target.loL = V(0.05f, 0.12f, 1f); target.wL = k;
            }
            // tucked in a roll: let go of the pose
            float tuck = motion != null ? motion.tuck : 0f;
            target.wR *= 1f - tuck; target.wL *= 1f - tuck; target.twoHand *= 1f - tuck;
            target.twist *= 1f - tuck; target.lean *= 1f - tuck; target.stance *= 1f - tuck;

            current = hasCurrent ? Blend(current, target, 1f - Mathf.Exp(-(swinging ? 55f : 14f) * Time.deltaTime)) : target;
            hasCurrent = true;
            wasSwinging = swinging;
            lastCombo = controller.Combo;
            lastHeavy = controller.HeavySwing;
            lastT = t;

            Apply(current);
            PlaceWeapon(current, swinging, t);
            PlaceShield();
        }

        Vector3 C(Vector3 v) => model.TransformDirection(v);

        void Apply(Pose p)
        {
            // torso: twist into the swing and bow over it; the head stays on the target
            Rotate(spine, model.up, p.twist * 0.4f);
            Rotate(chest, model.up, p.twist * 0.6f);
            Rotate(spine, model.right, p.lean * 0.45f);
            Rotate(chest, model.right, p.lean * 0.55f);
            if (neck != null) Rotate(neck, model.up, -p.twist * 0.55f);
            if (neck != null) Rotate(neck, model.right, -p.lean * 0.4f);
            // a lunge stance: left foot forward, right foot back
            if (legUL != null && p.stance > 0f)
            {
                Rotate(legUL, model.right, -16f * p.stance);
                Rotate(legLL, model.right, 12f * p.stance);
                Rotate(legUR, model.right, 14f * p.stance);
                Rotate(legLR, model.right, 18f * p.stance);
            }
            if (p.wR > 0.001f)
            {
                Aim(upR, loR, C(p.upR), p.wR);
                Aim(loR, handR, C(p.loR), p.wR);
            }
            if (p.twoHand > 0.001f && weapon != null)
            {
                // the left hand reaches for the handle (heavy) or the flat of the blade (guard)
                Vector3 grip = Grip();
                Vector3 target = grip + C(p.blade) * p.leftOnBlade - C(Vector3.right) * 0.03f;
                Vector3 pole = upL.position + C(new Vector3(-0.8f, -0.6f, -0.1f));
                Reach(upL, loL, handL, target, pole, p.twoHand);
            }
            else if (p.wL > 0.001f)
            {
                Aim(upL, loL, C(p.upL), p.wL);
                Aim(loL, handL, C(p.loL), p.wL);
            }
        }

        static void Rotate(Transform bone, Vector3 axis, float degrees)
        {
            if (bone == null || degrees == 0f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }

        // Turns a bone so its child lies along `dir` (weight 0..1 blends from the current pose).
        static void Aim(Transform bone, Transform child, Vector3 dir, float weight)
        {
            Vector3 cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-6f || dir.sqrMagnitude < 1e-6f) return;
            var q = Quaternion.FromToRotation(cur, dir);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, q, weight) * bone.rotation;
        }

        // Two-bone reach: puts the hand on `target`, elbow bending toward `pole`.
        static void Reach(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 pole, float weight)
        {
            Vector3 a = upper.position;
            float l1 = Vector3.Distance(a, lower.position), l2 = Vector3.Distance(lower.position, hand.position);
            Vector3 toT = target - a;
            float d = Mathf.Clamp(toT.magnitude, 0.05f, (l1 + l2) * 0.999f);
            Vector3 dir = toT.normalized;
            float along = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
            float side = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - along * along));
            Vector3 bend = Vector3.ProjectOnPlane(pole - a, dir).normalized;
            Vector3 elbow = a + dir * along + bend * side;
            Aim(upper, lower, elbow - a, weight);
            Aim(lower, hand, a + dir * d - lower.position, weight);
        }

        Vector3 Grip() => handR.position + (handR.position - loR.position).normalized * 0.05f;

        void PlaceWeapon(Pose p, bool swinging, float t)
        {
            if (weapon == null) return;
            bool show = stats.Alive;
            if (weapon.gameObject.activeSelf != show) weapon.gameObject.SetActive(show);
            Vector3 blade = C(p.blade).normalized;
            // the edge (or the pan's face) leads the swing; at rest it faces forward
            Vector3 move = blade - lastBlade;
            Vector3 want = move.magnitude > 0.02f ? move : C(Vector3.forward);
            want = Vector3.ProjectOnPlane(want, blade);
            if (want.sqrMagnitude > 1e-4f) edge = Vector3.Slerp(edge, want.normalized, 1f - Mathf.Exp(-25f * Time.deltaTime));
            edge = Vector3.ProjectOnPlane(edge, blade).normalized;
            if (edge.sqrMagnitude < 0.5f) edge = Vector3.ProjectOnPlane(C(Vector3.forward), blade).normalized;
            lastBlade = blade;
            weapon.SetPositionAndRotation(Grip(), Quaternion.LookRotation(Vector3.Cross(edge, blade), blade));

            // the swing trail: from a bit up the blade to the tip, while the strike is moving
            if (trail == null) trail = SwingTrail.Create();
            bool heavy = controller.HeavySwing;
            bool emit = swinging && t > (heavy ? 0.42f : 0.3f) && t < (heavy ? 0.74f : 0.66f);
            trail.color = heavy ? heavyTrail : lightTrail;
            Vector3 g = weapon.position;
            trail.Sample(g + blade * weaponLength * 0.3f, g + blade * weaponLength * 1.02f, emit);
        }

        void PlaceShield()
        {
            if (shield == null) return;
            bool show = stats.Alive;
            if (shield.gameObject.activeSelf != show) shield.gameObject.SetActive(show);
            Vector3 fore = (handL.position - loL.position).normalized;
            float g = controller.Guarding ? 1f : 0f;
            Vector3 facing = Vector3.ProjectOnPlane(Vector3.Slerp(C(Vector3.left), C(Vector3.forward), g), fore).normalized;
            Vector3 pos = Vector3.Lerp(loL.position, handL.position, 0.55f) + facing * 0.07f;
            shield.SetPositionAndRotation(pos, Quaternion.LookRotation(fore, facing));
        }
    }
}
