using System.Collections.Generic;
using UnityEngine;

namespace DreamWorld.Gameplay
{
    // Something you can walk up to and press E on. The HUD shows the nearest one's prompt.
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();
        public float range = 2.2f;

        public abstract string Prompt(PlayerStats player);
        public abstract void Interact(PlayerStats player);

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        public static Interactable Nearest(Vector3 position)
        {
            Interactable best = null;
            float bestDist = float.MaxValue;
            foreach (var i in All)
            {
                float d = Vector3.Distance(position, i.transform.position);
                if (d <= i.range && d < bestDist) { best = i; bestDist = d; }
            }
            return best;
        }
    }
}
