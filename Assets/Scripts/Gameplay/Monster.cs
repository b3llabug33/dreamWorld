using System;
using UnityEngine;

namespace DreamWorld.Gameplay
{
    // A dream monster. It wanders near where it spawned, chases players it can see outside safe zones,
    // and attacks with a clear tell first (a wind-up you can see and roll through). It never steps into
    // a safe zone. Killing it gives XP, coins (more with luck) and drops into the killer's bag.
    // The kind decides how it fights:
    //   Hopper   (shroomling)     squashes down and flashes, then hops at you
    //   Waddler  (cursed plush)   waddles over, wobbles, then belly-flops
    //   Slammer  (moss golem)     slow tank: raises up high, then slams the ground around it
    //   Charger  (thorn beetle)   lowers its head and scrapes, then charges in a straight line
    //   Flyer    (glitch moth, night bat)  hovers, climbs up shivering, then swoops down through you
    //   Spitter  (rot slug)       keeps its distance, swells up, spits a glob of slime
    //   Zapper   (static wisp)    floats at range, flickers, then fires a fast zap
    //   Lunger   (ivy serpent)    hides in the ground until you're close, rears up, then lunges
    //   Knight   (mushroom knight) mini-boss: two telegraphed slashes, then a jumping slam
    public class Monster : MonoBehaviour, IDamageable
    {
        public enum Kind { Hopper, Waddler, Slammer, Charger, Flyer, Spitter, Zapper, Lunger, Knight }

        public Kind kind = Kind.Hopper;
        public string displayName = "shroomling";
        public float maxHealth = 45f;
        public float damage = 12f;
        public int xpReward = 25;
        public Vector2Int coinReward = new Vector2Int(6, 12);

        [Serializable]
        public class Drop { public string item; public int min = 1, max = 1; [Range(0, 1)] public float chance = 1f; }
        // What it leaves behind (goes straight into the killer's bag).
        public Drop[] drops =
        {
            new Drop { item = "spore_cap", min = 1, max = 2, chance = 1f },
            new Drop { item = "glow_jelly", chance = 0.08f },
            new Drop { item = "dream_shard", chance = 0.06f },
        };
        public float sightRange = 9f;
        public float leashRange = 14f;     // gives up if dragged this far from home
        public float moveSpeed = 2.3f;
        public float attackRange = 2.2f;
        public float windupSeconds = 0.6f;
        public float recoverSeconds = 1.4f;
        [Tooltip("Ranged kinds try to stay about this far away.")]
        public float keepAway = 6f;
        public float projectileSpeed = 8f;
        public Color tellColor = new Color(1f, 0.55f, 0.75f);
        [Tooltip("Heavy hits don't interrupt its wind-up.")]
        public bool poise;
        [Tooltip("Where its health bar floats (meters above the ground).")]
        public float barHeight = 1.3f;
        [Tooltip("The visible mesh (squashes, hops, flies); the root stays on the ground. Hover height = its starting height.")]
        public Transform body;

        public static event Action<Monster, PlayerStats> Killed;

        public float Health { get; private set; }
        public float HealthFraction => Health / maxHealth;
        public float LastHitTime { get; private set; } = -99f;
        public Transform Body => transform;
        public bool Alive => Health > 0f;
        public MonsterSpawner Spawner { get; set; }
        public bool WindingUp => state == State.Windup;

        enum State { Wander, Chase, Windup, Attack, Recover, Dead, Hidden }
        State state;
        float stateTime, attackLength, strafeSign = 1f;
        Vector3 home, wanderTarget, lungeDir, swoopFrom, swoopTo;
        bool attackHit;
        int comboStep;   // knight: 0, 1 = slashes, 2 = jump slam
        Vector3 bodyBaseScale, bodyBasePos;
        Quaternion bodyBaseRot;
        Renderer[] renderers;
        MaterialPropertyBlock block;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        bool Flies => kind == Kind.Flyer || kind == Kind.Zapper;

