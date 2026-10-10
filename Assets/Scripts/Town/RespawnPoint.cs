using UnityEngine;

namespace DreamWorld.Town
{
    // Where you wake up when you don't own a plot yet (the town square).
    public class RespawnPoint : MonoBehaviour
    {
        public static RespawnPoint Town { get; private set; }

        void OnEnable() => Town = this;
        void OnDisable() { if (Town == this) Town = null; }
    }
}
