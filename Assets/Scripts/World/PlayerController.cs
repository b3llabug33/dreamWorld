using System.Collections.Generic;
using DreamWorld.CharacterCreation;
using DreamWorld.Gameplay;
using UnityEngine;

namespace DreamWorld.World
{
    // Elden Ring-style third-person controls (keyboard + mouse, same keys as Elden Ring on PC):
    //   WASD move (jog)         Shift (hold, while moving): sprint      Space: tap = roll (i-frames), hold = sprint too
    //   F: jump   Left click: attack (3-hit combo)   Shift + left click: heavy attack      Right click: guard
    //   Q: lock on / off (strafe around the target)   E: interact   1-4: quick items (InventoryUI)
    // Rolling, sprinting, jumping, attacking and guarding all cost stamina (PlayerStats).
    // Getting hit hard staggers you for a moment. The model rolls/punches through IdleMotion; with a
    // weapon equipped WeaponPose takes over the arms (wind-up, slash, follow-through) and draws the trail.
    // The equipped weapon sets damage, swing speed and reach (Items.cs).
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Transform cameraTransform;
        public float jogSpeed = 3.4f;
        public float sprintSpeed = 5.8f;
        public float strafeSpeed = 2.8f;   // locked on
        public float guardSpeed = 1.6f;
        public float acceleration = 14f;
        public float turnSmoothing = 14f;
        public float gravity = -20f;
        public float jumpSpeed = 5.6f;
        [Tooltip("Meters per full step cycle (two steps).")]
        public float strideLength = 1.3f;
        [Tooltip("Keeps her inside the play area.")]
        public float areaRadius = 46f;

        [Header("Stamina costs")]
        public float rollCost = 18f, sprintCostPerSecond = 11f, jumpCost = 8f, lightCost = 14f, heavyCost = 28f;

        [Header("Attacks (bare hands; weapons use their own stats)")]
        public float lightDamage = 12f;
        public float heavyDamage = 28f;

        // Light swings: wind-up, strike, follow-through, recover. The hit lands in the strike window.
        public const float LightHitStart = 0.36f, LightHitEnd = 0.56f, HeavyHitStart = 0.5f, HeavyHitEnd = 0.66f;

        public bool InputEnabled { get; set; } = true;   // menus turn this off
        // Lets a script steer instead of the keyboard (recording clips, cutscenes later).
        public bool UseScriptedInput { get; set; }
        public bool ScriptedSprint { get; set; }     // tests: sprint while UseScriptedInput
        public bool Sprinting => sprinting;          // the camera widens its view while you sprint
        public Vector2 ScriptedInput { get; set; }
        public IDamageable LockTarget { get; private set; }
        public bool Busy => action != Action.None;

        // What WeaponPose reads to pose the arms.
        public bool Swinging => action == Action.Light || action == Action.Heavy;
        public bool HeavySwing => action == Action.Heavy;
        public int Combo => combo;                 // 0, 1, 2 (the finisher)
        public float SwingT => Swinging ? Mathf.Clamp01(actionTime / actionLength) : 0f;
        public bool Rolling => action == Action.Roll || action == Action.Backstep;
        public bool Guarding => stats != null && stats.GuardReduction > 0f;
        public ItemDef Weapon => stats != null ? stats.Weapon : null;

        enum Action { None, Roll, Backstep, Light, Heavy, Stagger }

        CharacterController controller;
        IdleMotion motion;
        PlayerStats stats;
        Transform model;
        Vector3 modelBasePos;
        Quaternion modelBaseRot;

        float speed, verticalSpeed;
        Vector3 moveDir;
        Action action;
        float actionTime, actionLength;
        Vector3 actionDir;
        int combo;
        bool comboQueued, heavyQueued, shotFired;
        float lastLightEnd = -9f;
        float spaceDown = -1f;
        bool sprinting, sprintLocked, sprintToggled;   // sprintToggled: the "toggle sprint" setting is on and Shift switched it on
        readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();

