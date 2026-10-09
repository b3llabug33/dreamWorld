// PS1-style lit shader for characters, props and the world.
//  - Vertex snapping: vertices snap to the low-res pixel grid, which gives the PS1 "wobble".
//  - Affine texture mapping: textures warp slightly on big polygons, like the PS1 (no perspective correction).
//  - Per-vertex (Gouraud) lighting from the main directional light + ambient.
// _Color is kept as the tint so CharacterAppearance's MaterialPropertyBlock still works.
Shader "DreamWorld/PSX Lit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SnapStrength ("Vertex Snap", Range(0,1)) = 1
        _AffineStrength ("Affine Warp", Range(0,1)) = 0.6
        [Toggle] _Unlit ("Unlit (ignore lights)", Float) = 0
        _Cutoff ("Alpha Cutoff (0 = off)", Range(0,1)) = 0
        _Softness ("Soft Shading", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _SnapStrength;
            float _AffineStrength;
            float _Unlit;
            float _Cutoff;
            float _Softness;
            // Set globally by PSXCamera: xy = low-res render size. Falls back to 320x240.
            float4 _PSXResolution;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 uvw : TEXCOORD0;     // uv * w and w, for the affine trick
                float2 uvPersp : TEXCOORD1; // normal perspective-correct uv
                fixed3 light : COLOR0;
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata v)
            {
                v2f o;
                float4 clip = UnityObjectToClipPos(v.vertex);

                // Snap to the low-res grid in screen space, then go back to clip space.
                float2 res = _PSXResolution.x > 0 ? _PSXResolution.xy : float2(320, 240);
                float2 ndc = clip.xy / clip.w;
                float2 snapped = floor(ndc * res * 0.5 + 0.5) / (res * 0.5);
                clip.xy = lerp(ndc, snapped, _SnapStrength) * clip.w;
                o.pos = clip;

                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                // GPUs divide interpolated values by w; pre-multiplying by w and dividing
                // back in the fragment cancels that out, giving PS1-style affine mapping.
                o.uvw = float3(uv * clip.w, clip.w);
                o.uvPersp = uv;

                float3 n = UnityObjectToWorldNormal(v.normal);
                float ndl = saturate(dot(n, normalize(_WorldSpaceLightPos0.xyz)));
                float3 lit = ShadeSH9(float4(n, 1)) + _LightColor0.rgb * ndl;
                #ifdef VERTEXLIGHT_ON
                // up to 4 nearby point lights (lamps, glowing mushrooms), lit per vertex like the PS1
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                lit += Shade4PointLights(unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
                                         unity_LightColor[0].rgb, unity_LightColor[1].rgb,
                                         unity_LightColor[2].rgb, unity_LightColor[3].rgb,
                                         unity_4LightAtten0, worldPos, n);
                #endif
                // Softness pulls the shading toward full brightness: faces/skin read cleaner,
                // closer to flat anime coloring, without losing the PS1 vertex lighting entirely.
                lit = lerp(lit, max(lit, 1), _Softness);
                o.light = lerp(lit, 1, _Unlit);

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 affineUV = i.uvw.xy / i.uvw.z;
                float2 uv = lerp(i.uvPersp, affineUV, _AffineStrength);
                fixed4 col = tex2D(_MainTex, uv) * _Color;
                // Pixels below the cutoff are skipped entirely: used by the face decal's transparent texels.
                clip(col.a - _Cutoff);
                col.rgb *= i.light;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    // Borrow a shadow caster pass so characters still drop a shadow on the floor.
    Fallback "Legacy Shaders/VertexLit"
}
