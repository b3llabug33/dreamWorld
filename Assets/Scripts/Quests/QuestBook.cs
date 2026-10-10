using System.Collections.Generic;
using DreamWorld.Gameplay;

namespace DreamWorld.Quests
{
    // All the quest content: definitions, who says what, and when steps advance.
    // Writing a new quest = add a QuestDef in Defs, then handle its NPCs in Conversation / OnKilled / OnReached.
    public static class QuestBook
    {
        public const string OrionIntro = "orion_intro";
        public const string Mullens = "mullens";

        public const string Orion = "Orion";
        public const string Snackula = "Count Snackula";
        public static readonly string[] MullenFamily =
            { "Carlily Mullen", "Esmay Mullen", "Edwerd Mullen", "Alyss Mullen", "Jaspurr Mullen", "Rosaleaf Mullen", "Emmitt Mullen", "Stella" };

        static readonly Dictionary<string, QuestDef> defs = new Dictionary<string, QuestDef>();

        static QuestBook()
        {
            Def(new QuestDef { id = OrionIntro, title = "a friendly beetle" }
                .AddStep("say hi to Orion in the town square", Orion));
            Def(new QuestDef { id = Mullens, title = "forks, sparkles and wolves" }
                .AddStep("ask Count Snackula about the vampire family in the woods", Snackula)
                .AddStep("find all 8 Mullens around their glass house in Mistpine Hollow (check the moody map)")
                .AddStep("join the Mullen family meeting (talk to Carlily)", "Carlily Mullen")
                .AddStep("spy on the Wolfpups at Quillet Riverbank (sneak to the 3 bushes)")
                .AddStep("defeat the Boulder Brute")
                .AddStep("tell the Mullens what happened (talk to Carlily)", "Carlily Mullen"));
        }

        static void Def(QuestDef d) => defs[d.id] = d;
        public static QuestDef Get(string id) => id != null && defs.TryGetValue(id, out var d) ? d : null;
        public static IEnumerable<QuestDef> All => defs.Values;

        public static void OnLoaded(QuestManager q)
        {
            if (!q.Started(OrionIntro)) q.StartQuest(OrionIntro);
        }

        // NPCs that have a quest to offer right now (they get a "!" too).
        public static IEnumerable<string> Offers(QuestManager q)
        {
            if (q.Finished(OrionIntro) && !q.Started(Mullens)) yield return Snackula;
        }

        static PlayerStats P => PlayerStats.Local;

        static void Give(string item, int count = 1)
        {
            if (P != null && Items.Get(item) != null)
            {
                int got = P.AddItem(item, count);
                if (got > 0) GameHUD.Instance?.Toast($"got {Items.Get(item).name}" + (got > 1 ? $" x{got}" : ""));
            }
        }

        static void Reward(int coins, int xp)
        {
            if (P == null) return;
            if (coins > 0) GameHUD.Instance?.Toast($"+{P.AddCoins(coins)} coins");
            if (xp > 0) { P.AddXp(xp); GameHUD.Instance?.Toast($"+{xp} xp"); }
        }

        // ---------------------------------------------------------------- conversations

        public static Dialogue Conversation(QuestManager q, string npc)
        {
            if (npc == Orion) return OrionTalk(q);
            if (npc == Snackula) return SnackulaTalk(q);
            int m = System.Array.IndexOf(MullenFamily, npc);
            if (m >= 0) return MullenTalk(q, npc);
            return null;
        }

        // Orion: the first-time greeting (her outline), then the same menu any time you come back.
        public static Dialogue OrionTalk(QuestManager q, bool scared = false)
        {
            const string O = "Orion";
            var d = new Dialogue();
            if (scared)
                d.Line("scared", O, "wait wait, don't be scared! i'm a beetle, but i'm a person too. i promise i don't bite. (much.)", "hello");
            bool first = !q.Has("orion_met");
            d.Ask("hello", O, first ? "hello traveler! what brings you here?" : "hi again! what do you want to know?",
                  ("fight", "fight"), ("barter", "barter"), ("sell", "sell"), ("collect", "collect"), ("i don't know", "all0"),
                  ("that's all", first ? "after0" : "bye"));
            d.Line("fight", O, "out past the paths, the woods get wild. shroomlings, moss golems, worse. roll with space, swing with a click, and lock on with Q.", "fight2");
            d.Line("fight2", O, "inside the town and on the paths you're safe. nothing can hurt you there.", "hello");
            d.Line("barter", O, "some folks here trade item for item instead of coins. bring them what they're looking for and they'll swap you something good.", "hello");
            d.Line("sell", O, "shops buy what you find out there. monster drops, extra materials, things you made. open your bag at a shop and pick something to sell it.", "hello");
            d.Line("collect", O, "chop trees for wood, break rocks for stone, mine ore out of the rocks in the woods, and gather thread from cotton and old webs.", "collect2");
            d.Line("collect2", O, "then make things at the workbench here in the square. decor for your house, tools, even weapons. i like the mushroom stools. obviously.", "hello");
            d.Line("all0", O, "that's okay! here's the short version.", "all1");
            d.Line("all1", O, "fighting: the wilds are dangerous, the town and paths are safe. roll, swing, lock on.", "all2");
            d.Line("all2", O, "bartering and selling: trade with people, or sell your stuff at the shops for coins.", "all3");
            d.Line("all3", O, "collecting: wood, rock, ore and thread. you can make cute things for your house with them.", "all4");
            d.Line("all4", O, "it's a lot, i know. you'll get the hang of it.", first ? "after0" : "hello");
            d.Line("after0", O, "oh! and if you want a place of your own, you can buy a plot of land at the land office. that's where you build your base.", "after1");
            d.Line("after1", O, "it's 100 coins. your home computer keeps your coins and stuff safe, and you'll respawn at your home base if you get knocked out.", "after2");
            d.Line("after2", O, "you'll meet a lot of characters around here. vampires, elves, a very loud mayor. talk to everyone. helping people is how you find quests.", "after3");
            d.Line("after3", O, "come find me anytime. i'll be around the square.", null, () =>
            {
                q.Set("orion_met");
                if (!q.Finished(OrionIntro)) { q.Complete(OrionIntro); Reward(15, 20); }
            });
            d.Line("bye", O, "okay! good luck out there.");
            d.start = scared ? "scared" : "hello";
            return d;
        }

