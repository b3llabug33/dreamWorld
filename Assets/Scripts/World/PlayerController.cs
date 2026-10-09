using System.Collections.Generic;
using DreamWorld.CharacterCreation;
using DreamWorld.Gameplay;
using UnityEngine;

namespace DreamWorld.World
{
    // Elden Ring-style third-person controls (keyboard + mouse, same keys as Elden Ring on PC):
    //   WASD move (jog)         Space: tap = roll (i-frames), hold = sprint      F: jump
    //   Left click: attack (3-hit combo)   Shift + left click: heavy attack      Right click: guard
    //   Q: lock on / off (strafe around the target)   E: interact   R: eat a snack
    // Rolling, sprinting, jumping, attacking and guarding all cost stamina (PlayerStats).
    // Getting hit hard staggers you for a moment. The model rolls/punches through IdleMotion.
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

        [Header("Attacks")]
        public float lightDamage = 12f;
        public float heavyDamage = 28f;

        public bool InputEnabled { get; set; } = true;   // menus turn this off
        // Lets a script steer instead of the keyboard (recording clips, cutscenes later).
        public bool UseScriptedInput { get; set; }
        public Vector2 ScriptedInput { get; set; }
        public IDamageable LockTarget { get; private set; }
        public bool Busy => action != Action.None;

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
        bool comboQueued, heavyQueued;
        float spaceDown = -1f;
        bool sprinting, sprintLocked;
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
            sprinting = holding && !sprintLocked && moveDir.sqrMagnitude > 0.01f && action == Action.None;

            if (Input.GetKeyDown(KeyCode.F)) Jump();
            if (Input.GetMouseButtonDown(0))
            {
                bool heavy = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (heavy) HeavyAttack(); else LightAttack();
            }
            if (Input.GetKeyDown(KeyCode.Q)) ToggleLockOn();
            if (Input.GetKeyDown(KeyCode.R) && stats != null) stats.EatSnack();
            if (stats != null) stats.GuardReduction = Input.GetMouseButton(1) && action == Action.None ? 0.75f : 0f;
        }

        // Public so menus/tests can trigger them too.
        public void Roll()
        {
            if (action != Action.None || !controller.isGrounded || stats == null || !stats.TrySpendStamina(rollCost)) return;
            bool moving = moveDir.sqrMagnitude > 0.01f;
            Begin(moving ? Action.Roll : Action.Backstep, moving ? 0.6f : 0.4f, moving ? moveDir.normalized : -transform.forward);
            if (moving) transform.rotation = Quaternion.LookRotation(actionDir);
        }

        public void Jump()
        {
            if (action != Action.None || !controller.isGrounded || stats == null || !stats.TrySpendStamina(jumpCost)) return;
            verticalSpeed = jumpSpeed;
        }

        public void LightAttack()
        {
            if (action == Action.Light) { comboQueued = true; return; }   // buffer the next hit of the combo
            if (action != Action.None || stats == null || !stats.TrySpendStamina(lightCost)) return;
            combo = 0;
            StartSwing(Action.Light, 0.55f);
        }

        public void HeavyAttack()
        {
            if (action == Action.Light) { heavyQueued = true; return; }
            if (action != Action.None || stats == null || !stats.TrySpendStamina(heavyCost)) return;
            StartSwing(Action.Heavy, 1.0f);
        }

        void StartSwing(Action kind, float length)
        {
            Vector3 dir = LockTarget != null ? Flat(LockTarget.Body.position - transform.position)
                        : moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : transform.forward;
            Begin(kind, length, dir);
            transform.rotation = Quaternion.LookRotation(actionDir);
            hitThisSwing.Clear();
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
                    float lungeEnd = heavy ? 0.55f : 0.4f;
                    if (t < lungeEnd) Move(actionDir * (heavy ? 1.8f : 1.4f) * (1f - t / lungeEnd));
                    else Move(Vector3.zero);
                    // the hit lands in a short window mid-swing
                    bool active = heavy ? (t > 0.42f && t < 0.6f) : (t > 0.25f && t < 0.45f);
                    if (active)
                    {
                        float dmg = (heavy ? heavyDamage : lightDamage * (combo == 2 ? 1.4f : 1f)) * stats.DamageMultiplier;
                        Hits.Sweep(transform.position + Vector3.up * 0.9f + actionDir * 0.9f, heavy ? 0.95f : 0.75f, gameObject, dmg, hitThisSwing);
                    }
                    // chain: a buffered click continues the combo after the hit window
                    if (!heavy && t > 0.6f && (comboQueued || heavyQueued))
                    {
                        bool toHeavy = heavyQueued;
                        if (toHeavy ? stats.TrySpendStamina(heavyCost) : (combo < 2 && stats.TrySpendStamina(lightCost)))
                        {
                            if (toHeavy) { StartSwing(Action.Heavy, 1.0f); return; }
                            combo++;
                            StartSwing(Action.Light, combo == 2 ? 0.7f : 0.55f);
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
            controller.Move(horizontal * Time.deltaTime);
            var flat = new Vector3(transform.position.x, 0, transform.position.z);
            if (flat.magnitude > areaRadius)   // soft boundary: slide back inside the area
                controller.Move(-flat.normalized * (flat.magnitude - areaRadius));
            if (motion != null) motion.stridePhase += horizontal.magnitude * Time.deltaTime / strideLength * Mathf.PI * 2f;
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
            motion.guard = Mathf.MoveTowards(motion.guard, stats != null && stats.GuardReduction > 0f ? 1f : 0f, Time.deltaTime * 8f);

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

            // swing: wind up, strike, follow through (heavy uses both arms)
            float swing = 0f;
            if (action == Action.Light || action == Action.Heavy)
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