        void Start()
        {
            controller = GetComponent<CharacterController>();
            motion = GetComponentInChildren<IdleMotion>();
            stats = GetComponent<PlayerStats>();
            if (cameraTransform == null) cameraTransform = Camera.main.transform;
            if (motion != null)
            {
                model = motion.transform;
                modelBasePos = model.localPosition;
                modelBaseRot = model.localRotation;
            }
            if (stats != null) stats.Changed += WatchForHits;
        }

        void OnDestroy()
        {
            if (stats != null) stats.Changed -= WatchForHits;
        }

        // ---------- input ----------

        void Update()
        {
            bool dead = stats != null && stats.Dead;
            var input = UseScriptedInput ? ScriptedInput
                      : InputEnabled && !dead ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) : Vector2.zero;
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            moveDir = forward * input.y + right * input.x;
            if (moveDir.sqrMagnitude > 1) moveDir.Normalize();

            if (InputEnabled && !dead && !UseScriptedInput) ReadActions();
            else if (UseScriptedInput && !ScriptedSprint) sprintLocked = false;
            if (UseScriptedInput) sprinting = ScriptedSprint && !sprintLocked && moveDir.sqrMagnitude > 0.01f && action == Action.None && !dead;
            if (LockTarget != null && (!LockTarget.Alive || Vector3.Distance(LockTarget.Body.position, transform.position) > 18f))
                LockTarget = null;

            if (action != Action.None) UpdateAction();
            else Locomotion();

