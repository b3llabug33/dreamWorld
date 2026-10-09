using System;
using UnityEngine;

namespace DreamWorld.Gameplay
{
    // The five things a level-up point can go into.
    public enum Attribute { Health, Stamina, Magic, Strength, Luck }

    // Everything about a character's progress that gets saved (separate from their look).
    [Serializable]
    public class PlayerProgress
    {
        public int level;                  // everyone starts at 0
        public int xp;                     // toward the next level
        public int unspent;                // level-up points not spent yet
        public int[] points = new int[5];  // spent points per Attribute
        public int carried;                // coins on you: lost when you die
        public int banked;                 // coins in your home computer: safe
        public float food = 100f;
        public int snacks = 3;             // old saves kept snacks as a number; moved into the bag on load
        public int deaths;
        public ItemStack[] bag;            // 28 slots you carry
        public ItemStack[] storage;        // 56 slots in your home computer
        public string[] hotbar = new string[4];   // item ids on the 1-4 quick slots
    }

    // A player's bars, level and money. Health/stamina/magic grow with the points spent on them;
    // food slowly drains (eat to keep it up). Runs on the player you control.
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        public static PlayerStats Local { get; private set; }

        public PlayerProgress progress = new PlayerProgress();
        public float health, stamina, magic;

        [Header("Stamina")]
        public float staminaRegen = 32f;        // per second
        public float staminaRegenDelay = 0.9f;  // after spending some
        [Header("Food")]
        public float foodDrainPerMinute = 4f;   // full to empty in about 25 minutes
        public float starvingDamagePerSecond = 0.6f;
        public float wellFedRegen = 1.2f;       // health per second when fed and out of combat

        public event Action Changed;
        public event Action<int> LeveledUp;     // new level
        public event Action<int> Died;          // coins lost

        public bool Dead { get; private set; }
        public bool Invulnerable { get; set; }  // roll i-frames
        public float GuardReduction { get; set; }   // 0 = not guarding, 0.75 = block 75%
        public float LastHitTime { get; private set; } = -99f;
        float lastStaminaUse = -99f;
        float protectedUntil = -99f;
        // A few seconds of safety after waking up at home, so nothing can camp your bed.
        public bool Protected => Time.time < protectedUntil;

        // ---------- derived numbers (all formulas in one place) ----------
        int P(Attribute a) => progress.points[(int)a];
        public float MaxHealth => 100f + 12f * P(Attribute.Health);
        public float MaxStamina => 80f + 6f * P(Attribute.Stamina);
        public float MaxMagic => 10f + 8f * P(Attribute.Magic);       // starts small
        public float MaxFood => 100f;
        public float DamageMultiplier => 1f + 0.06f * P(Attribute.Strength);
        public float LuckMultiplier => 1f + 0.04f * P(Attribute.Luck);  // more coins from everything
        public bool Starving => progress.food <= 0f;
        public static int XpForLevel(int level) => Mathf.RoundToInt(80f * Mathf.Pow(level + 1, 1.4f));
        public int XpToNext => XpForLevel(progress.level);

        void OnEnable() => Local = this;
        void OnDisable() { if (Local == this) Local = null; }

        void Awake()
        {
            Prepare();
            FillBars();
        }

        // Makes sure every saved array is there and the right size, and moves old-style snacks into the bag.
        void Prepare()
        {
            if (progress.points == null || progress.points.Length != 5) progress.points = new int[5];
            progress.bag = Inventory.Fit(progress.bag, Inventory.BagSize);
            progress.storage = Inventory.Fit(progress.storage, Inventory.StorageSize);
            if (progress.hotbar == null || progress.hotbar.Length != 4)
            {
                var old = progress.hotbar;
                progress.hotbar = new string[4];
                for (int i = 0; old != null && i < old.Length && i < 4; i++) progress.hotbar[i] = old[i];
            }
            if (progress.snacks > 0)
            {
                Inventory.Add(progress.bag, "snack", progress.snacks);
                progress.snacks = 0;
                if (string.IsNullOrEmpty(progress.hotbar[0])) progress.hotbar[0] = "snack";
            }
        }

        public void FillBars()
        {
            health = MaxHealth;
            stamina = MaxStamina;
            magic = MaxMagic;
        }

        // Loads saved progress (bars start full).
        public void Load(PlayerProgress saved)
        {
            progress = saved ?? new PlayerProgress();
            Prepare();
            FillBars();
            Changed?.Invoke();
        }

        void Update()
        {
            if (Dead) return;
            float dt = Time.deltaTime;
            if (Time.time - lastStaminaUse > staminaRegenDelay)
                stamina = Mathf.Min(MaxStamina, stamina + staminaRegen * (Starving ? 0.5f : 1f) * dt);
            magic = Mathf.Min(MaxMagic, magic + 0.4f * dt);

            progress.food = Mathf.Max(0f, progress.food - foodDrainPerMinute / 60f * dt);
            if (Starving)
                health = Mathf.Max(1f, health - starvingDamagePerSecond * dt);   // hunger hurts but never kills
            else if (progress.food > 50f && Time.time - LastHitTime > 6f)
                health = Mathf.Min(MaxHealth, health + wellFedRegen * dt);
            Changed?.Invoke();
        }

