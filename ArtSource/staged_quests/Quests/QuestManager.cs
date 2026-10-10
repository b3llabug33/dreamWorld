using System;
using System.Collections.Generic;
using UnityEngine;

namespace DreamWorld.Quests
{
    // Saved quest progress: which step each quest is on, plus simple flags and counters
    // (lists, not dictionaries, so JsonUtility can save it).
    [Serializable]
    public class QuestState
    {
        public List<string> questIds = new List<string>();
        public List<int> questSteps = new List<int>();      // -1 = finished
        public List<string> flags = new List<string>();
        public List<string> counterIds = new List<string>();
        public List<int> counterValues = new List<int>();
    }

    // One quest: a title and an ordered list of steps. Each step has the text shown in the quest log
    // and the NPC (if any) to point the marker at.
    public class QuestDef
    {
        public string id, title;
        public List<Step> steps = new List<Step>();

        public class Step
        {
            public string log;           // "talk to Count Snackula in the town square"
            public string markerNpc;     // npc name that gets the "!" marker (null = none)
        }

        public QuestDef AddStep(string log, string markerNpc = null)
        {
            steps.Add(new Step { log = log, markerNpc = markerNpc });
            return this;
        }
    }

    // Runs every quest: who you've talked to, what you've killed or found, what step you're on.
    // NPCs ask it for a conversation (Conversation(npc)); the world tells it about kills and places.
    // Quest content lives in QuestBook so this file stays the "engine".
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        public QuestState state = new QuestState();
        public event Action Changed;
        public event Action<string> Notice;      // "new quest: ...", "quest updated" banners

        bool loaded;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (loaded) Save();
        }

        async void Start()
        {
            var json = await QuestStore.Load();
            if (this == null) return;
            if (!string.IsNullOrEmpty(json))
            {
                try { state = JsonUtility.FromJson<QuestState>(json) ?? new QuestState(); }
                catch (ArgumentException) { state = new QuestState(); }
            }
            loaded = true;
            QuestBook.OnLoaded(this);
            Changed?.Invoke();
        }

        public void Save()
        {
            if (!loaded) return;
            _ = QuestStore.Save(JsonUtility.ToJson(state));
        }

        // ---------- quests ----------

        int Index(string questId) => state.questIds.IndexOf(questId);
        public bool Started(string questId) => Index(questId) >= 0;
        public bool Finished(string questId) => Index(questId) >= 0 && state.questSteps[Index(questId)] < 0;
        public int StepOf(string questId) => Index(questId) >= 0 ? state.questSteps[Index(questId)] : -2;
        public bool OnStep(string questId, int step) => StepOf(questId) == step;

        public void StartQuest(string questId)
        {
            if (Started(questId)) return;
            state.questIds.Add(questId);
            state.questSteps.Add(0);
            var def = QuestBook.Get(questId);
            Notice?.Invoke($"new quest: {def?.title ?? questId}");
            Commit();
        }

        public void Advance(string questId)
        {
            int i = Index(questId);
            if (i < 0 || state.questSteps[i] < 0) return;
            var def = QuestBook.Get(questId);
            state.questSteps[i]++;
            if (def != null && state.questSteps[i] >= def.steps.Count)
            {
                state.questSteps[i] = -1;
                Notice?.Invoke($"quest complete: {def.title}");
            }
            else Notice?.Invoke("quest updated");
            Commit();
        }

        public void Complete(string questId)
        {
            int i = Index(questId);
            if (i < 0) { StartQuest(questId); i = Index(questId); }
            state.questSteps[i] = -1;
            Notice?.Invoke($"quest complete: {QuestBook.Get(questId)?.title ?? questId}");
            Commit();
        }

        // The log line for a quest's current step ("" when finished).
        public string CurrentLog(string questId)
        {
            var def = QuestBook.Get(questId);
            int s = StepOf(questId);
            return def != null && s >= 0 && s < def.steps.Count ? def.steps[s].log : "";
        }

        // ---------- flags and counters ----------

        public bool Has(string flag) => state.flags.Contains(flag);

        public void Set(string flag)
        {
            if (Has(flag)) return;
            state.flags.Add(flag);
            Commit();
        }

        public int Count(string id)
        {
            int i = state.counterIds.IndexOf(id);
            return i >= 0 ? state.counterValues[i] : 0;
        }

        public void Add(string id, int amount = 1)
        {
            int i = state.counterIds.IndexOf(id);
            if (i < 0) { state.counterIds.Add(id); state.counterValues.Add(amount); }
            else state.counterValues[i] += amount;
            Commit();
        }

        void Commit()
        {
            Changed?.Invoke();
            Save();
        }

        // ---------- hooks the world calls ----------

        // An NPC was talked to: returns a quest conversation for them, or null to use their normal lines.
        public Dialogue Conversation(string npc) => QuestBook.Conversation(this, npc);

        public void Killed(string monsterName) => QuestBook.OnKilled(this, monsterName);

        public void Reached(string place) => QuestBook.OnReached(this, place);

        // Which NPC names should show a "!" right now.
        public IEnumerable<string> MarkedNpcs()
        {
            for (int i = 0; i < state.questIds.Count; i++)
            {
                int step = state.questSteps[i];
                var def = QuestBook.Get(state.questIds[i]);
                if (def == null || step < 0 || step >= def.steps.Count) continue;
                if (!string.IsNullOrEmpty(def.steps[step].markerNpc)) yield return def.steps[step].markerNpc;
            }
            foreach (var npc in QuestBook.Offers(this)) yield return npc;
        }
    }
}
