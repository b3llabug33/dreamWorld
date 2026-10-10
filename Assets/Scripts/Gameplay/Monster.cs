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
    // On top of its kind, any monster can have special moves (mix and match per prefab):
    //   Enrage   below 40% health it turns red and gets faster and meaner
    //   Quake    glows and rises up, then two shockwaves roll out (jump or roll through them)
    //   Volley   a fan of five shots
    //   Blink    flickers out and reappears behind you, then strikes
    //   Frenzy   follows an attack with a quick second one
    //   Summon   once, when it's hurt, calls two little copies of itself
    // Elites are bigger, tougher versions with a golden shimmer and two or three specials.
    public class Monster : MonoBehaviour, IDamageable
    {
        public enum Kind { Hopper, Waddler, Slammer, Charger, Flyer, Spitter, Zapper, Lunger, Knight }

        [Flags] public enum Special { None = 0, Enrage = 1, Quake = 2, Volley = 4, Blink = 8, Frenzy = 16, Summon = 32 }
        public Special specials;
        [Tooltip("Seconds between special moves (Quake, Volley, Blink).")]
        public float specialCooldown = 7f;
        public bool elite;
        [NonSerialized] public bool minion;   // a little summoned copy: no drops, goes away for good when beaten

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

        // ---------- online (see Net/NetMonster.cs) ----------
        // Who it goes after: online the server picks the nearest player; solo (null) it's you.
        public static Func<Vector3, PlayerStats> TargetPicker;
        // A copy on a client: the server runs the real one, this one only shows it (its body is synced).
        public bool Puppet { get; set; }
        // Client copies: hits are sent to the server instead of landing here.
        public Func<float, GameObject, bool> HitRedirect;
        // Server: effects to replay for everyone (kind, position, direction/extra, size, color).
        public Action<string, Vector3, Vector3, float, Color> FxSent;
        // Server: someone else's kill. Their own game adds the XP, coins and drops ("id:n;id:n").
        public Action<PlayerStats, int, int, string> RewardRedirect;
        public bool Enraged => enraged;
        public void SetNetState(float health, bool enrage) { Health = health; enraged = enrage; }
        public void FlashHit() { LastHitTime = Time.time; Tint(new Color(2f, 2f, 2f)); }
        public static void RaiseKilled(Monster m, PlayerStats killer) => Killed?.Invoke(m, killer);

        void Shot(Vector3 from, Vector3 dir, float speed, float damage, float size)
        {
            Projectile.Fire(gameObject, from, dir, speed, damage, tellColor, size);
            FxSent?.Invoke("shot", from, dir.normalized * speed, size, tellColor);
        }

        void Wave(Vector3 at, float radius, Color color)
        {
            Shockwave.Spawn(at, radius, color);
            FxSent?.Invoke("wave", at, Vector3.zero, radius, color);
        }

        void RingFx(Vector3 at, Color color, float radius, float seconds)
        {
            SpellFx.Ring(at, color, radius, seconds);
            FxSent?.Invoke("ring", at, new Vector3(seconds, 0, 0), radius, color);
        }

        public float Health { get; private set; }
        public float HealthFraction => Health / maxHealth;
        public float LastHitTime { get; private set; } = -99f;
        public Transform Body => transform;
        public bool Alive => Health > 0f;
        public MonsterSpawner Spawner { get; set; }
        public bool WindingUp => state == State.Windup;

        enum State { Wander, Chase, Windup, Attack, Recover, Dead, Hidden, SpecialWindup, SpecialAct }
        State state;
        Special move;                 // the special being done right now
        float nextSpecial;
        bool enraged, summoned, frenzied;
        float minionUntil;
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
            enraged = summoned = frenzied = false;
            nextSpecial = Time.time + UnityEngine.Random.Range(2f, specialCooldown);
            ResetBody();
            Enter(kind == Kind.Lunger ? State.Hidden : State.Wander);
            Tint(BaseTint);
        }

        bool Has(Special s) => (specials & s) != 0;

        // the color it rests at: elites shimmer gold, enraged monsters pulse red
        Color BaseTint
        {
            get
            {
                if (enraged) return Color.Lerp(new Color(1.25f, 0.55f, 0.55f), new Color(1.5f, 0.7f, 0.6f), Mathf.PingPong(Time.time * 2f, 1f));
                if (elite) return Color.Lerp(new Color(1.12f, 1.02f, 0.78f), new Color(1.3f, 1.15f, 0.8f), Mathf.PingPong(Time.time * 0.8f, 1f));
                return Color.white;
            }
        }
        float Rage => enraged ? 1.35f : 1f;   // speed boost when enraged

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
            if (Puppet)
            {
                if (Alive && Time.time - LastHitTime > 0.12f) Tint(BaseTint);
                return;
            }
            stateTime += Time.deltaTime;
            var player = TargetPicker != null ? TargetPicker(transform.position) : PlayerStats.Local;
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
                    if (TrySpecial(dist)) break;
                    if (WantsToAttack(dist)) { BeginWindup(player); break; }
                    Approach(player.transform.position, dist);
                    Idle(1f);
                    break;
                case State.Windup:
                    if (player != null && kind != Kind.Charger) Face(player.transform.position, 6f);
                    Tell(Mathf.Clamp01(stateTime / WindupTime));
                    if (stateTime >= WindupTime) BeginAttack(player);
                    break;
                case State.SpecialWindup:
                    if (player != null) Face(player.transform.position, 5f);
                    SpecialTell(Mathf.Clamp01(stateTime / SpecialWindupTime));
                    if (stateTime >= SpecialWindupTime) { ResetBody(); Enter(State.SpecialAct); SpecialStart(player); }
                    break;
                case State.SpecialAct:
                    if (SpecialAct(player, stateTime)) { ResetBody(); Enter(State.Recover); attackHit = false; }
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
                        // frenzy: a quick second attack right after the first (once)
                        if (Has(Special.Frenzy) && !frenzied && canChase && dist < attackRange + 1.5f) { frenzied = true; BeginWindup(player); break; }
                        frenzied = false;
                        comboStep = 0;
                        Enter(canChase ? State.Chase : State.Wander);
                    }
                    break;
                case State.Dead:     // shrink away (flyers fall), then the spawner brings it back later
                    body.localScale = Vector3.Lerp(body.localScale, Vector3.zero, Time.deltaTime * 6f);
                    if (Flies) body.localPosition = Vector3.Lerp(body.localPosition, new Vector3(bodyBasePos.x, 0.2f, bodyBasePos.z), Time.deltaTime * 8f);
                    if (stateTime > 0.6f)
                    {
                        if (minion) Destroy(gameObject);
                        else if (Spawner != null) Spawner.Despawned(this);
                        else gameObject.SetActive(false);
                    }
                    break;
            }
            // little summoned copies fade away after a while
            if (minion && state != State.Dead && Time.time > minionUntil) { Health = 0f; Enter(State.Dead); }
            if (state != State.Windup && state != State.SpecialWindup && state != State.Dead && Time.time - LastHitTime > 0.12f) Tint(BaseTint);
        }

        // knight slashes chain quickly; the slam leaves it open for longer
        float RecoverTime => (kind == Kind.Knight ? (comboStep < 2 ? 0.25f : recoverSeconds) : frenzied ? 0.2f : recoverSeconds) / (enraged ? 1.6f : 1f);
        float WindupTime => (frenzied ? Mathf.Min(windupSeconds, 0.35f) : windupSeconds) * (enraged ? 0.75f : 1f);

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
            Step(target, moveSpeed * Rage);
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
                    Shot(mouth, aim, projectileSpeed, damage, kind == Kind.Spitter ? 0.32f : 0.2f);
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
            Wave(new Vector3(center.x, transform.position.y, center.z), radius, dust);
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

        // ---------- special moves ----------

        static readonly Special[] pickable = new Special[3];
        bool quakeSecond;

        // Maybe start a special instead of a normal attack (each has its own range; one every specialCooldown).
        // What it's doing right now ("Chase", "SpecialWindup"...), for debugging.
        public string StateName => state == State.SpecialWindup || state == State.SpecialAct ? $"{state} {move}" : state.ToString();

        // Starts a special right away (tests, and scripted fights later). False if it can't now.
        public bool ForceSpecial(Special m)
        {
            if (!Alive || minion || state == State.SpecialWindup || state == State.SpecialAct) return false;
            BeginSpecial(m);
            return true;
        }

        bool TrySpecial(float dist)
        {
            if (specials == Special.None || minion || Time.time < nextSpecial || stateTime < 0.5f) return false;
            if (Has(Special.Summon) && !summoned && FxSent == null && Health < maxHealth * 0.6f) { BeginSpecial(Special.Summon); return true; }   // (no summons online yet)
            int n = 0;
            if (Has(Special.Quake) && dist < 6f) pickable[n++] = Special.Quake;
            if (Has(Special.Volley) && dist > 3f && dist < 13f) pickable[n++] = Special.Volley;
            if (Has(Special.Blink) && dist > 2.5f && dist < 12f) pickable[n++] = Special.Blink;
            if (n == 0) return false;
            BeginSpecial(pickable[UnityEngine.Random.Range(0, n)]);
            return true;
        }

        void BeginSpecial(Special m)
        {
            move = m;
            nextSpecial = Time.time + specialCooldown * (enraged ? 0.7f : 1f) * UnityEngine.Random.Range(0.85f, 1.2f);
            attackHit = false;
            quakeSecond = false;
            if (m == Special.Summon) summoned = true;
            Enter(State.SpecialWindup);
            if (m == Special.Quake)   // the warning: a ring growing out to where the second wave will reach
                RingFx(transform.position + Vector3.up * 0.12f, new Color(1f, 0.45f, 0.35f), 5.5f, SpecialWindupTime);
        }

        float SpecialWindupTime =>
            (move == Special.Quake ? 1.1f : move == Special.Volley ? 0.8f : move == Special.Blink ? 0.6f : 1.0f) * (enraged ? 0.8f : 1f);

        // Every special has its own clear tell.
        void SpecialTell(float k)
        {
            bool flash = Mathf.PingPong(stateTime * 8f, 1f) > 0.5f;
            switch (move)
            {
                case Special.Quake:     // glows gold and rises up
                    Tint(flash ? new Color(1.6f, 1.4f, 0.7f) : tellColor);
                    body.localPosition = bodyBasePos + Vector3.up * 0.5f * Smooth(k);
                    body.localScale = bodyBaseScale * (1f + 0.15f * k);
                    break;
                case Special.Volley:    // swells and flashes
                    Tint(Color.Lerp(Color.white, tellColor, flash ? 1f : 0.3f));
                    body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f + 0.2f * k, 1f + 0.2f * k, 1f));
                    break;
                case Special.Blink:     // flickers and shrinks away
                    Tint(flash ? new Color(0.6f, 0.4f, 1.6f) : new Color(2f, 2f, 2f));
                    body.localScale = bodyBaseScale * (1f - 0.7f * k);
                    break;
                case Special.Summon:    // spins, glowing purple
                    Tint(flash ? new Color(1.3f, 0.7f, 1.6f) : Color.white);
                    body.localRotation = bodyBaseRot * Quaternion.Euler(0, stateTime * 720f, 0);
                    break;
            }
        }

        void SpecialStart(PlayerStats player)
        {
            Vector3 target = player != null ? player.transform.position : transform.position + transform.forward * 3f;
            lungeDir = Flat(target - transform.position).normalized;
            if (lungeDir.sqrMagnitude < 0.01f) lungeDir = transform.forward;
            Tint(BaseTint);
            switch (move)
            {
                case Special.Quake:
                    Slam(player, transform.position, 3f, new Color(1f, 0.75f, 0.45f, 0.85f));
                    break;
                case Special.Volley:
                {
                    Vector3 mouth = body.position + transform.forward * 0.6f + Vector3.up * 0.3f;
                    Vector3 aim = (target + Vector3.up * 0.9f - mouth).normalized;
                    foreach (var a in new[] { -24f, -12f, 0f, 12f, 24f })
                        Shot(mouth, Quaternion.AngleAxis(a, Vector3.up) * aim, Mathf.Max(projectileSpeed, 9f), damage * 0.6f, 0.24f);
                    break;
                }
                case Special.Blink:
                {
                    if (player == null) break;
                    Vector3 behind = player.transform.position - Flat(player.transform.forward).normalized * 1.8f;
                    if (ZoneMap.IsSafe(behind)) break;   // never pops into a safe zone
                    Wave(transform.position, 1.2f, new Color(0.6f, 0.4f, 1f, 0.8f));
                    transform.position = new Vector3(behind.x, transform.position.y, behind.z);
                    lungeDir = Flat(player.transform.position - transform.position).normalized;
                    if (lungeDir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(lungeDir);
                    Wave(transform.position, 1.2f, new Color(0.6f, 0.4f, 1f, 0.8f));
                    break;
                }
                case Special.Summon:
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 pos = transform.position + transform.right * (i == 0 ? -1.8f : 1.8f);
                        if (ZoneMap.IsSafe(pos)) continue;
                        var m = Instantiate(this, pos, transform.rotation, transform.parent);
                        m.MakeMinion(this);
                        Wave(pos, 1f, new Color(1f, 0.6f, 1f, 0.8f));
                    }
                    break;
            }
        }

        // Plays out the special; true when it's over.
        bool SpecialAct(PlayerStats player, float t)
        {
            switch (move)
            {
                case Special.Quake:
                    if (t >= 0.45f && !quakeSecond)
                    {
                        quakeSecond = true;
                        attackHit = false;
                        Slam(player, transform.position, 5.5f, new Color(1f, 0.6f, 0.4f, 0.7f), 0.8f);
                    }
                    return t > 0.75f;
                case Special.Volley:
                    body.localRotation = bodyBaseRot * Quaternion.Euler(Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI) * 12f, 0, 0);
                    return t > 0.35f;
                case Special.Blink:
                    if (t < 0.4f) Move(lungeDir * 6f * (1f - t / 0.4f));
                    if (!attackHit && player != null && player.Alive && t > 0.12f && t < 0.42f
                        && (player.transform.position + Vector3.up * 0.8f - (transform.position + Vector3.up * 0.6f + transform.forward * 0.8f)).magnitude < 1.6f)
                    {
                        attackHit = true;
                        player.TakeDamage(damage * 1.2f, gameObject);
                    }
                    return t > 0.5f;
                default:
                    return t > 0.3f;
            }
        }

        // Turns a fresh copy into a little summoned helper of `parent`.
        void MakeMinion(Monster parent)
        {
            minion = true;
            elite = false;
            specials = Special.None;
            Spawner = null;
            displayName = "little " + parent.displayName.Replace("elite ", "");
            maxHealth = Mathf.Max(8f, parent.maxHealth * 0.22f);
            Health = maxHealth;
            damage = parent.damage * 0.45f;
            xpReward = Mathf.Max(1, parent.xpReward / 6);
            coinReward = Vector2Int.zero;
            drops = new Drop[0];
            transform.localScale = parent.transform.localScale * 0.6f;
            barHeight = parent.barHeight * 0.6f;
            home = transform.position;
            minionUntil = Time.time + 40f;
            Tint(Color.white);
            Enter(State.Chase);
        }

        // ---------- getting hit ----------

        public bool TakeDamage(float amount, GameObject source)
        {
            if (!Alive) return false;
            if (HitRedirect != null) { FlashHit(); return HitRedirect(amount, source); }   // a client copy
            Health -= enraged ? amount * 0.9f : amount;
            LastHitTime = Time.time;
            if (Has(Special.Enrage) && !enraged && Health > 0f && Health < maxHealth * 0.4f)
            {
                enraged = true;
                Wave(transform.position, 1.6f, new Color(1f, 0.4f, 0.4f, 0.8f));
            }
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
                if (killer != null && RewardRedirect != null && killer != PlayerStats.Local)
                {
                    // online, someone else's kill: roll the rewards here, their game adds them (and counts the kill)
                    int coins = minion ? 0 : UnityEngine.Random.Range(coinReward.x, coinReward.y + 1);
                    var got = new System.Text.StringBuilder();
                    if (!minion)
                        foreach (var drop in drops)
                            if (UnityEngine.Random.value <= drop.chance)
                                got.Append(drop.item).Append(':').Append(UnityEngine.Random.Range(drop.min, drop.max + 1)).Append(';');
                    RewardRedirect(killer, xpReward, coins, got.ToString());
                    return true;
                }
                if (killer != null && minion) killer.AddXp(xpReward);   // little copies: a bit of XP, nothing else
                else if (killer != null)
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
            else if (amount >= 20f && (state == State.Windup || state == State.SpecialWindup) && !poise) { ResetBody(); Enter(State.Recover); }   // heavy hits interrupt the tell
            else if (state == State.Wander || state == State.Hidden) Enter(State.Chase);
            return true;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
