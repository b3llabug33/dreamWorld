using System;
using System.Collections.Generic;
using System.Linq;
using DreamWorld.Gameplay;
using UnityEngine;

namespace DreamWorld.Quests
{
    // Bounties from the board in town: "defeat 5 shroomlings" or "bring 4 beetle thorns", for coins + XP.
    // The board offers three at a time (a fresh set after each turn-in, and every day); you can carry two.
    // Active bounties are saved with the quests (QuestState.bounties) as "kind|target|need|have|coins|xp".
    public class Bounty
    {
        public string kind, target;   // kind: "kill" (target = monster name) or "bring" (target = item id)
        public int need, have, coins, xp;

        public string Label
        {
            get
            {
                if (kind == "kill") return $"defeat {need} {Plural(target, need)}";
                var item = Items.Get(target);
                return $"bring {need} {(item != null ? Plural(item.name, need) : target)}";
            }
        }

        public int Current(PlayerStats p) => kind == "kill" ? have : (p != null ? Inventory.Count(p.Bag, target) : 0);
        public bool Done(PlayerStats p) => Current(p) >= need;
        public string Progress() => $"{Label} ({Mathf.Min(Current(PlayerStats.Local), need)}/{need})";

        public string Save() => $"{kind}|{target}|{need}|{have}|{coins}|{xp}";

        public static Bounty Load(string s)
        {
            var p = s.Split('|');
            if (p.Length < 6) return null;
            return new Bounty { kind = p[0], target = p[1], need = int.Parse(p[2]), have = int.Parse(p[3]), coins = int.Parse(p[4]), xp = int.Parse(p[5]) };
        }

        static string Plural(string name, int n)
        {
            if (n == 1) return name;
            if (name.EndsWith("s") || name.EndsWith("dust") || name.EndsWith("slime") || name.EndsWith("moss") || name.EndsWith("stuffing")) return name;
            if (name.EndsWith("f")) return name.Substring(0, name.Length - 1) + "ves";   // leaf -> leaves
            return name + "s";
        }
    }

    public static class Bounties
    {
        public const int MaxActive = 2;

        // (target, count range, coins per one, xp per one, the level you need)
        static readonly (string target, int min, int max, int coins, int xp, int level)[] Kills =
        {
            ("shroomling", 3, 6, 6, 12, 0), ("glitch moth", 3, 5, 7, 14, 0), ("night bat", 3, 5, 7, 14, 0),
            ("rot slug", 3, 5, 8, 15, 1), ("cursed plush", 3, 5, 8, 15, 1), ("thorn beetle", 2, 4, 11, 20, 2),
            ("static wisp", 2, 4, 11, 20, 2), ("ivy serpent", 2, 4, 11, 20, 2), ("fog bat", 3, 5, 9, 16, 2),
            ("fern serpent", 2, 4, 12, 22, 3), ("moss golem", 1, 2, 30, 55, 4), ("mist golem", 1, 2, 32, 60, 4),
            ("mushroom knight", 1, 1, 90, 160, 6),
        };
        static readonly (string item, int min, int max, int coins, int xp, int level)[] Brings =
        {
            ("spore_cap", 4, 8, 4, 8, 0), ("moth_dust", 3, 5, 6, 10, 0), ("bat_wing", 2, 4, 7, 12, 0),
            ("rot_slime", 3, 6, 5, 9, 1), ("plush_stuffing", 2, 4, 7, 12, 1), ("thorn", 3, 5, 7, 12, 2),
            ("ivy_leaf", 3, 5, 6, 11, 2), ("static_spark", 2, 3, 10, 16, 2), ("golem_moss", 2, 3, 12, 20, 4),
        };

        public static IEnumerable<Bounty> Active(QuestManager q) =>
            q == null ? Enumerable.Empty<Bounty>() : q.state.bounties.Select(Bounty.Load).Where(b => b != null);

        static void Store(QuestManager q, IEnumerable<Bounty> list)
        {
            q.state.bounties = list.Select(b => b.Save()).ToList();
            q.Touch();
        }

