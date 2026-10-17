#ifndef ZELDEXILE_ATMOSPHERE
#define ZELDEXILE_ATMOSPHERE

// Globals written every frame by AtmosphereController (time of day + weather).
float4 _ZxSkyZenith;
float4 _ZxSkyHorizon;
float4 _ZxSkyGround;
float4 _ZxFogColor;
float4 _ZxFogParams;    // x start (m), y end (m), z height falloff (1/m), w max density
float4 _ZxAerialTint;   // rgb distant haze color, a strength
float4 _ZxSunColor;
float4 _ZxSunDir;       // xyz direction toward the sun, w = 1 by day
float4 _ZxAmbient;      // rgb multiplier for the shadow side, a = 1 when set
float4 _ZxCloudParams;  // x coverage, y scale, z speed, w shadow strength
float4 _ZxWind;         // xy direction, z strength, w gust time
float _ZxWetness;
float4 _ZxPlayerPos;

float ZxHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float ZxNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = ZxHash(i);
    float b = ZxHash(i + float2(1, 0));
    float c = ZxHash(i + float2(0, 1));
    float d = ZxHash(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float ZxClouds(float2 uv)
{
    return ZxNoise(uv) * 0.6 + ZxNoise(uv * 2.17 + 17.0) * 0.28 + ZxNoise(uv * 4.9 + 3.0) * 0.12;
}

// 1 = full sun, lower under a cloud shadow sweeping over the ground.
float ZxCloudShadow(float3 worldPos)
{
    if (_ZxCloudParams.w <= 0.0) return 1.0;
    float2 uv = worldPos.xz * _ZxCloudParams.y + _ZxWind.xy * _Time.y * _ZxCloudParams.z;
    float n = ZxClouds(uv);
    float cover = smoothstep(1.0 - _ZxCloudParams.x, 1.0 - _ZxCloudParams.x + 0.12, n);
    return 1.0 - cover * _ZxCloudParams.w;
}

float ZxFogAmount(float3 worldPos)
{
    if (_ZxFogParams.y <= _ZxFogParams.x) return 0.0;
    float d = distance(worldPos, _WorldSpaceCameraPos);
    float dist = saturate((d - _ZxFogParams.x) / (_ZxFogParams.y - _ZxFogParams.x));
    float heightFade = exp(-max(0.0, worldPos.y) * _ZxFogParams.z);
    return saturate(dist * lerp(1.0, heightFade, 0.55)) * _ZxFogParams.w;
}

float3 ZxFogColorFor(float3 viewDir)
{
    float sun = pow(saturate(dot(viewDir, _ZxSunDir.xyz)), 6.0);
    return lerp(_ZxFogColor.rgb, _ZxSunColor.rgb, sun * 0.45);
}

// Aerial perspective (distant shapes drift toward a blue haze) followed by fog colored by the sky.
float3 ZxApplyAtmosphere(float3 color, float3 worldPos)
{
    if (_ZxFogParams.y <= _ZxFogParams.x) return color;
    float d = distance(worldPos, _WorldSpaceCameraPos);
    float aerial = saturate((d - _ZxFogParams.x * 0.4) / _ZxFogParams.y);
    color = lerp(color, color * 0.55 + _ZxAerialTint.rgb * 0.45, aerial * _ZxAerialTint.a);
    float3 viewDir = normalize(worldPos - _WorldSpaceCameraPos);
    return lerp(color, ZxFogColorFor(viewDir), ZxFogAmount(worldPos));
}

float3 ZxAmbient()
{
    return _ZxAmbient.a > 0.0 ? _ZxAmbient.rgb : float3(1, 1, 1);
}

#endif
