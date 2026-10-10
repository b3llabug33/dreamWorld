namespace DreamWorld.Gameplay
{
    // A shop counter: the food stall in town (snacks and glow jelly), and the weapon shop. Buy what's
    // in stock, sell what you find (monster drops).
    public class SnackStand : Interactable
    {
        public string title = "snack stand";
        public string[] stock = { "snack", "glow_jelly" };

        public override string Prompt(PlayerStats player) => $"browse the {title} (buy + sell)";
        public override void Interact(PlayerStats player) => InventoryUI.Instance?.OpenShop(stock, title);
    }
}
