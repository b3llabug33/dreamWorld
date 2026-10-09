namespace DreamWorld.Gameplay
{
    // A food stall in town: buy a snack (eat it with R to refill food).
    public class SnackStand : Interactable
    {
        public int price = 10;

        public override string Prompt(PlayerStats player) => $"buy a snack ({price} coins)";

        public override void Interact(PlayerStats player)
        {
            if (player.SpendCoins(price))
            {
                player.progress.snacks++;
                GameHUD.Instance?.Toast($"bought a snack  ({player.progress.snacks} now, R to eat)");
            }
            else GameHUD.Instance?.Toast("not enough coins");
        }
    }
}
