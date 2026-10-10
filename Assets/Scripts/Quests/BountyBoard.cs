using DreamWorld.Gameplay;

namespace DreamWorld.Quests
{
    // The board in town: press E to see bounties.
    public class BountyBoard : Interactable
    {
        public override string Prompt(PlayerStats player) => "read the bounty board";

        public override void Interact(PlayerStats player)
        {
            var q = QuestManager.Instance;
            if (q == null || DialogueUI.Instance == null) return;
            DialogueUI.Instance.Show(Bounties.BoardTalk(q, player));
        }
    }
}
