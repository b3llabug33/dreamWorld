// Full-screen PS1 color pass, run on the already-downscaled image by PSXCamera:
// reduces each color channel to a few levels (PS1 used 5 bits = 32 levels) and hides the
// banding with a 4x4 ordered (Bayer) dither, computed per low-res pixel.
Shader "Hidden/DreamWorld/PSXPost"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // zw = low-res width/height
            float _ColorLevels;        // levels per channel, e.g. 32
            float _DitherStrength;     // 0 = no dither, 1 = one full color step

            static const float bayer[16] = {
                 0,  8,  2, 10,
                12,  4, 14,  6,
                 3, 11,  1,  9,
                15,  7, 13,  5
            };

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                int2 p = int2(i.uv * _MainTex_TexelSize.zw) % 4;
                float threshold = bayer[p.y * 4 + p.x] / 16.0 - 0.5;

                float steps = max(_ColorLevels - 1, 1);
                col.rgb = floor(col.rgb * steps + 0.5 + threshold * _DitherStrength) / steps;
                return saturate(col);
            }
            ENDCG
        }
    }
}
