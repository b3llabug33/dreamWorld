using UnityEngine;

namespace DreamWorld.CharacterCreation
{
    // Says which submeshes of this renderer take the slot color. Missing entries default to tinted.
    // Filled in from Dreamer.manifest.json by the Dreamer setup menu.
    public class TintMask : MonoBehaviour
    {
        public bool[] tintSubmesh;

        public bool Tints(int submesh) => tintSubmesh == null || submesh >= tintSubmesh.Length || tintSubmesh[submesh];
    }
}
