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

        public override void Interact(PlayerStats player)
        {
            if (lines.Length == 0) return;
            if (InventoryUI.IsOpen) InventoryUI.Instance.Toggle();   // close the bag so they don't overlap
            GameHUD.Instance?.Say(npcName, lines[next % lines.Length]);
            next++;
            var walker = GetComponent<Wanderer>();
            if (walker != null) walker.PauseFor(4f, player.transform.position);
        }
    }
}
