// Zeldexile/ToonOutlineSmooth for textured models (the skinned Meshy hero): the same hard two-tone band and screen-
// space contour, with the albedo read from _MainTex x _Color instead of a flat slot color. The Meshy PBR maps
// (metallic, roughness, normal) are left out on purpose: the toon band would flatten them anyway. It does not include
// ToonLighting.cginc: surface passes refer to a struct named Input, and that file declares its own.
Shader "Zeldexile/ToonOutlineTextured"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.66,0.58,0.72,1)
        _BandOffset ("Band Offset (N.L)", Range(-0.5,0.5)) = -0.12
        _BandSoftness ("Band Softness", Range(0.001,0.2)) = 0.012
        _OutlineColor ("Outline Color", Color) = (0.07,0.05,0.06,1)
        _OutlineWidth ("Outline Width (m)", Range(0,0.05)) = 0.008
        _OutlineMinPx ("Outline Min (px)", Range(0,4)) = 1.2
        _OutlineMaxPx ("Outline Max (px)", Range(0,8)) = 3
        _OutlineDepth ("Outline Depth Push (m)", Range(0,0.1)) = 0.012
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Atmosphere.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineMinPx;
            float _OutlineMaxPx;
            float _OutlineDepth;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                if (_OutlineWidth <= 0)
                {
                    o.pos = float4(0, 0, -2, 1);
                    return o;
                }

                float3 view = UnityObjectToViewPos(v.vertex);
                float3 n = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                view.z -= _OutlineDepth;
                float4 pos = mul(UNITY_MATRIX_P, float4(view, 1));

                float pxPerMeter = _ScreenParams.y * UNITY_MATRIX_P[1][1] * 0.5 / pos.w;
                float px = clamp(_OutlineWidth * pxPerMeter, _OutlineMinPx, _OutlineMaxPx);
                float len = length(n.xy);
                float2 dir = len > 1e-4 ? n.xy / len : float2(0, 0);
                pos.xy += dir * px * 2.0 / _ScreenParams.xy * pos.w;
                o.pos = pos;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(ZxApplyAtmosphere(_OutlineColor.rgb, i.world), 1);
            }
            ENDCG
        }

        CGPROGRAM
        #pragma surface TexturedSurf ToonTextured fullforwardshadows noambient nofog nometa finalcolor:TexturedFinal
        #pragma target 3.0
        #include "Atmosphere.cginc"

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _ShadowTint;
        half _BandSoftness;
        half _BandOffset;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };

        // ToonSurf with the texture as base color (no vertex color on the Meshy export).
        void TexturedSurf (Input IN, inout SurfaceOutput o)
        {
            float up = saturate(IN.worldNormal.y);
            float3 albedo = tex2D(_MainTex, IN.uv_MainTex).rgb * _Color.rgb;
            albedo *= lerp(1.0, 0.68, _ZxWetness * (0.4 + 0.6 * up));
            o.Albedo = albedo;
            o.Gloss = ZxCloudShadow(IN.worldPos);
            o.Specular = 0;
            o.Alpha = 1;
        }

        void TexturedFinal (Input IN, SurfaceOutput o, inout fixed4 color)
        {
        #ifdef UNITY_PASS_FORWARDADD
            color.rgb *= 1.0 - ZxFogAmount(IN.worldPos);
        #else
            color.rgb = ZxApplyAtmosphere(color.rgb, IN.worldPos);
        #endif
        }

        half4 LightingToonTextured (SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir);
        #ifdef UNITY_PASS_FORWARDADD
            half3 added = s.Albedo * _LightColor0.rgb * smoothstep(_BandOffset - _BandSoftness, _BandOffset + _BandSoftness, ndl) * atten;
            return half4(added, s.Alpha);
        #else
            half shadow = smoothstep(0.4, 0.6, atten) * s.Gloss;
            half lit = smoothstep(_BandOffset - _BandSoftness, _BandOffset + _BandSoftness, ndl) * shadow;
            half3 shadowSide = s.Albedo * _ShadowTint.rgb * ZxAmbient();
            half3 litSide = s.Albedo * _LightColor0.rgb;
            return half4(lerp(shadowSide, litSide, lit), s.Alpha);
        #endif
        }
        ENDCG
    }

    Fallback "Diffuse"
}
