using DreamWorld.Gameplay;
using UnityEngine;

namespace DreamWorld.Town
{
    // Your home on the plot you bought: the cottage, the home computer (bank + storage + where you wake up)
    // and the home zone. Hidden until you own a plot, then it moves onto it. (Online, every player will
    // get their own copy; for now there's one.)
    public class HomeBase : MonoBehaviour
    {
        public static HomeBase Instance { get; private set; }
        public GameObject contents;   // everything that appears on the plot
        int shownPlot = int.MinValue;

        void Awake() => Instance = this;

        void Update()
        {
            var stats = PlayerStats.Local;
            int plot = stats != null && stats.progress != null ? stats.progress.plot : -1;
            if (plot == shownPlot) return;
            shownPlot = plot;
            var target = plot >= 0 ? Plot.Get(plot) : null;
            contents.SetActive(target != null);
            foreach (var p in Plot.All)
                if (p.forSaleSign != null) p.forSaleSign.SetActive(p != target);
            if (target != null) contents.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
        }
    }
}
