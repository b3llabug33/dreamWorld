using UnityEngine;

namespace DreamWorld.Gameplay
{
    // A flat ring of dust that spreads out and fades: where a slam lands (moss golem, mushroom knight).
    public class Shockwave : MonoBehaviour
    {
        float age, radius;
        Color color;
        Renderer rend;
        MaterialPropertyBlock block;
        static Material material;

        public static void Spawn(Vector3 at, float radius, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Shockwave";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = at + Vector3.up * 0.05f;
            go.transform.localScale = new Vector3(0.2f, 0.02f, 0.2f);
            if (material == null) material = new Material(Shader.Find("Sprites/Default"));
            var s = go.AddComponent<Shockwave>();
            s.rend = go.GetComponent<Renderer>();
            s.rend.sharedMaterial = material;
            s.rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s.radius = radius;
            s.color = color;
            s.block = new MaterialPropertyBlock();
        }

        void Update()
        {
            age += Time.deltaTime;
            float k = age / 0.4f;
            if (k >= 1f) { Destroy(gameObject); return; }
            float d = Mathf.Lerp(0.3f, radius * 2f, 1f - (1f - k) * (1f - k));
            transform.localScale = new Vector3(d, 0.02f, d);
            block.SetColor("_Color", new Color(color.r, color.g, color.b, color.a * (1f - k)));
            rend.SetPropertyBlock(block);
        }
    }
}