            Gravity();
            Animate();
        }

        void ReadActions()
        {
            // Space: tap to roll, hold to sprint
            if (Input.GetKeyDown(KeyCode.Space)) spaceDown = Time.time;
            bool holding = Input.GetKey(KeyCode.Space) && spaceDown >= 0 && Time.time - spaceDown > 0.22f;
            if (Input.GetKeyUp(KeyCode.Space))
            {
                if (spaceDown >= 0 && Time.time - spaceDown <= 0.22f) Roll();
                spaceDown = -1f;
                sprintLocked = false;
            }
            // Shift: hold while moving to sprint (Shift + click is still the heavy attack: clicking doesn't need you to move)
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift)) sprintLocked = false;
            if (Settings.ToggleSprint)
            {
                // toggle sprint (settings): tap Shift to start, tap again (or stop moving / run out of stamina) to end
                if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                {
                    sprintToggled = !sprintToggled;
                    if (sprintToggled) sprintLocked = false;
                }
                if (moveDir.sqrMagnitude <= 0.01f || sprintLocked) sprintToggled = false;
                shift = sprintToggled;
            }
            else sprintToggled = false;
            sprinting = (holding || shift) && !sprintLocked && moveDir.sqrMagnitude > 0.01f && action == Action.None;

            if (Input.GetKeyDown(KeyCode.F)) Jump();
            if (Input.GetMouseButtonDown(0))
            {
                bool heavy = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (heavy) HeavyAttack(); else LightAttack();
            }
            if (Input.GetKeyDown(KeyCode.Q)) ToggleLockOn();
            if (stats != null) stats.GuardReduction = Input.GetMouseButton(1) && action == Action.None ? GuardAmount : 0f;
        }

        // Public so menus/tests can trigger them too.
        public void Roll()
        {
            if (action != Action.None || !controller.isGrounded || stats == null || !stats.TrySpendStamina(rollCost)) return;
            bool moving = moveDir.sqrMagnitude > 0.01f;
            Begin(moving ? Action.Roll : Action.Backstep, moving ? 0.6f : 0.4f, moving ? moveDir.normalized : -transform.forward);
            if (moving) transform.rotation = Quaternion.LookRotation(actionDir);
            DreamWorld.Audio.AudioManager.Play("roll");
        }

        public void Jump()
        {
            if (action != Action.None || !controller.isGrounded || stats == null || !stats.TrySpendStamina(jumpCost)) return;
            verticalSpeed = jumpSpeed;
            DreamWorld.Audio.AudioManager.Play("jump");
        }

        // Guarding with a shield blocks more.
        public float GuardAmount => stats != null && stats.Shield != null ? stats.Shield.guard : 0.75f;
        float SwingSpeed => Weapon != null ? Mathf.Max(0.4f, Weapon.speed) : 1f;
        float LightLength(int hit) => (hit == 2 ? 0.75f : 0.6f) / SwingSpeed;
        float HeavyLength => 1.05f / SwingSpeed;

        public void LightAttack()
        {
            if (action == Action.Light) { comboQueued = true; return; }   // buffer the next hit of the combo
            if (action != Action.None || stats == null || !stats.TrySpendStamina(lightCost)) return;
            // clicking again right after a swing ends keeps the combo going
            combo = Time.time - lastLightEnd < 0.35f && combo < 2 ? combo + 1 : 0;
            StartSwing(Action.Light, LightLength(combo));
        }

        public void HeavyAttack()
        {
            if (action == Action.Light) { heavyQueued = true; return; }
            if (action != Action.None || stats == null || !stats.TrySpendStamina(heavyCost)) return;
            StartSwing(Action.Heavy, HeavyLength);
        }

        void StartSwing(Action kind, float length)
        {
            Vector3 dir = LockTarget != null ? Flat(LockTarget.Body.position - transform.position)
                        : moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : transform.forward;
            Begin(kind, length, dir);
            transform.rotation = Quaternion.LookRotation(actionDir);
            hitThisSwing.Clear();
            shotFired = false;
            DreamWorld.Audio.AudioManager.Play(kind == Action.Heavy ? "swing_heavy" : "swing");
        }

        void Begin(Action kind, float length, Vector3 dir)
        {
            action = kind;
            actionTime = 0f;
            actionLength = length;
            actionDir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
            comboQueued = heavyQueued = false;
        }

        // ---------- lock-on ----------

        public void ToggleLockOn()
        {
            if (LockTarget != null) { LockTarget = null; return; }
            LockTarget = FindLockTarget();
        }

        IDamageable FindLockTarget()
        {
            IDamageable best = null;
            float bestScore = float.MaxValue;
            Vector3 camForward = Flat(cameraTransform.forward);
            foreach (var hit in Physics.OverlapSphere(transform.position, 16f, ~0, QueryTriggerInteraction.Ignore))
            {
                var d = hit.GetComponentInParent<IDamageable>();
                if (d == null || !d.Alive || d.Body == transform) continue;
                Vector3 to = d.Body.position - transform.position;
                float angle = Vector3.Angle(camForward, Flat(to));
                if (angle > 70f) continue;   // only things roughly in front of the camera
                float score = to.magnitude + angle * 0.1f;
                if (score < bestScore) { bestScore = score; best = d; }
            }
            return best;
        }

        // ---------- movement ----------

        void Locomotion()
        {
            bool guarding = stats != null && stats.GuardReduction > 0f;
            if (sprinting && stats != null)
            {
                if (!stats.TrySpendStamina(sprintCostPerSecond * Time.deltaTime)) { sprinting = false; sprintLocked = true; }
            }
            float top = guarding ? guardSpeed : sprinting ? sprintSpeed : LockTarget != null ? strafeSpeed : jogSpeed;
            speed = Mathf.MoveTowards(speed, moveDir.magnitude * top, acceleration * Time.deltaTime);

            // face where you move; locked on (and not sprinting) you keep facing the target and strafe
            Vector3 face = LockTarget != null && !sprinting ? Flat(LockTarget.Body.position - transform.position)
                         : moveDir.sqrMagnitude > 0.01f ? moveDir : Vector3.zero;
            if (face.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-turnSmoothing * Time.deltaTime));

            Vector3 horizontal = (moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : Vector3.zero) * speed;
            Move(horizontal);
        }

        void UpdateAction()
        {
            actionTime += Time.deltaTime;
            float t = actionTime / actionLength;
            switch (action)
            {
                case Action.Roll:       // fast at first, easing out; invulnerable through the middle of it
                    stats.Invulnerable = t > 0.08f && t < 0.62f;
                    Move(actionDir * 7.2f * (1f - t) * (1f - t) * 1.6f);
                    break;
                case Action.Backstep:
                    stats.Invulnerable = t > 0.05f && t < 0.45f;
                    Move(actionDir * 5f * (1f - t));
                    break;
                case Action.Light:
                case Action.Heavy:
                {
                    bool heavy = action == Action.Heavy;
                    var weapon = Weapon;
                    // step into the swing as it lands (the thrust finisher lunges further)
                    float stepStart = heavy ? 0.3f : 0.2f, stepEnd = heavy ? 0.62f : 0.5f;
                    float push = heavy ? 2.2f : combo == 2 ? 2.6f : 1.5f;
                    if (t > stepStart && t < stepEnd) Move(actionDir * push * Mathf.Sin((t - stepStart) / (stepEnd - stepStart) * Mathf.PI));
                    else Move(Vector3.zero);
                    float baseDamage = weapon != null ? weapon.damage : lightDamage;
                    float dmg = (heavy ? (weapon != null ? weapon.damage * weapon.heavy : heavyDamage)
                                       : baseDamage * (combo == 2 ? 1.4f : 1f)) * stats.DamageMultiplier;
                    float hitStart = heavy ? HeavyHitStart : LightHitStart, hitEnd = heavy ? HeavyHitEnd : LightHitEnd;
                    if (weapon != null && weapon.ranged)
                    {
                        // the slingshot: one pebble (three in a fan for a heavy shot)
                        if (!shotFired && t > hitStart)
                        {
                            shotFired = true;
                            Vector3 from = transform.position + Vector3.up * 1.2f + actionDir * 0.5f;
                            Vector3 aim = LockTarget != null ? (LockTarget.Body.position + Vector3.up * 0.6f - from).normalized : actionDir;
                            var pebble = new Color(1f, 0.85f, 0.55f);
                            if (heavy)
                                foreach (var a in new[] { -9f, 0f, 9f })
                                    Projectile.Fire(gameObject, from, Quaternion.AngleAxis(a, Vector3.up) * aim, 22f, dmg / 2f, pebble, 0.16f);
                            else Projectile.Fire(gameObject, from, aim, 22f, dmg, pebble, 0.16f);
                        }
                    }
                    else if (t > hitStart && t < hitEnd)
                    {
                        // the hit lands in the strike window; longer weapons reach further
                        float reach = weapon != null ? weapon.reach : 0.85f;
                        float forward = (heavy ? 1.0f : combo == 2 ? 1.1f : 0.9f) * reach;
                        float radius = (heavy ? 1.0f : combo == 2 ? 0.7f : 0.85f) * Mathf.Lerp(1f, reach, 0.6f);
                        Hits.Sweep(transform.position + Vector3.up * 0.9f + actionDir * forward, radius, gameObject, dmg, hitThisSwing);
                    }
                    // chain: a buffered click continues the combo once the strike has landed
                    if (!heavy && t > 0.62f && (comboQueued || heavyQueued))
                    {
                        bool toHeavy = heavyQueued;
                        if (toHeavy ? stats.TrySpendStamina(heavyCost) : (combo < 2 && stats.TrySpendStamina(lightCost)))
                        {
                            if (toHeavy) { StartSwing(Action.Heavy, HeavyLength); return; }
                            combo++;
                            StartSwing(Action.Light, LightLength(combo));
                            return;
                        }
                        comboQueued = heavyQueued = false;
                    }
                    break;
                }
                case Action.Stagger:
                    Move(actionDir * 2f * (1f - t));
                    break;
            }
            if (actionTime >= actionLength)
            {
                if (action == Action.Roll || action == Action.Backstep) stats.Invulnerable = false;
                if (action == Action.Light) lastLightEnd = Time.time;
                speed = action == Action.Roll ? jogSpeed : 0f;   // come out of a roll already jogging
                action = Action.None;
            }
        }

        // Big hits knock you back and interrupt what you were doing (not while rolling).
        float lastHealth = -1f;
        void WatchForHits()
        {
            if (stats == null) return;
            if (lastHealth >= 0f && stats.health < lastHealth - 14f && action != Action.Roll && action != Action.Backstep && !stats.Dead)
            {
                Begin(Action.Stagger, 0.35f, -transform.forward);
                stats.Invulnerable = false;
            }
            lastHealth = stats.health;
        }

        void Gravity()
        {
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -1f;   // stay snapped to slopes
            verticalSpeed += gravity * Time.deltaTime;
            controller.Move(Vector3.up * verticalSpeed * Time.deltaTime);
        }

        void Move(Vector3 horizontal)
        {
            var before = transform.position;
            controller.Move(horizontal * Time.deltaTime);
            var flat = new Vector3(transform.position.x, 0, transform.position.z);
            // the walkable world: the circle around town, plus extra areas (the trail out to Mistpine Hollow and
            // the hollow itself, see PlayArea). Stepping outside all of them is undone.
            if (flat.magnitude > areaRadius && !PlayArea.AnyContains(transform.position))
            {
                if (new Vector3(before.x, 0, before.z).magnitude <= areaRadius || PlayArea.AnyContains(before))
                    controller.Move(new Vector3(before.x - transform.position.x, 0, before.z - transform.position.z));
                else   // already outside (old saves, warps): slide back toward town like before
                    controller.Move(-flat.normalized * (flat.magnitude - areaRadius));
            }
            if (motion == null) return;
            float strideBefore = motion.stridePhase;
            motion.stridePhase += horizontal.magnitude * Time.deltaTime / strideLength * Mathf.PI * 2f;
            // a footstep each time a foot comes down (twice per stride cycle), only walking on the ground
            if (action == Action.None && controller.isGrounded && Mathf.Floor(motion.stridePhase / Mathf.PI) != Mathf.Floor(strideBefore / Mathf.PI))
                DreamWorld.Audio.AudioManager.Footstep(transform.position);
        }

        // Teleport (respawning at home). The CharacterController has to be off to move it directly.
        public void Warp(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            action = Action.None;
            speed = verticalSpeed = 0f;
            LockTarget = null;
            if (stats != null) stats.Invulnerable = false;
        }

        // ---------- the model: roll flip, swing and guard poses ----------

        void Animate()
        {
            if (motion == null) return;
            float t = action == Action.None ? 0f : Mathf.Clamp01(actionTime / actionLength);
            motion.moveAmount = action == Action.None ? Mathf.Clamp01(speed / sprintSpeed) : 0.2f;
            bool armed = Weapon != null;   // armed: WeaponPose poses the arms instead
            motion.guard = Mathf.MoveTowards(motion.guard, !armed && stats != null && stats.GuardReduction > 0f ? 1f : 0f, Time.deltaTime * 8f);

            // a real forward roll: spin the model around its middle, tucked up
            float spin = 0f, tuck = 0f;
            if (action == Action.Roll) { spin = Mathf.SmoothStep(0f, 360f, t); tuck = Mathf.Sin(t * Mathf.PI); }
            if (action == Action.Backstep) { spin = -Mathf.Sin(t * Mathf.PI) * 18f; tuck = Mathf.Sin(t * Mathf.PI) * 0.4f; }
            if (action == Action.Stagger) spin = -Mathf.Sin(t * Mathf.PI) * 14f;
            motion.tuck = tuck;
            var pivot = new Vector3(0f, 0.5f, 0f);
            var rot = Quaternion.AngleAxis(spin, Vector3.right);
            model.localRotation = modelBaseRot * rot;
            model.localPosition = modelBasePos + (pivot - rot * pivot);
            // heavy with a weapon: a little hop up into the slam, then a dip as it lands
            if (armed && action == Action.Heavy)
            {
                float hop = t < 0.3f ? 0f
                          : t < 0.5f ? Mathf.Sin((t - 0.3f) / 0.2f * Mathf.PI * 0.5f) * 0.2f
                          : t < 0.6f ? (1f - (t - 0.5f) / 0.1f) * 0.2f
                          : -Mathf.Sin(Mathf.Clamp01((t - 0.6f) / 0.25f) * Mathf.PI) * 0.07f;
                model.localPosition += Vector3.up * hop;
            }

            // swing: wind up, strike, follow through (heavy uses both arms)
            float swing = 0f;
            if (!armed && (action == Action.Light || action == Action.Heavy))
            {
                float strike = action == Action.Heavy ? 0.45f : 0.3f;
                swing = t < strike ? -0.35f * Mathf.Sin(t / strike * Mathf.PI * 0.5f)
                                   : Mathf.Sin(Mathf.Clamp01((t - strike) / (1f - strike)) * Mathf.PI);
            }
            motion.attack = swing;
            motion.attackArm = action == Action.Heavy ? 2 : combo % 2;   // alternate arms, 2 = both
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }
    }
}
