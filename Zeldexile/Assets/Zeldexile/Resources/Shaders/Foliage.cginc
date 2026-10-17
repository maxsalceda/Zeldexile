#ifndef ZELDEXILE_FOLIAGE
#define ZELDEXILE_FOLIAGE

#include "Atmosphere.cginc"

// Instanced nature props (trees, rocks, plants). Colors come in the vertex color; its alpha is how much the
// vertex moves in the wind (leaves 1, trunks 0). One shared wind direction (_ZxWind) moves everything.

half _WindStrength;

float3 ZxFoliageRoot()
{
    return mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
}

// World-space offset for a vertex: slow gusts rolling across the valley plus a light flutter of the leaves.
float3 ZxFoliageWind(float3 root, float3 world, float weight)
{
    float t = _Time.y;
    float gust = ZxNoise(root.xz * 0.045 - _ZxWind.xy * t * 0.55);
    float sway = (gust - 0.3) * (0.55 + _ZxWind.z * 0.8);
    float h = max(0.0, world.y - root.y);
    float bend = weight * (0.03 + 0.016 * h) * _WindStrength;
    float flutter = sin(t * 2.9 + dot(world.xz, float2(0.83, 1.27)) + root.x) * 0.22;
    return float3(_ZxWind.x, 0, _ZxWind.y) * sway * bend + float3(flutter, 0, flutter * 0.6) * bend * 0.35;
}

// Small per-instance tint so a field of the same model never looks stamped.
float3 ZxFoliageTint(float3 root)
{
    float h = ZxHash(floor(root.xz * 0.73) + 0.5);
    return lerp(float3(0.9, 0.93, 0.88), float3(1.08, 1.06, 1.0), h);
}

#endif
