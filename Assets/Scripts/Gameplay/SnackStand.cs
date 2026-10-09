namespace DreamWorld.Gameplay
{
    // A food stall in town: buy snacks and glow jelly, sell what you find (monster drops).
    public class SnackStand : Interactable
    {
        public string[] stock = { "snack", "glow_jelly" };

        public override string Prompt(PlayerStats player) => "browse the snack stand (buy + sell)";
        public override void Interact(PlayerStats player) => InventoryUI.Instance?.OpenShop(stock);
    }
}
