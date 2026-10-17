// Stylized low-poly look for the world: banded light with a soft terminator, tinted shadows,
// cloud shadows, wet darkening and sky-colored fog with aerial perspective (see Atmosphere.cginc).
// Rim light is off by default: on flat ground seen at a grazing angle it would light up the whole far field.
// Lives in Resources so builds include it (Palette finds it with Shader.Find).
Shader "Zeldexile/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.55,0.6,0.82,1)
        _RimColor ("Rim (alpha = strength)", Color) = (1,1,1,0)
        _BandSoftness ("Band Softness", Range(0.001,0.3)) = 0.05
        _Specular ("Specular", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        CGPROGRAM
        #pragma surface ToonSurf Toon fullforwardshadows addshadow noambient nofog finalcolor:ToonFinal
        #pragma target 3.0
        #include "ToonLighting.cginc"
        ENDCG
    }

    Fallback "Diffuse"
}