        // The three on the board right now: picked from the day and how many you've turned in, so it's the same
        // board until you finish one (or tomorrow comes).
        public static List<Bounty> Offers(QuestManager q, PlayerStats p)
        {
            int level = p != null ? p.progress.level : 0;
            var rng = new System.Random(DateTime.Now.DayOfYear * 7919 + q.Count("bounties_done") * 104729);
            var pool = new List<Bounty>();
            foreach (var k in Kills.Where(k => k.level <= level))
            {
                int n = rng.Next(k.min, k.max + 1);
                pool.Add(new Bounty { kind = "kill", target = k.target, need = n, coins = k.coins * n, xp = k.xp * n });
            }
            foreach (var b in Brings.Where(b => b.level <= level))
            {
                int n = rng.Next(b.min, b.max + 1);
                pool.Add(new Bounty { kind = "bring", target = b.item, need = n, coins = b.coins * n, xp = b.xp * n });
            }
            var active = Active(q).Select(b => b.kind + b.target).ToHashSet();
            return pool.Where(b => !active.Contains(b.kind + b.target)).OrderBy(_ => rng.Next()).Take(3).ToList();
        }

        public static void Take(QuestManager q, Bounty b)
        {
            var list = Active(q).ToList();
            if (list.Count >= MaxActive) return;
            list.Add(b);
            Store(q, list);
            q.Announce("new bounty: " + b.Label);
        }

        public static void OnKilled(QuestManager q, string monster)
        {
            var list = Active(q).ToList();
            bool changed = false;
            foreach (var b in list)
                if (b.kind == "kill" && b.target == monster && b.have < b.need)
                {
                    b.have++;
                    changed = true;
                    GameHUD.Instance?.Toast(b.have >= b.need ? $"bounty done! turn it in at the board" : $"bounty: {b.have}/{b.need} {b.target}");
                }
            if (changed) Store(q, list);
        }

        // Hands in a finished bounty: items leave the bag, coins + XP arrive.
        public static bool TurnIn(QuestManager q, int index, PlayerStats p)
        {
            var list = Active(q).ToList();
            if (p == null || index < 0 || index >= list.Count || !list[index].Done(p)) return false;
            var b = list[index];
            if (b.kind == "bring")
            {
                Inventory.Remove(p.Bag, b.target, b.need);
                p.AddItem(b.target, 0);   // (refreshes the bag UI)
            }
            list.RemoveAt(index);
            Store(q, list);
            q.Add("bounties_done");
            GameHUD.Instance?.Toast($"+{p.AddCoins(b.coins)} coins");
            p.AddXp(b.xp);
            GameHUD.Instance?.Toast($"+{b.xp} xp");
            q.Announce("bounty complete!");
            return true;
        }

        // What the board says when you press E on it: turn-ins first, then the offers.
        public static Dialogue BoardTalk(QuestManager q, PlayerStats p)
        {
            const string B = "bounty board";
            var d = new Dialogue();
            var active = Active(q).ToList();
            var choices = new List<(string, string)>();
            for (int i = 0; i < active.Count; i++)
            {
                int k = i;
                var b = active[i];
                if (b.Done(p))
                {
                    choices.Add(($"turn in: {b.Label} (+{b.coins} coins)", "in" + k));
                    d.Line("in" + k, B, $"bounty complete! here's {b.coins} coins and {b.xp} xp.", null, () => TurnIn(q, k, p));
                }
            }
            var offers = active.Count < MaxActive ? Offers(q, p) : new List<Bounty>();
            for (int i = 0; i < offers.Count; i++)
            {
                var b = offers[i];
                choices.Add(($"take: {b.Label} ({b.coins} coins, {b.xp} xp)", "take" + i));
                d.Line("take" + i, B, $"bounty taken: {b.Label}. check your progress with J.", null, () => Take(q, b));
            }
            choices.Add(("leave", "bye"));
            string intro = active.Count >= MaxActive && !active.Any(b => b.Done(p))
                ? "you're already carrying two bounties. finish one, then come back for more."
                : "== BOUNTIES ==  pick one. you can carry two at a time.";
            d.Ask("start", B, intro, choices.ToArray());
            d.Line("bye", B, "(the board creaks in the wind)");
            d.start = "start";
            return d;
        }
    }
}
