// Anime-style toon for smooth meshes (the hero exported by build_hero_game.py --anime): a hard two-tone band with
// a blue-leaning shadow, like toon_material in build_hero_hd.py, and an inverted-hull outline grown along the
// vertex normal. Unlike Zeldexile/ToonOutline (hull grown from the piece's center, for faceted convex parts), this
// needs one smooth normal per vertex, which the smooth export guarantees.
// The hull is pushed out in screen space, so the line keeps about the same width in pixels: _OutlineWidth in
// meters, clamped to [_OutlineMinPx, _OutlineMaxPx]. It is also pushed back from the camera by _OutlineDepth, so
// a piece's hull does not poke through a thin shell lying just over it (shirt under the jacket): it only shows
// past the silhouette, where nothing close is behind.
// _OutlineWidth = 0 drops the hull (eye discs, nose); _Unlit = 1 ignores the light (eyes, as in the HD render).
Shader "Zeldexile/ToonOutlineSmooth"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.66,0.58,0.72,1)
        _BandOffset ("Band Offset (N.L)", Range(-0.5,0.5)) = -0.12
        _BandSoftness ("Band Softness", Range(0.001,0.2)) = 0.012
        _Unlit ("Unlit", Range(0,1)) = 0
        _OutlineColor ("Outline Color", Color) = (0.07,0.05,0.06,1)
        _OutlineWidth ("Outline Width (m)", Range(0,0.05)) = 0.01
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
                    o.pos = float4(0, 0, -2, 1); // outside the clip volume: no hull
                    return o;
                }

                float3 view = UnityObjectToViewPos(v.vertex);
                float3 n = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                // Away from the camera (view space looks down -Z), so only the part past the silhouette shows.
                view.z -= _OutlineDepth;
                float4 pos = mul(UNITY_MATRIX_P, float4(view, 1));

                // Meters to pixels at this depth (works for perspective and orthographic: w = 1 there).
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
        #pragma surface ToonSurf ToonSmooth fullforwardshadows addshadow noambient nofog finalcolor:ToonFinal
        #pragma target 3.0
        #include "ToonLighting.cginc"

        half _BandOffset;
        half _Unlit;

        // Two tones with a hard cut, as the Color Ramp (Constant) of the HD render: lit = base color, shadow = base
        // x shadow tint (darker and bluer). Cast shadows fall on the same band. No specular, rim or second band.
        half4 LightingToonSmooth (SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir);
        #ifdef UNITY_PASS_FORWARDADD
            half3 added = s.Albedo * _LightColor0.rgb * smoothstep(_BandOffset - _BandSoftness, _BandOffset + _BandSoftness, ndl) * atten;
            return half4(added * (1 - _Unlit), s.Alpha);
        #else
            half shadow = smoothstep(0.4, 0.6, atten) * s.Gloss;
            half lit = smoothstep(_BandOffset - _BandSoftness, _BandOffset + _BandSoftness, ndl) * shadow;
            half3 shadowSide = s.Albedo * _ShadowTint.rgb * ZxAmbient();
            half3 litSide = s.Albedo * _LightColor0.rgb;
            half3 c = lerp(shadowSide, litSide, lit);
            c = lerp(c, s.Albedo * max(_LightColor0.rgb, ZxAmbient()), _Unlit);
            return half4(c, s.Alpha);
        #endif
        }
        ENDCG
    }

    Fallback "Diffuse"
}
