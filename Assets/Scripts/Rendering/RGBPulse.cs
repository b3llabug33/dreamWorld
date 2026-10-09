using UnityEngine;

namespace DreamWorld.Rendering
{
    // Slowly cycles a light (and optionally a renderer's glow) through a set of colors,
    // like RGB lighting in a gaming PC. Give neighbors different offsets so they don't sync up.
    public class RGBPulse : MonoBehaviour
    {
        public Color[] colors =
        {
            new Color(1f, 0.45f, 0.85f),   // pink
            new Color(0.62f, 0.40f, 1f),   // violet
            new Color(0.35f, 0.75f, 1f),   // cyan
        };
        [Tooltip("Seconds to fade from one color to the next.")]
        public float secondsPerColor = 4f;
        [Tooltip("0-1: where in the cycle this one starts.")]
        public float offset;
        [Tooltip("How much the brightness breathes (0 = steady).")]
        [Range(0f, 1f)] public float breathe = 0.25f;
        [Tooltip("Optional: also tint this renderer's glow (_EmissionColor).")]
        public Renderer glowRenderer;
        public float glowStrength = 1.5f;

        Light glowLight;
        float baseIntensity;
        MaterialPropertyBlock block;

        void Awake()
        {
            glowLight = GetComponent<Light>();
            if (glowLight != null) baseIntensity = glowLight.intensity;
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (colors.Length == 0) return;
            float t = (Time.time / secondsPerColor + offset * colors.Length) % colors.Length;
            int a = Mathf.FloorToInt(t);
            var color = Color.Lerp(colors[a], colors[(a + 1) % colors.Length], Mathf.SmoothStep(0, 1, t - a));
            float pulse = 1f - breathe * 0.5f * (1f + Mathf.Sin(Time.time * 1.3f + offset * 6.28f));

            if (glowLight != null)
            {
                glowLight.color = color;
                glowLight.intensity = baseIntensity * pulse;
            }
            if (glowRenderer != null)
            {
                glowRenderer.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", color * glowStrength * pulse);
                glowRenderer.SetPropertyBlock(block);
            }
        }
    }
}
