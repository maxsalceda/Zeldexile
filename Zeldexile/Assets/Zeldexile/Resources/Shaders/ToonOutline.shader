// Toon shading plus an inverted-hull outline. The hull grows from the object's center in world units,
// so it works on faceted (flat-normal) convex parts where normal extrusion would split at the edges.
Shader "Zeldexile/ToonOutline"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.55,0.6,0.82,1)
        _RimColor ("Rim (alpha = strength)", Color) = (1,1,1,0.35)
        _BandSoftness ("Band Softness", Range(0.001,0.3)) = 0.05
        _Specular ("Specular", Range(0,1)) = 0.25
        _OutlineColor ("Outline Color", Color) = (0.07,0.06,0.09,1)
        _OutlineWidth ("Outline Width (m)", Range(0,0.1)) = 0.022
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

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 center = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
                float3 dir = world - center;
                float len = length(dir);
                dir = len > 1e-5 ? dir / len : float3(0,1,0);
                world += dir * _OutlineWidth;
                o.world = world;
                o.pos = UnityWorldToClipPos(world);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(ZxApplyAtmosphere(_OutlineColor.rgb, i.world), 1);
            }
            ENDCG
        }

        CGPROGRAM
        #pragma surface ToonSurf Toon fullforwardshadows addshadow noambient nofog finalcolor:ToonFinal
        #pragma target 3.0
        #include "ToonLighting.cginc"
        ENDCG
    }

    Fallback "Diffuse"
}