        void Awake()
        {
            if (body == null) body = transform.GetChild(0);
            bodyBaseScale = body.localScale;
            bodyBasePos = body.localPosition;
            bodyBaseRot = body.localRotation;
            renderers = body.GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            Health = maxHealth;
            home = transform.position;
            wanderTarget = home;
            comboStep = 0;
            ResetBody();
            Enter(kind == Kind.Lunger ? State.Hidden : State.Wander);
            Tint(Color.white);
        }

        void ResetBody()
        {
            body.localScale = bodyBaseScale;
            body.localPosition = bodyBasePos;
            body.localRotation = bodyBaseRot;
        }

        void Enter(State s)
        {
            state = s;
            stateTime = 0f;
        }

        // ---------- the brain ----------

        void Update()
        {
            stateTime += Time.deltaTime;
            var player = PlayerStats.Local;
            bool canChase = player != null && player.Alive && !ZoneMap.IsSafe(player.transform.position)
                            && Vector3.Distance(home, transform.position) < leashRange;
            float dist = player != null ? Flat(player.transform.position - transform.position).magnitude : 999f;

            switch (state)
            {
                case State.Hidden:   // lunger: just the tips of its leaves poke out of the ground
                    body.localPosition = bodyBasePos + Vector3.down * 0.42f + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.02f;
                    body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f, 0.5f, 1f));
                    if (canChase && dist < 4.5f) { if (player != null) Face(player.transform.position, 30f); BeginWindup(player); }
                    break;
                case State.Wander:
                    if (canChase && dist < sightRange) { Enter(State.Chase); break; }
                    if (kind == Kind.Lunger && Flat(home - transform.position).magnitude < 0.5f && stateTime > 2f) { Enter(State.Hidden); break; }
                    if (Flat(wanderTarget - transform.position).magnitude < 0.3f || stateTime > 6f)
                    {
                        var r = UnityEngine.Random.insideUnitCircle * 3f;
                        wanderTarget = kind == Kind.Lunger ? home : home + new Vector3(r.x, 0, r.y);
                        stateTime = kind == Kind.Lunger ? stateTime : 0f;
                    }
                    Step(wanderTarget, moveSpeed * 0.4f);
                    Idle(0.6f);
                    break;
                case State.Chase:
                    if (!canChase || dist > sightRange * 1.5f) { Enter(State.Wander); wanderTarget = home; break; }
                    Face(player.transform.position);
                    if (WantsToAttack(dist)) { BeginWindup(player); break; }
                    Approach(player.transform.position, dist);
                    Idle(1f);
                    break;
                case State.Windup:
                    if (player != null && kind != Kind.Charger) Face(player.transform.position, 6f);
                    Tell(Mathf.Clamp01(stateTime / windupSeconds));
                    if (stateTime >= windupSeconds) BeginAttack(player);
                    break;
                case State.Attack:
                    Attack(player, Mathf.Clamp01(stateTime / attackLength));
                    if (stateTime >= attackLength) { Enter(State.Recover); attackHit = false; }
                    break;
                case State.Recover:
                    Settle();
                    if (stateTime > RecoverTime)
                    {
                        if (kind == Kind.Knight && comboStep < 2 && canChase && dist < attackRange + 2f) { comboStep++; BeginWindup(player); break; }
                        comboStep = 0;
                        Enter(canChase ? State.Chase : State.Wander);
                    }
                    break;
                case State.Dead:     // shrink away (flyers fall), then the spawner brings it back later
                    body.localScale = Vector3.Lerp(body.localScale, Vector3.zero, Time.deltaTime * 6f);
                    if (Flies) body.localPosition = Vector3.Lerp(body.localPosition, new Vector3(bodyBasePos.x, 0.2f, bodyBasePos.z), Time.deltaTime * 8f);
                    if (stateTime > 0.6f)
                    {
                        if (Spawner != null) Spawner.Despawned(this);
                        else gameObject.SetActive(false);
                    }
                    break;
            }
            if (state != State.Windup && state != State.Dead && Time.time - LastHitTime > 0.12f) Tint(Color.white);
        }

        // knight slashes chain quickly; the slam leaves it open for longer
        float RecoverTime => kind == Kind.Knight ? (comboStep < 2 ? 0.25f : recoverSeconds) : recoverSeconds;

        bool WantsToAttack(float dist)
        {
            if (stateTime < 0.5f) return false;   // a moment between attacks
            switch (kind)
            {
                case Kind.Spitter:
                case Kind.Zapper: return dist < keepAway + 2.5f && stateTime > 1f;
                case Kind.Charger: return dist < 7.5f && (dist > 2.5f || dist < attackRange);
                case Kind.Lunger: return dist < 4f;
                case Kind.Flyer: return dist < 4.5f;
                default: return dist < attackRange;
            }
        }

        void Approach(Vector3 target, float dist)
        {
            if (kind == Kind.Spitter || kind == Kind.Zapper)
            {
                // ranged: back off when you get close, circle at a distance
                Vector3 away = Flat(transform.position - target).normalized;
                if (dist < keepAway - 1.5f) StepDir(away, moveSpeed * 0.8f);
                else if (dist > keepAway + 1f) Step(target, moveSpeed);
                else StepDir(Vector3.Cross(Vector3.up, away) * strafeSign, moveSpeed * 0.4f);
                if (UnityEngine.Random.value < Time.deltaTime * 0.3f) strafeSign = -strafeSign;
                Face(target);
                return;
            }
            if (kind == Kind.Flyer && dist < 3.5f)   // flutter around you before swooping
            {
                StepDir(Vector3.Cross(Vector3.up, Flat(transform.position - target).normalized) * strafeSign, moveSpeed * 0.6f);
                Face(target);
                return;
            }
            Step(target, moveSpeed);
        }

        // ---------- attacks ----------

        void BeginWindup(PlayerStats player)
        {
            attackHit = false;
            Enter(State.Windup);
        }

        void BeginAttack(PlayerStats player)
        {
            Vector3 target = player != null ? player.transform.position : transform.position + transform.forward * 3f;
            lungeDir = Flat(target - transform.position).normalized;
            if (lungeDir.sqrMagnitude < 0.01f) lungeDir = transform.forward;
            attackHit = false;
            Tint(Color.white);
            attackLength = 0.38f;
            switch (kind)
            {
                case Kind.Slammer: attackLength = 0.32f; break;
                case Kind.Charger: attackLength = 0.75f; break;
                case Kind.Flyer:
                    attackLength = 0.65f;
                    swoopFrom = transform.position;
                    swoopTo = target + lungeDir * 2.5f;   // swoops right through you and out the other side
                    break;
                case Kind.Spitter:
                case Kind.Zapper:
                {
                    attackLength = 0.3f;
                    Vector3 mouth = body.position + transform.forward * (kind == Kind.Spitter ? 0.9f : 0.3f) + Vector3.up * (kind == Kind.Spitter ? 0.25f : 0f);
                    Vector3 aim = (target + Vector3.up * 0.9f - mouth).normalized;
                    Projectile.Fire(gameObject, mouth, aim, projectileSpeed, damage, tellColor, kind == Kind.Spitter ? 0.32f : 0.2f);
                    break;
                }
                case Kind.Lunger: attackLength = 0.4f; break;
                case Kind.Knight: attackLength = comboStep < 2 ? 0.28f : 0.7f; swoopFrom = transform.position;
                    swoopTo = transform.position + lungeDir * Mathf.Clamp(Flat(target - transform.position).magnitude - 1f, 0f, 5f); break;
            }
            Enter(State.Attack);
        }

        // The tell: every kind shows its attack coming. k = 0..1 through the wind-up.
        void Tell(float k)
        {
            bool flash = Mathf.PingPong(stateTime * 6f, 1f) > 0.5f;
            Tint(Color.Lerp(Color.white, tellColor, flash ? 1f : 0.25f));
            switch (kind)
            {
                case Kind.Hopper:
                case Kind.Waddler:   // squash down
                    body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f + 0.25f * k, 1f - 0.35f * k, 1f + 0.25f * k));
                    if (kind == Kind.Waddler) body.localRotation = bodyBaseRot * Quaternion.Euler(0, 0, Mathf.Sin(stateTime * 22f) * 12f * k);
                    break;
                case Kind.Slammer:   // rear up high and lean back
                    body.localPosition = bodyBasePos + Vector3.up * 0.35f * Smooth(k);
                    body.localRotation = bodyBaseRot * Quaternion.Euler(-14f * Smooth(k), 0, 0);
                    body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f, 1f + 0.12f * k, 1f));
                    break;
                case Kind.Charger:   // head down, scraping, backing up a little
                    body.localRotation = bodyBaseRot * Quaternion.Euler(10f * k, 0, Mathf.Sin(stateTime * 30f) * 4f);
                    body.localPosition = bodyBasePos + Vector3.down * 0.06f * k;
                    StepDir(-transform.forward, 0.5f, face: false);
                    break;
                case Kind.Flyer:     // climbs, shivering (the glitch moth jitters around)
                    body.localPosition = bodyBasePos + Vector3.up * 1.1f * Smooth(k)
                                       + (displayName.Contains("moth") ? UnityEngine.Random.insideUnitSphere * 0.08f * k : Vector3.zero);
                    Flap(2.2f);
                    break;
                case Kind.Spitter:   // swells up
                    body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f + 0.18f * k, 1f + 0.22f * k, 1f - 0.08f * k));
                    body.localRotation = bodyBaseRot * Quaternion.Euler(-10f * k, 0, 0);
                    break;
                case Kind.Zapper:    // flickers and buzzes
                    body.localPosition = bodyBasePos + UnityEngine.Random.insideUnitSphere * 0.06f * k;
                    body.localScale = bodyBaseScale * (1f + 0.15f * k * Mathf.PingPong(stateTime * 14f, 1f));
                    Tint(Mathf.PingPong(stateTime * 14f, 1f) > 0.5f ? tellColor : new Color(2f, 2f, 2f));
                    break;
                case Kind.Lunger:    // rises out of the ground and rears back
                    body.localPosition = bodyBasePos + Vector3.down * Mathf.Lerp(0.42f, 0f, Smooth(Mathf.Clamp01(k * 1.6f))) + Vector3.up * 0.12f * k;
                    body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f, Mathf.Lerp(0.5f, 1.15f, Smooth(k)), 1f));
                    body.localRotation = bodyBaseRot * Quaternion.Euler(-22f * Smooth(k), 0, 0);
                    break;
                case Kind.Knight:
                    if (comboStep < 2)   // sword back, twisting away
                        body.localRotation = bodyBaseRot * Quaternion.Euler(-8f * k, (comboStep == 0 ? -35f : 35f) * Smooth(k), 0);
                    else                 // crouch for the jump
                        body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f + 0.1f * k, 1f - 0.18f * k, 1f + 0.1f * k));
                    break;
            }
        }

        void Attack(PlayerStats player, float k)
        {
            switch (kind)
            {
                case Kind.Hopper:
                case Kind.Waddler:
                {
                    Move(lungeDir * (kind == Kind.Waddler ? 4.5f : 6f) * (1f - k));
                    body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(0.9f, 1.15f, 0.9f));
                    body.localPosition = bodyBasePos + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.45f;
                    if (kind == Kind.Waddler) body.localRotation = bodyBaseRot * Quaternion.Euler(70f * k, 0, 0);   // belly-flop
                    if (k > 0.3f) HitIfClose(player, transform.position + Vector3.up * 0.4f, 1.15f);
                    if (k >= 1f) body.localPosition = bodyBasePos;
                    break;
                }
                case Kind.Slammer:
                    body.localPosition = bodyBasePos + Vector3.up * Mathf.Lerp(0.35f, -0.05f, k * k);
                    body.localRotation = bodyBaseRot * Quaternion.Euler(Mathf.Lerp(-14f, 16f, k * k), 0, 0);
                    if (k > 0.65f && !attackHit) Slam(player, transform.position + transform.forward * 1.2f, 2.4f, new Color(0.55f, 0.75f, 0.4f, 0.8f));
                    break;
                case Kind.Charger:
                    Move(lungeDir * 10f * (1f - k * 0.6f));
                    body.localRotation = bodyBaseRot * Quaternion.Euler(12f, 0, Mathf.Sin(stateTime * 40f) * 3f);
                    HitIfClose(player, transform.position + Vector3.up * 0.6f + transform.forward * 0.6f, 1.3f);
                    break;
                case Kind.Flyer:
                {
                    // swoop: down through the player's chest height and back up past them
                    Vector3 p = Vector3.Lerp(swoopFrom, swoopTo, Smooth(k));
                    if (!ZoneMap.IsSafe(p)) transform.position = new Vector3(p.x, transform.position.y, p.z);
                    float high = bodyBasePos.y + 1.1f;
                    body.localPosition = new Vector3(bodyBasePos.x, Mathf.Lerp(high, 0.75f, Mathf.Sin(k * Mathf.PI)), bodyBasePos.z);
                    body.localRotation = bodyBaseRot * Quaternion.Euler(Mathf.Cos(k * Mathf.PI) * 30f, 0, 0);
                    Flap(1.6f);
                    HitIfClose(player, body.position, 1.0f);
                    break;
                }
                case Kind.Spitter:
                case Kind.Zapper:   // recoil after the shot
                    body.localScale = Vector3.Lerp(body.localScale, bodyBaseScale, Time.deltaTime * 12f);
                    body.localRotation = bodyBaseRot * Quaternion.Euler(Mathf.Sin(k * Mathf.PI) * 12f, 0, 0);
                    break;
                case Kind.Lunger:
                    Move(lungeDir * 11f * (1f - k));
                    body.localRotation = bodyBaseRot * Quaternion.Euler(Mathf.Lerp(-22f, 14f, Smooth(k * 2f)), 0, Mathf.Sin(k * Mathf.PI * 3f) * 10f);
                    body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(0.95f, 1f, 1.25f));
                    HitIfClose(player, transform.position + Vector3.up * 0.4f + transform.forward * 0.8f, 1.2f);
                    break;
                case Kind.Knight:
                    if (comboStep < 2)
                    {
                        // a wide slash in front of it, the body whipping through
                        body.localRotation = bodyBaseRot * Quaternion.Euler(6f, Mathf.Lerp(comboStep == 0 ? -35f : 35f, comboStep == 0 ? 40f : -40f, Smooth(k)), 0);
                        Move(lungeDir * 2.5f * (1f - k));
                        if (!attackHit && player != null && player.Alive && k > 0.3f && k < 0.8f)
                        {
                            Vector3 to = player.transform.position - transform.position;
                            if (Flat(to).magnitude < 2.4f && Vector3.Angle(transform.forward, Flat(to)) < 75f) { attackHit = true; player.TakeDamage(damage, gameObject); }
                        }
                    }
                    else
                    {
                        // the jumping slam: up, over, down hard
                        Vector3 p = Vector3.Lerp(swoopFrom, swoopTo, Smooth(k));
                        if (!ZoneMap.IsSafe(p)) transform.position = p;
                        body.localScale = bodyBaseScale;
                        body.localPosition = bodyBasePos + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.3f;
                        body.localRotation = bodyBaseRot * Quaternion.Euler(Mathf.Lerp(-10f, 18f, k), 0, 0);
                        if (k > 0.9f && !attackHit) Slam(player, transform.position + transform.forward * 0.6f, 2.7f, new Color(0.9f, 0.85f, 0.6f, 0.8f), 1.6f);
                    }
                    break;
            }
        }

        void HitIfClose(PlayerStats player, Vector3 point, float reach)
        {
            if (attackHit || player == null || !player.Alive) return;
            if ((player.transform.position + Vector3.up * 0.8f - point).magnitude < reach)
            {
                attackHit = true;
                player.TakeDamage(damage, gameObject);
            }
        }

        void Slam(PlayerStats player, Vector3 center, float radius, Color dust, float damageScale = 1f)
        {
            attackHit = true;
            Shockwave.Spawn(new Vector3(center.x, transform.position.y, center.z), radius, dust);
            if (player != null && player.Alive && Flat(player.transform.position - center).magnitude < radius
                && player.transform.position.y < transform.position.y + 1.2f)   // jumping clears it
                player.TakeDamage(damage * damageScale, gameObject);
        }

        // ---------- body motion ----------

        void Idle(float amount)
        {
            float t = Time.time + home.x;
            switch (kind)
            {
                case Kind.Hopper: Bounce(amount); break;
                case Kind.Waddler:   // side to side
                    body.localRotation = bodyBaseRot * Quaternion.Euler(0, 0, Mathf.Sin(t * 7f) * 10f * amount);
                    body.localPosition = bodyBasePos + Vector3.up * Mathf.Abs(Mathf.Sin(t * 7f)) * 0.05f * amount;
                    break;
                case Kind.Slammer:   // heavy stomping sway
                    body.localRotation = bodyBaseRot * Quaternion.Euler(0, 0, Mathf.Sin(t * 3.2f) * 4f * amount);
                    body.localPosition = bodyBasePos + Vector3.up * Mathf.Abs(Mathf.Sin(t * 3.2f)) * 0.04f;
                    break;
                case Kind.Charger:   // skittering
                    body.localPosition = bodyBasePos + Vector3.up * Mathf.Abs(Mathf.Sin(t * 14f)) * 0.02f * amount;
                    body.localRotation = bodyBaseRot * Quaternion.Euler(0, Mathf.Sin(t * 14f) * 3f * amount, 0);
                    break;
                case Kind.Flyer:
                    body.localPosition = bodyBasePos + Vector3.up * Mathf.Sin(t * 2.6f) * 0.18f;
                    body.localRotation = bodyBaseRot * Quaternion.Euler(Mathf.Sin(t * 1.3f) * 6f, 0, Mathf.Sin(t * 1.9f) * 10f);
                    Flap(1f);
                    break;
                case Kind.Spitter:   // inching along
                    body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f, 1f - Mathf.Sin(t * 4f) * 0.05f * amount, 1f + Mathf.Sin(t * 4f) * 0.08f * amount));
                    break;
                case Kind.Zapper:
                    body.localPosition = bodyBasePos + Vector3.up * Mathf.Sin(t * 2f) * 0.12f;
                    body.localRotation = bodyBaseRot * Quaternion.Euler(0, t * 90f, 0);
                    break;
                case Kind.Lunger:    // slithering
                    body.localPosition = bodyBasePos;
                    body.localScale = bodyBaseScale;
                    body.localRotation = bodyBaseRot * Quaternion.Euler(0, Mathf.Sin(t * 5f) * 14f * amount, 0);
                    break;
                case Kind.Knight:    // marching
                    body.localPosition = bodyBasePos + Vector3.up * Mathf.Abs(Mathf.Sin(t * 6f)) * 0.06f * amount;
                    body.localRotation = bodyBaseRot * Quaternion.Euler(0, 0, Mathf.Sin(t * 6f) * 4f * amount);
                    body.localScale = bodyBaseScale;
                    break;
            }
        }

        void Settle()
        {
            float f = 1f - Mathf.Exp(-8f * Time.deltaTime);
            body.localScale = Vector3.Lerp(body.localScale, bodyBaseScale, f);
            body.localPosition = Vector3.Lerp(body.localPosition, bodyBasePos, f);
            if (kind == Kind.Charger)   // dizzy after a charge
                body.localRotation = bodyBaseRot * Quaternion.Euler(0, 0, Mathf.Sin(stateTime * 9f) * 10f * Mathf.Max(0f, 1f - stateTime / recoverSeconds));
            else body.localRotation = Quaternion.Slerp(body.localRotation, bodyBaseRot, f);
            if (Flies) Flap(1f);
        }

        // Wings beat by squashing the model sideways (moths and bats).
        void Flap(float speed)
        {
            if (kind != Kind.Flyer) return;
            float s = Mathf.Sin(Time.time * 18f * speed + home.z);
            body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f + 0.18f * s, 1f - 0.06f * s, 1f));
        }

        void Bounce(float amount)
        {
            float b = Mathf.Abs(Mathf.Sin(Time.time * 9f)) * amount;
            body.localPosition = bodyBasePos + Vector3.up * b * 0.08f;
            body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f + b * 0.05f, 1f - b * 0.06f, 1f + b * 0.05f));
        }

        static float Smooth(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

        // ---------- moving ----------

        void Step(Vector3 toward, float speed)
        {
            Vector3 dir = Flat(toward - transform.position);
            if (dir.sqrMagnitude < 0.0001f) return;
            StepDir(dir.normalized, speed);
        }

        void StepDir(Vector3 dir, float speed, bool face = true)
        {
            Vector3 next = transform.position + dir * speed * Time.deltaTime;
            if (ZoneMap.IsSafe(next)) return;   // stops at the edge of a safe zone
            transform.position = next;
            if (face && kind != Kind.Spitter && kind != Kind.Zapper) Face(next + dir);
        }

        void Move(Vector3 velocity)
        {
            Vector3 next = transform.position + velocity * Time.deltaTime;
            if (!ZoneMap.IsSafe(next)) transform.position = next;
        }

        void Face(Vector3 point, float rate = 10f)
        {
            Vector3 d = Flat(point - transform.position);
            if (d.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-rate * Time.deltaTime));
        }

        // Tints the whole model (keeps each material's own alpha, so glass stays see-through).
        void Tint(Color c)
        {
            if (block == null) block = new MaterialPropertyBlock();   // lost if scripts recompile during Play
            if (renderers == null) renderers = body.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    r.GetPropertyBlock(block, i);
                    float a = mats[i] != null && mats[i].HasProperty(ColorId) ? mats[i].GetColor(ColorId).a : 1f;
                    block.SetColor(ColorId, new Color(c.r, c.g, c.b, a));
                    r.SetPropertyBlock(block, i);
                }
            }
        }

        // ---------- getting hit ----------

        public bool TakeDamage(float amount, GameObject source)
        {
            if (!Alive) return false;
            Health -= amount;
            LastHitTime = Time.time;
            Tint(new Color(2f, 2f, 2f));   // white hit flash
            if (source != null && kind != Kind.Knight && kind != Kind.Slammer)   // a little knockback (not the big ones)
            {
                Vector3 away = Flat(transform.position - source.transform.position).normalized * Mathf.Min(0.6f, amount * 0.02f);
                if (!ZoneMap.IsSafe(transform.position + away)) transform.position += away;
            }
            if (Health <= 0f)
            {
                Health = 0f;
                Enter(State.Dead);
                var killer = source != null ? source.GetComponent<PlayerStats>() : null;
                if (killer != null)
                {
                    killer.AddXp(xpReward);
                    killer.AddCoins(UnityEngine.Random.Range(coinReward.x, coinReward.y + 1));
                    foreach (var drop in drops)
                    {
                        if (UnityEngine.Random.value > drop.chance) continue;
                        int n = UnityEngine.Random.Range(drop.min, drop.max + 1);
                        int got = killer.AddItem(drop.item, n);
                        var def = Items.Get(drop.item);
                        if (got > 0) GameHUD.Instance?.Toast($"+{got} {def.name}");
                        if (got < n) GameHUD.Instance?.Toast("your bag is full");
                    }
                }
                Killed?.Invoke(this, killer);
            }
            else if (amount >= 20f && state == State.Windup && !poise) { ResetBody(); Enter(State.Recover); }   // heavy hits interrupt the tell
            else if (state == State.Wander || state == State.Hidden) Enter(State.Chase);
            return true;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