        static Dialogue SnackulaTalk(QuestManager q)
        {
            const string S = "Count Snackula";
            if (!q.Started(Mullens) && q.Finished(OrionIntro))
            {
                var d = new Dialogue();
                d.Line("a", S, "psst. you. you look like someone who appreciates drama.", "b");
                d.Line("b", S, "there's a family of vampires out in the fog woods. the Mullens. they don't drink blood. they drink juice boxes. disgraceful.", "c");
                d.Line("c", S, "also they sparkle. in the SUN. can you imagine.", "d");
                d.Ask("d", S, "they're very hard to find. want my old map?", ("yes please", "yes"), ("why do you have a map", "why"));
                d.Line("why", S, "i drew it during a moody phase. we don't talk about the moody phase.", "yes");
                d.Line("yes", S, "here. the house is west of town, deep in the mist. go meet them. tell me EVERYTHING.", null, () =>
                {
                    q.StartQuest(Mullens);
                    q.Advance(Mullens);
                    Give("moody_map");
                });
                return d;
            }
            if (q.Started(Mullens) && !q.Finished(Mullens))
                return Dialogue.Lines(S, "well? have you found the sparkly family yet? i need gossip. i'm starving for it.");
            return null;   // otherwise his normal town lines
        }

        static readonly Dictionary<string, string> MullenFirstLines = new Dictionary<string, string>
        {
            { "Carlily Mullen", "oh, hello. sorry, i'm in the middle of a procedure. this mushroom has a tiny scrape." },
            { "Esmay Mullen", "welcome! do you think this vase looks better here... or here? or here." },
            { "Edwerd Mullen", "...hello. you smell like... spore caps." },
            { "Alyss Mullen", "i saw you coming! you were wearing red shoes! ...oh. you're not. huh." },
            { "Jaspurr Mullen", "sorry. i'm just... nervous. new people. it's not you. it's a little you." },
            { "Rosaleaf Mullen", "oh. another one." },
            { "Emmitt Mullen", "arm wrestle? no? later then. i'm holding you to it." },
            { "Stella", "hi! sorry, i keep tripping on this doormat. it's a whole thing. i'm Stella!" },
        };

        static Dialogue MullenTalk(QuestManager q, string npc)
        {
            int step = q.StepOf(Mullens);
            string flag = "met_" + npc;
            if (step == 1 && !q.Has(flag))
            {
                return new Dialogue().Line("a", npc, MullenFirstLines[npc], null, () =>
                {
                    q.Set(flag);
                    q.Add("mullens_found");
                    int found = q.Count("mullens_found");
                    GameHUD.Instance?.Toast($"found {found}/8 Mullens");
                    if (found >= MullenFamily.Length) q.Advance(Mullens);
                });
            }
            if (step == 2 && npc == "Carlily Mullen")
            {
                var d = new Dialogue();
                d.Line("a", npc, "thank you all for coming. and you, our new friend.", "b");
                d.Line("b", npc, "we have a situation. the Wolfpups down by the river have been acting strange. howling at noon. eating all the berries.", "c");
                d.Line("c", npc, "we'd go ourselves, but... we're sparkly. they'd see us coming.", "d");
                d.Line("d", "Edwerd Mullen", "go and listen. don't let them smell you. ...they will smell you.", "e");
                d.Line("e", "Alyss Mullen", "it'll go perfectly! (it won't.)", null, () =>
                {
                    q.Advance(Mullens);
                    Give("fog_cloak");
                });
                return d;
            }
            if (step == 5 && npc == "Carlily Mullen")
            {
                var d = new Dialogue();
                d.Line("a", npc, "you're back! and you're... covered in moss. what happened?", "b");
                d.Line("b", "Edwerd Mullen", "you fought it. alone. that's... i should have been there.", "c");
                d.Line("c", "Rosaleaf Mullen", "fine. you're not boring.", "d");
                d.Line("d", "Stella", "group hug! oh no. i tripped. hug anyway!", "e");
                d.Line("e", npc, "please, take this. and consider our home yours. you're family now.", null, () =>
                {
                    q.Advance(Mullens);
                    Give("glimmer_blade");
                    Reward(150, 200);
                    q.Set("mullen_friend");
                });
                return d;
            }
            if (q.Has("mullen_friend")) return Dialogue.Lines(npc, "it's always nice to see you.");
            return null;
        }

        // ---------------------------------------------------------------- world events

        public static void OnKilled(QuestManager q, string monsterName)
        {
            if (q.OnStep(Mullens, 4) && monsterName == "boulder brute") q.Advance(Mullens);
        }

        public static void OnReached(QuestManager q, string place)
        {
            if (q.OnStep(Mullens, 3) && place.StartsWith("wolf_bush_") && !q.Has("heard_" + place))
            {
                q.Set("heard_" + place);
                q.Add("wolf_bushes");
                if (q.Count("wolf_bushes") >= 3) q.Advance(Mullens);
            }
        }
    }
}
