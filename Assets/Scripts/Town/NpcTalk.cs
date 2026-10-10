using DreamWorld.Gameplay;

namespace DreamWorld.Town
{
    // Walk up and press E: the townsperson says their next line (shown as a toast).
    public class NpcTalk : Interactable
    {
        public string npcName = "someone";
        public string[] lines = { "hello!" };
        int next;

        public override string Prompt(PlayerStats player) => $"talk to {npcName}";

        void Start() => Quests.QuestHUD.Npcs[npcName] = transform;   // quest "!" markers float over us

        public override void Interact(PlayerStats player)
        {
            // a quest conversation comes first (Quests/QuestBook.cs), otherwise our everyday lines
            var quest = Quests.QuestManager.Instance != null ? Quests.QuestManager.Instance.Conversation(npcName) : null;
            if (quest != null && Quests.DialogueUI.Instance != null)
            {
                Quests.DialogueUI.Instance.Show(quest);
                var w = GetComponent<Wanderer>();
                if (w != null) w.PauseFor(8f, player.transform.position);
                return;
            }
            if (lines.Length == 0) return;
            if (InventoryUI.IsOpen) InventoryUI.Instance.Toggle();   // close the bag so they don't overlap
            GameHUD.Instance?.Say(npcName, lines[next % lines.Length]);
            next++;
            var walker = GetComponent<Wanderer>();
            if (walker != null) walker.PauseFor(4f, player.transform.position);
        }
    }
}