        // ---------- actions ----------

        // Elden Ring rule: any action works while you have some stamina left, then you're out.
        public bool TrySpendStamina(float cost)
        {
            if (Dead || stamina <= 0f) return false;
            stamina = Mathf.Max(0f, stamina - cost);
            lastStaminaUse = Time.time;
            return true;
        }

        public bool TrySpendMagic(float cost)
        {
            if (Dead || magic < cost) return false;
            magic -= cost;
            return true;
        }

        // IDamageable: monsters (and later other players outside safe zones) hit you through this.
        public bool TakeDamage(float amount, GameObject source)
        {
            if (Dead || Invulnerable || Protected || amount <= 0f) return false;
            if (ZoneMap.IsSafe(transform.position)) return false;   // nothing can hurt you in a safe zone
            if (GuardReduction > 0f)
            {
                // guarding: block most of it, pay the rest in stamina; guard breaks when stamina runs out
                float blocked = amount * GuardReduction;
                if (stamina > blocked * 0.6f)
                {
                    stamina -= blocked * 0.6f;
                    lastStaminaUse = Time.time;
                    amount -= blocked;
                }
            }
            health -= amount;
            LastHitTime = Time.time;
            if (health <= 0f) Die();
            Changed?.Invoke();
            return true;
        }

        public Transform Body => transform;
        public bool Alive => !Dead;

        public void Eat(float amount)
        {
            progress.food = Mathf.Min(MaxFood, progress.food + amount);
            Changed?.Invoke();
        }

        // ---------- items ----------

        public ItemStack[] Bag => progress.bag;
        public ItemStack[] Storage => progress.storage;

        // Puts items in the bag; returns how many fit.
        public int AddItem(string id, int count = 1)
        {
            int added = Inventory.Add(progress.bag, id, count);
            Changed?.Invoke();
            return added;
        }

        // Uses one of an item from the bag (food fills the food bar, potions heal). False if it can't be used now.
        public bool UseItem(string id)
        {
            var def = Items.Get(id);
            if (def == null || !def.Usable || Dead || Inventory.Count(progress.bag, id) == 0) return false;
            bool helps = (def.food > 0 && progress.food < MaxFood - 1f) || (def.heal > 0 && health < MaxHealth - 0.5f);
            if (!helps) return false;
            Inventory.Remove(progress.bag, id, 1);
            if (def.food > 0) Eat(def.food);
            if (def.heal > 0) health = Mathf.Min(MaxHealth, health + def.heal);
            Changed?.Invoke();
            return true;
        }

        public void AddXp(int amount)
        {
            progress.xp += amount;
            while (progress.xp >= XpToNext)
            {
                progress.xp -= XpToNext;
                progress.level++;
                progress.unspent++;
                LeveledUp?.Invoke(progress.level);
            }
            Changed?.Invoke();
        }

        // Coins you earn are boosted by luck; returns what you actually got.
        public int AddCoins(int baseAmount)
        {
            int got = Mathf.RoundToInt(baseAmount * LuckMultiplier);
            progress.carried += got;
            Changed?.Invoke();
            return got;
        }

        public bool SpendCoins(int amount)
        {
            if (progress.carried < amount) return false;
            progress.carried -= amount;
            Changed?.Invoke();
            return true;
        }

        public void Deposit()
        {
            progress.banked += progress.carried;
            progress.carried = 0;
            Changed?.Invoke();
        }

        public void Withdraw(int amount)
        {
            amount = Mathf.Clamp(amount, 0, progress.banked);
            progress.banked -= amount;
            progress.carried += amount;
            Changed?.Invoke();
        }

        // Spends one level-up point. Bars that grow keep the same fill fraction.
        public bool SpendPoint(Attribute attribute)
        {
            if (progress.unspent <= 0) return false;
            float h = health / MaxHealth, s = stamina / MaxStamina, m = magic / MaxMagic;
            progress.points[(int)attribute]++;
            progress.unspent--;
            health = h * MaxHealth;
            stamina = s * MaxStamina;
            magic = m * MaxMagic;
            Changed?.Invoke();
            return true;
        }

        void Die()
        {
            Dead = true;
            health = 0f;
            int lost = progress.carried;   // everything you didn't deposit at home
            progress.carried = 0;
            progress.deaths++;
            Died?.Invoke(lost);
        }

        public void Respawn()
        {
            Dead = false;
            FillBars();
            progress.food = Mathf.Max(progress.food, 40f);
            LastHitTime = -99f;
            protectedUntil = Time.time + 6f;
            Changed?.Invoke();
        }
    }
}
