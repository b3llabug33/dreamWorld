using UnityEngine;

namespace DreamWorld.Rendering
{
    // Put this on the main camera. Each frame the 3D image is shrunk to a low PS1-like
    // resolution, color-reduced + dithered (PSXPost shader), then stretched back up with
    // hard pixel edges. Screen-space overlay UI is drawn after this, so menus stay crisp.
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public class PSXCamera : MonoBehaviour
    {
        [Tooltip("Vertical resolution of the 3D image. PS1 games were mostly 240.")]
        [Range(120, 720)] public int targetHeight = 240;
        [Tooltip("Color levels per channel. 32 = PS1's 15-bit color; lower = crunchier.")]
        [Range(2, 256)] public int colorLevels = 32;
        [Range(0f, 2f)] public float ditherStrength = 1f;
        public bool effectEnabled = true;
        // Assigned in the scene so the shader is included in builds (Shader.Find alone can miss hidden shaders).
        public Shader postShader;

        static readonly int ResolutionId = Shader.PropertyToID("_PSXResolution");
        Material postMaterial;

        void OnEnable()
        {
            var shader = postShader != null ? postShader : Shader.Find("Hidden/DreamWorld/PSXPost");
            if (shader != null) postMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDisable()
        {
            if (postMaterial != null) DestroyImmediate(postMaterial);
            // Turn vertex snapping back to the shader's default grid.
            Shader.SetGlobalVector(ResolutionId, Vector4.zero);
        }

        Vector2Int LowRes(int screenWidth, int screenHeight)
        {
            int height = Mathf.Min(targetHeight, screenHeight);
            int width = Mathf.Max(1, Mathf.RoundToInt(height * (float)screenWidth / screenHeight));
            return new Vector2Int(width, height);
        }

        void OnPreCull()
        {
            // Tell PSX Lit materials which grid to snap vertices to.
            var cam = GetComponent<Camera>();
            var res = effectEnabled ? LowRes(cam.pixelWidth, cam.pixelHeight) : new Vector2Int(cam.pixelWidth, cam.pixelHeight);
            Shader.SetGlobalVector(ResolutionId, new Vector4(res.x, res.y, 0, 0));
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!effectEnabled || postMaterial == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            var res = LowRes(source.width, source.height);
            var low = RenderTexture.GetTemporary(res.x, res.y, 0, source.format);
            var crunched = RenderTexture.GetTemporary(res.x, res.y, 0, source.format);
            low.filterMode = FilterMode.Point;
            crunched.filterMode = FilterMode.Point;
            source.filterMode = FilterMode.Point;

            Graphics.Blit(source, low);                         // shrink (nearest pixel)
            postMaterial.SetFloat("_ColorLevels", colorLevels);
            postMaterial.SetFloat("_DitherStrength", ditherStrength);
            Graphics.Blit(low, crunched, postMaterial);         // color reduce + dither
            Graphics.Blit(crunched, destination);               // stretch back up, hard edges

            RenderTexture.ReleaseTemporary(low);
            RenderTexture.ReleaseTemporary(crunched);
        }
    }
}
