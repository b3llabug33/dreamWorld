using System;
using UnityEngine;

namespace DreamWorld.Gameplay
{
    // A simple dream monster (the first one is the shroomling). It wanders near where it spawned,
    // chases players it can see outside safe zones, and attacks with a telegraphed hop: it squashes
    // down and flashes first, so you can roll through it. It never steps into a safe zone.
    // Killing it gives XP and coins (more coins with luck).
    public class Monster : MonoBehaviour, IDamageable
    {
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
        [Tooltip("The visible mesh (squashes and hops); the root stays on the ground.")]
        public Transform body;

        public static event Action<Monster, PlayerStats> Killed;

        public float Health { get; private set; }
        public float HealthFraction => Health / maxHealth;
        public float LastHitTime { get; private set; } = -99f;
        public Transform Body => transform;
        public bool Alive => Health > 0f;
        public MonsterSpawner Spawner { get; set; }
        public bool WindingUp => state == State.Windup;

        enum State { Wander, Chase, Windup, Lunge, Recover, Dead }
        State state;
        float stateTime;
        Vector3 home, wanderTarget, lungeDir;
        bool lungeHit;
        Vector3 bodyBaseScale, bodyBasePos;
        Renderer[] renderers;
        MaterialPropertyBlock block;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            if (body == null) body = transform.GetChild(0);
            bodyBaseScale = body.localScale;
            bodyBasePos = body.localPosition;
            renderers = body.GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            Health = maxHealth;
            home = transform.position;
            wanderTarget = home;
            Enter(State.Wander);
            body.localScale = bodyBaseScale;
            body.localPosition = bodyBasePos;
            Tint(Color.white);
        }

        void Enter(State s)
        {
            state = s;
            stateTime = 0f;
        }

        void Update()
        {
            stateTime += Time.deltaTime;
            var player = PlayerStats.Local;
            bool canChase = player != null && player.Alive && !ZoneMap.IsSafe(player.transform.position)
                            && Vector3.Distance(home, transform.position) < leashRange;
            float dist = player != null ? Flat(player.transform.position - transform.position).magnitude : 999f;

            switch (state)
            {
                case State.Wander:
                    if (canChase && dist < sightRange) { Enter(State.Chase); break; }
                    if (Flat(wanderTarget - transform.position).magnitude < 0.3f || stateTime > 6f)
                    {
                        var r = UnityEngine.Random.insideUnitCircle * 3f;
                        wanderTarget = home + new Vector3(r.x, 0, r.y);
                        stateTime = 0f;
                    }
                    Step(wanderTarget, moveSpeed * 0.4f);
                    Bounce(0.6f);
                    break;
                case State.Chase:
                    if (!canChase || dist > sightRange * 1.5f) { Enter(State.Wander); wanderTarget = home; break; }
                    Face(player.transform.position);
                    if (dist < attackRange) { Enter(State.Windup); break; }
                    Step(player.transform.position, moveSpeed);
                    Bounce(1f);
                    break;
                case State.Windup:   // squash down and flash: the tell
                {
                    if (player != null) Face(player.transform.position);
                    float k = Mathf.Clamp01(stateTime / 0.6f);
                    body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f + 0.25f * k, 1f - 0.35f * k, 1f + 0.25f * k));
                    Tint(Color.Lerp(Color.white, new Color(1f, 0.55f, 0.75f), Mathf.PingPong(stateTime * 6f, 1f)));
                    if (stateTime >= 0.6f)
                    {
                        lungeDir = player != null ? Flat(player.transform.position - transform.position).normalized : transform.forward;
                        lungeHit = false;
                        Tint(Color.white);
                        Enter(State.Lunge);
                    }
                    break;
                }
                case State.Lunge:    // hop forward; hits once if it lands on you
                {
                    float k = Mathf.Clamp01(stateTime / 0.38f);
                    Vector3 next = transform.position + lungeDir * 6f * (1f - k) * Time.deltaTime;
                    if (!ZoneMap.IsSafe(next)) transform.position = next;
                    body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(0.9f, 1.15f, 0.9f));
                    body.localPosition = bodyBasePos + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.45f;
                    if (!lungeHit && player != null && player.Alive && k > 0.3f
                        && (player.transform.position + Vector3.up * 0.8f - (transform.position + Vector3.up * 0.4f)).magnitude < 1.15f)
                    {
                        lungeHit = true;
                        player.TakeDamage(damage, gameObject);
                    }
                    if (k >= 1f) { body.localPosition = bodyBasePos; Enter(State.Recover); }
                    break;
                }
                case State.Recover:
                    body.localScale = Vector3.Lerp(body.localScale, bodyBaseScale, Time.deltaTime * 8f);
                    if (stateTime > 1.4f) Enter(canChase ? State.Chase : State.Wander);
                    break;
                case State.Dead:     // shrink away, then the spawner brings it back later
                    body.localScale = Vector3.Lerp(body.localScale, Vector3.zero, Time.deltaTime * 6f);
                    if (stateTime > 0.6f)
                    {
                        if (Spawner != null) Spawner.Despawned(this);
                        else gameObject.SetActive(false);
                    }
                    break;
            }
            if (state != State.Windup && state != State.Dead && Time.time - LastHitTime > 0.12f) Tint(Color.white);
        }

        void Step(Vector3 toward, float speed)
        {
            Vector3 dir = Flat(toward - transform.position);
            if (dir.sqrMagnitude < 0.0001f) return;
            Vector3 next = transform.position + dir.normalized * speed * Time.deltaTime;
            if (ZoneMap.IsSafe(next)) return;   // stops at the edge of a safe zone
            transform.position = next;
            Face(toward);
        }

        void Face(Vector3 point)
        {
            Vector3 d = Flat(point - transform.position);
            if (d.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        void Bounce(float amount)
        {
            float b = Mathf.Abs(Mathf.Sin(Time.time * 9f)) * amount;
            body.localPosition = bodyBasePos + Vector3.up * b * 0.08f;
            body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f + b * 0.05f, 1f - b * 0.06f, 1f + b * 0.05f));
        }

        void Tint(Color c)
        {
            foreach (var r in renderers)
                for (int i = 0; i < r.sharedMaterials.Length; i++)
                {
                    r.GetPropertyBlock(block, i);
                    block.SetColor(ColorId, c);
                    r.SetPropertyBlock(block, i);
                }
        }

        public bool TakeDamage(float amount, GameObject source)
        {
            if (!Alive) return false;
            Health -= amount;
            LastHitTime = Time.time;
            Tint(new Color(2f, 2f, 2f));   // white hit flash
            if (source != null)            // a little knockback
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
            else if (amount >= 20f && state == State.Windup) Enter(State.Recover);   // heavy hits interrupt the hop
            else if (state == State.Wander) Enter(State.Chase);
            return true;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
