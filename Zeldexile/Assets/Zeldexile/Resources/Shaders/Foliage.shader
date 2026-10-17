// Instanced nature props without a contour: distant trees, plants, flowers and small stones.
// Toon light bands, cloud shadows and fog from ToonLighting; wind from Foliage.cginc.
Shader "Zeldexile/Foliage"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.55,0.6,0.82,1)
        _RimColor ("Rim (alpha = strength)", Color) = (1,1,1,0)
        _BandSoftness ("Band Softness", Range(0.001,0.3)) = 0.08
        _Specular ("Specular", Range(0,1)) = 0
        _WindStrength ("Wind Strength", Range(0,2)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

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
