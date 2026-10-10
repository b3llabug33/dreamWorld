using System.Collections.Generic;
using UnityEngine;

namespace DreamWorld.Town
{
    // One buildable plot of land in the plots area. The "for sale" sign hides once it's yours.
    public class Plot : MonoBehaviour
    {
        public static readonly List<Plot> All = new List<Plot>();
        public int index;
        public GameObject forSaleSign;

        void OnEnable() { All.Add(this); All.Sort((a, b) => a.index.CompareTo(b.index)); }
        void OnDisable() => All.Remove(this);

        public static Plot Get(int index) => All.Find(p => p.index == index);
    }
}
