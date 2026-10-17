// Instanced grass clumps: low-poly blades (one triangle each) that sway with wind noise,
// bend away from the player, fade out with distance, and take the shared light, clouds and fog.
Shader "Zeldexile/Grass"
{
    Properties
    {
        _BaseColor ("Base", Color) = (0.22,0.42,0.16,1)
        _TipColor ("Tip", Color) = (0.55,0.76,0.32,1)
        _ShadowTint ("Shadow Tint", Color) = (0.55,0.6,0.82,1)
        _DrawDistance ("Draw Distance", Float) = 70
        _BendRadius ("Bend Radius", Float) = 1.4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "Atmosphere.cginc"

            fixed4 _BaseColor, _TipColor, _ShadowTint;
            float _DrawDistance, _BendRadius;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                float height : TEXCOORD1;
                float tint : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                float3 root = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float h = v.uv.y;

                // Shrink into the ground near the draw distance instead of popping.
                float d = distance(root, _WorldSpaceCameraPos);
                float fade = saturate((_DrawDistance - d) / (_DrawDistance * 0.3));
                world.y = root.y + (world.y - root.y) * fade;

                // Wind: rolling gusts from world-space noise along the wind direction.
                float gust = ZxNoise(root.xz * 0.12 - _ZxWind.xy * _Time.y * 1.5);
                float2 sway = _ZxWind.xy * (gust - 0.25) * (0.35 + _ZxWind.z * 0.5)
                            + float2(sin(_Time.y * 2.3 + root.x * 0.7), cos(_Time.y * 1.9 + root.z * 0.7)) * 0.04;

                // Trample: blades near the player lean away and flatten.
                float2 away = world.xz - _ZxPlayerPos.xz;
                float pd = length(away);
                float push = saturate(1.0 - pd / _BendRadius) * step(abs(world.y - _ZxPlayerPos.y), 1.5);
                world.xz += (sway + away / max(pd, 1e-3) * push * 0.55) * h * h;
                world.y -= push * h * 0.22;

                o.world = world;
                o.pos = UnityWorldToClipPos(world);
                o.height = h;
                o.tint = ZxHash(root.xz);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 col = lerp(_BaseColor.rgb, _TipColor.rgb, i.height);
                col *= lerp(0.85, 1.12, i.tint);
                col *= lerp(1.0, 0.72, _ZxWetness);

                float day = max(_ZxSunDir.w, 0.0);
                float lit = ZxCloudShadow(i.world) * lerp(0.35, 1.0, day);
                float3 sun = _ZxSunColor.rgb * lerp(0.35, 1.0, day);
                col *= lerp(_ShadowTint.rgb * ZxAmbient(), sun, lit * lerp(0.6, 1.0, i.height));

                return fixed4(ZxApplyAtmosphere(col, i.world), 1);
            }
            ENDCG
        }
    }
}
