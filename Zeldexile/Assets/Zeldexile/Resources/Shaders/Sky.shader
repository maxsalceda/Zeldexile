// Procedural sky driven by AtmosphereController: gradient, sun disk and glow, moon, stars,
// and painterly cloud shapes thresholded from noise (no textures).
Shader "Zeldexile/Sky"
{
    Properties
    {
        _CloudHeight ("Cloud Plane Scale", Float) = 0.6
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Atmosphere.cginc"

            float _CloudHeight;
            float _ZxFlash;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;

                float3 col = y > 0
                    ? lerp(_ZxSkyHorizon.rgb, _ZxSkyZenith.rgb, pow(saturate(y), 0.55))
                    : lerp(_ZxSkyHorizon.rgb, _ZxSkyGround.rgb, saturate(-y * 4.0));

                float day = _ZxSunDir.w;
                float sd = dot(d, _ZxSunDir.xyz);
                col += _ZxSunColor.rgb * (smoothstep(0.9988, 0.9992, sd) * 2.0 + pow(saturate(sd), 48.0) * 0.4) * day;

                float night = 1.0 - day;
                float md = dot(d, -_ZxSunDir.xyz);
                col += float3(0.92, 0.95, 1.0) * smoothstep(0.9991, 0.9994, md) * night;
                col += float3(0.4, 0.5, 0.8) * pow(saturate(md), 32.0) * 0.15 * night;

                float clear = 1.0 - _ZxCloudParams.x;
                float2 starUv = d.xz / (abs(d.y) + 0.25) * 70.0;
                float star = step(0.9975, ZxHash(floor(starUv))) * saturate(y * 5.0) * night * clear;
                col += star * 0.9;

                if (y > 0.0)
                {
                    float2 uv = d.xz / max(y, 0.06) * _CloudHeight + _ZxWind.xy * _Time.y * 0.012;
                    float n = ZxClouds(uv);
                    float coverage = _ZxCloudParams.x;
                    float cloud = smoothstep(1.0 - coverage, 1.0 - coverage + 0.1, n);
                    float core = smoothstep(1.0 - coverage + 0.12, 1.0 - coverage + 0.3, n);
                    float3 lit = lerp(_ZxSkyHorizon.rgb * 1.15, _ZxSunColor.rgb, 0.35 * day) + 0.05;
                    float3 shade = lerp(_ZxFogColor.rgb * 0.75, lit * 0.7, 0.4);
                    float3 cloudCol = lerp(lit, shade, core * coverage);
                    col = lerp(col, cloudCol, cloud * saturate(y * 8.0));
                }

                col += _ZxFlash * float3(0.8, 0.85, 1.0);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
