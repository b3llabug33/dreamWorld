// Night sky for the skybox: a dusk gradient (horizon glow -> deep indigo), a soft moon and
// pixel stars. Stars are picked per direction cell so they stay put and look chunky.
Shader "DreamWorld/PSX Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.05, 0.04, 0.14, 1)
        _Middle ("Middle", Color) = (0.17, 0.12, 0.32, 1)
        _Horizon ("Horizon", Color) = (0.55, 0.32, 0.50, 1)
        _Ground ("Below Horizon", Color) = (0.10, 0.08, 0.16, 1)
        _StarDensity ("Star Density", Range(0, 0.2)) = 0.05
        _StarCells ("Star Size (cells)", Float) = 140
        _MoonDir ("Moon Direction", Vector) = (-0.4, 0.45, 0.8, 0)
        _MoonColor ("Moon Color", Color) = (1, 0.95, 0.85, 1)
        _MoonSize ("Moon Size", Range(0.99, 0.9999)) = 0.9985
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Top, _Middle, _Horizon, _Ground, _MoonColor;
            float _StarDensity, _StarCells, _MoonSize;
            float4 _MoonDir;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.dir = vertex.xyz;
                return o;
            }

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                fixed3 col = h < 0 ? lerp(_Horizon.rgb, _Ground.rgb, saturate(-h * 6))
                                   : (h < 0.25 ? lerp(_Horizon.rgb, _Middle.rgb, h / 0.25)
                                               : lerp(_Middle.rgb, _Top.rgb, saturate((h - 0.25) / 0.6)));
                // pixel stars, fading out toward the horizon glow
                float3 cell = floor(d * _StarCells);
                float s = hash(cell);
                if (h > 0.08 && s > 1 - _StarDensity)
                    col += (s - (1 - _StarDensity)) / _StarDensity * 0.9 * saturate((h - 0.08) * 4);
                // moon disc + soft halo
                float m = dot(d, normalize(_MoonDir.xyz));
                col = lerp(col, _MoonColor.rgb, step(_MoonSize, m));
                col += _MoonColor.rgb * pow(saturate(m), 60) * 0.25;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
