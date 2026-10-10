using DreamWorld.Gameplay;

namespace DreamWorld.Gathering
{
    // A workbench: press E to craft (CraftingUI).
    public class CraftingBench : Interactable
    {
        public override string Prompt(PlayerStats player) => "craft at the workbench";
        public override void Interact(PlayerStats player) => CraftingUI.Open();
    }
}
