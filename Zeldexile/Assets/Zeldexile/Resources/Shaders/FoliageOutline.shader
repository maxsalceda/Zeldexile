// Instanced nature props with the same dark contour as characters and props (near trees, rocks, logs).
// Each part's center travels in TEXCOORD1, so the inverted hull grows evenly around every part of a merged model.
Shader "Zeldexile/FoliageOutline"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.55,0.6,0.82,1)
        _RimColor ("Rim (alpha = strength)", Color) = (1,1,1,0.25)
        _BandSoftness ("Band Softness", Range(0.001,0.3)) = 0.06
        _Specular ("Specular", Range(0,1)) = 0.1
        _WindStrength ("Wind Strength", Range(0,2)) = 1
        _OutlineColor ("Outline Color", Color) = (0.1,0.11,0.09,1)
        _OutlineWidth ("Outline Width (m)", Range(0,0.1)) = 0.03
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
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "Foliage.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 center : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                float3 root = ZxFoliageRoot();
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 center = mul(unity_ObjectToWorld, float4(v.center.xyz, 1)).xyz;
                float3 wind = ZxFoliageWind(root, world, v.color.a);
                float3 dir = world - center;
                float len = length(dir);
                dir = len > 1e-5 ? dir / len : float3(0, 1, 0);
                // Thinner contour on small parts (fruit, twigs) so it never swallows them.
                float width = _OutlineWidth * saturate(v.center.w);
                world += wind + dir * width;
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
        #pragma surface ToonSurf Toon fullforwardshadows addshadow noambient nofog finalcolor:ToonFinal vertex:FoliageVert
        #pragma target 3.0
        #pragma multi_compile_instancing
        #include "ToonLighting.cginc"
        #include "Foliage.cginc"

        void FoliageVert(inout appdata_full v)
        {
            float3 root = ZxFoliageRoot();
            float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 offset = ZxFoliageWind(root, world, v.color.a);
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, offset);
            v.color.rgb *= ZxFoliageTint(root);
        }
        ENDCG
    }

    Fallback "Diffuse"
}
