using UnityEngine;

namespace DreamWorld.World
{
    // Slowly drifts a water surface's texture so ponds and streams look like they're moving.
    // Uses a property block, so every water surface can share one material.
    public class WaterScroll : MonoBehaviour
    {
        public Vector2 speed = new Vector2(0.03f, 0.018f);

        static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");
        Renderer rend;
        MaterialPropertyBlock block;
        Vector4 baseSt;

        void Start()
        {
            rend = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            var m = rend.sharedMaterial;
            baseSt = new Vector4(m.mainTextureScale.x, m.mainTextureScale.y, m.mainTextureOffset.x, m.mainTextureOffset.y);
        }

        void Update()
        {
            if (rend == null) return;
            var off = speed * Time.time;
            block.SetVector(MainTexSt, new Vector4(baseSt.x, baseSt.y, baseSt.z + off.x, baseSt.w + off.y));
            rend.SetPropertyBlock(block);
        }
    }
}
