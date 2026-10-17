#ifndef ZELDEXILE_TOON_LIGHTING
#define ZELDEXILE_TOON_LIGHTING

#include "Atmosphere.cginc"

fixed4 _Color;
fixed4 _ShadowTint;
fixed4 _RimColor;
half _BandSoftness;
half _Specular;

struct Input
{
    float4 color : COLOR;
    float3 worldPos;
    float3 worldNormal;
};

// Albedo from color x vertex color, darkened when wet; cloud shadow travels in Gloss, specular strength in Specular.
void ToonSurf (Input IN, inout SurfaceOutput o)
{
    float up = saturate(IN.worldNormal.y);
    float3 albedo = _Color.rgb * IN.color.rgb;
    albedo *= lerp(1.0, 0.68, _ZxWetness * (0.4 + 0.6 * up));
    o.Albedo = albedo;
    o.Gloss = ZxCloudShadow(IN.worldPos);
    o.Specular = _Specular + _ZxWetness * 0.35 * up;
    o.Alpha = 1;
}

// Light bands: soft terminator, tinted shadow side (scaled by ambient for night), a faint second band,
// a hard specular glint (wet surfaces shine) and a rim on the lit side only.
half4 LightingToon (SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
{
    half ndl = dot(s.Normal, lightDir);
#ifdef UNITY_PASS_FORWARDADD
    // Extra lights (camp fires, lamps) only add their lit band, faded by distance. Adding the shadow side here
    // would brighten the whole screen rectangle the light covers.
    half3 added = s.Albedo * _LightColor0.rgb * smoothstep(-0.02, _BandSoftness, ndl) * atten;
    return half4(added, s.Alpha);
#else
    half shadow = smoothstep(0.35, 0.65, atten) * s.Gloss;
    half lit = smoothstep(-0.02, _BandSoftness, ndl) * shadow;

    half3 shadowSide = s.Albedo * _ShadowTint.rgb * ZxAmbient();
    half3 litSide = s.Albedo * _LightColor0.rgb;
    half3 c = lerp(shadowSide, litSide, lit);
    c += litSide * smoothstep(0.6, 0.6 + _BandSoftness, ndl) * 0.08 * shadow;

    half3 h = normalize(lightDir + viewDir);
    c += _LightColor0.rgb * smoothstep(0.93, 0.93 + _BandSoftness * 0.5, dot(s.Normal, h)) * s.Specular * lit;

    half rim = 1.0 - saturate(dot(normalize(viewDir), s.Normal));
    c += _RimColor.rgb * _LightColor0.rgb * smoothstep(0.62, 0.62 + _BandSoftness, rim) * lit * _RimColor.a;

    return half4(c, s.Alpha);
#endif
}

void ToonFinal (Input IN, SurfaceOutput o, inout fixed4 color)
{
#ifdef UNITY_PASS_FORWARDADD
    color.rgb *= 1.0 - ZxFogAmount(IN.worldPos);
#else
    color.rgb = ZxApplyAtmosphere(color.rgb, IN.worldPos);
#endif
}

#endif
