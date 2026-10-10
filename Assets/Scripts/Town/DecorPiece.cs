using UnityEngine;

namespace DreamWorld.Town
{
    // A piece of furniture you placed at home (prefabs in Resources/Decor/<recipe id>, built by WorldBuilder.Decor).
    // Knows which of its materials are "wood" so the same bench can be oak, birch or red cedar.
    public class DecorPiece : MonoBehaviour
    {
        public Renderer[] woodRenderers;   // with woodSlots: renderer i's material woodSlots[i] takes the wood tint
        public int[] woodSlots;
        [Range(0, 1)] public float woodStrength = 1f;   // 1 = the wood's full color; lower keeps more of the model's own colors
        [System.NonSerialized] public int index = -1;   // which entry in progress.decor this is

        static readonly int ColorId = Shader.PropertyToID("_Color");
        MaterialPropertyBlock block;

        public void SetWood(Color wood)
        {
            if (woodRenderers == null) return;
            var c = Color.Lerp(Color.white, wood, woodStrength);
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < woodRenderers.Length; i++)
            {
                var r = woodRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block, woodSlots[i]);
                block.SetColor(ColorId, c);
                r.SetPropertyBlock(block, woodSlots[i]);
            }
        }

        // The placing preview: everything goes green (fits) or red (doesn't).
        public void SetGhost(bool ok)
        {
            var c = ok ? new Color(0.6f, 1.6f, 0.8f, 1f) : new Color(1.8f, 0.45f, 0.5f, 1f);
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                block ??= new MaterialPropertyBlock();
                for (int m = 0; m < r.sharedMaterials.Length; m++)
                {
                    r.GetPropertyBlock(block, m);
                    block.SetColor(ColorId, c);
                    r.SetPropertyBlock(block, m);
                }
            }
        }
    }
}
