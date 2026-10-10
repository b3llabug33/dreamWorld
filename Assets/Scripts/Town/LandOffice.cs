using DreamWorld.Gameplay;
using UnityEngine;

namespace DreamWorld.Town
{
    // The land clerk: explains bases and sells you a plot for 100 coins (carried, not banked).
    public class LandOffice : Interactable
    {
        public const int PlotPrice = 100;
        public string clerkName = "Pip the land clerk";
        int talks;

        public override string Prompt(PlayerStats player) =>
            player.progress.plot >= 0 ? $"talk to {clerkName}" : $"buy a plot of land ({PlotPrice} coins)";

        public override void Interact(PlayerStats player)
        {
            var hud = GameHUD.Instance;
            if (player.progress.plot >= 0)
            {
                hud?.Toast($"{clerkName}: your plot is #{player.progress.plot + 1}, east of town. decorate it however you like!");
                return;
            }
            if (talks++ == 0)
            {
                hud?.Toast($"{clerkName}: every dreamer needs a base! a plot is your own land with a home computer");
                hud?.Toast("bank coins + store items there, and you wake up there instead of town. careful, bases aren't safe zones");
            }
            if (player.progress.carried < PlotPrice)
            {
                hud?.Toast($"{clerkName}: a plot costs {PlotPrice} coins and you have {player.progress.carried}. come back soon!");
                return;
            }
            int free = Plot.All.Count > 0 ? Plot.All[0].index : 0;   // solo: the first plot (online: the first untaken one)
            player.SpendCoins(PlotPrice);
            player.progress.plot = free;
            hud?.Toast($"you bought plot #{free + 1}! follow the east path to your new home");
            var life = player.GetComponent<PlayerLife>();
            if (life != null) life.Save();
        }
    }
}
