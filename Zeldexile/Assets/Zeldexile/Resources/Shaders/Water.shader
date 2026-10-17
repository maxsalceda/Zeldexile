// Stylized low-poly water: faceted waves (normals from screen derivatives), shallow-to-deep color
// from scene depth, and a hard foam band where the water meets the shore.
Shader "Zeldexile/Water"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.38,0.80,0.86,0.55)
        _Deep ("Deep", Color) = (0.10,0.34,0.60,0.88)
        _Foam ("Foam", Color) = (0.96,0.98,1,0.95)
        _DepthRange ("Depth Range (m)", Float) = 3
        _FoamWidth ("Foam Width (m)", Float) = 0.45
        _WaveHeight ("Wave Height (m)", Float) = 0.14
        _WaveScale ("Wave Scale", Float) = 0.32
        _WaveSpeed ("Wave Speed", Float) = 1.1
        _ShadowTint ("Shadow Tint", Color) = (0.70,0.76,0.92,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "Atmosphere.cginc"

            fixed4 _Shallow, _Deep, _Foam, _ShadowTint;
            float _DepthRange, _FoamWidth, _WaveHeight, _WaveScale, _WaveSpeed;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                float4 screen : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
            };

            float Wave (float2 xz)
            {
                float t = _Time.y * _WaveSpeed;
                return (sin(xz.x * _WaveScale + t) * cos(xz.y * _WaveScale * 0.8 + t * 1.3)
                      + 0.5 * sin((xz.x + xz.y) * _WaveScale * 1.7 - t * 0.9)) * _WaveHeight;
            }

            v2f vert (appdata_base v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                world.y += Wave(world.xz);
                o.world = world;
                o.pos = UnityWorldToClipPos(world);
                o.screen = ComputeScreenPos(o.pos);
                o.eyeDepth = -UnityWorldToViewPos(world).z;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Flat (faceted) normal per triangle.
                float3 n = normalize(cross(ddy(i.world), ddx(i.world)));
                if (n.y < 0) n = -n;

                float ndl = dot(n, normalize(_WorldSpaceLightPos0.xyz));
                float lit = smoothstep(0.0, 0.04, ndl);

                float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                float depth = max(0, scene - i.eyeDepth);

                fixed4 c = lerp(_Shallow, _Deep, saturate(depth / _DepthRange));
                lit *= ZxCloudShadow(i.world);
                c.rgb *= lerp(_ShadowTint.rgb * ZxAmbient(), _LightColor0.rgb, lit);
                // Sky reflection at grazing angles (cheap Fresnel toward the horizon color).
                float3 viewDirW = normalize(_WorldSpaceCameraPos - i.world);
                float fresnel = pow(1.0 - saturate(dot(n, viewDirW)), 4.0);
                c.rgb = lerp(c.rgb, _ZxSkyHorizon.rgb, fresnel * 0.5 * step(0.001, _ZxSkyHorizon.a));
                // Rain: tiny flickering ripples.
                float ripple = step(0.985, ZxHash(floor(i.world.xz * 3.0) + floor(_Time.y * 6.0)));
                c.rgb += ripple * _ZxWetness * 0.25;

                // Sun glint: a hard highlight band on facets facing the reflection.
                float3 view = normalize(_WorldSpaceCameraPos - i.world);
                float3 h = normalize(view + normalize(_WorldSpaceLightPos0.xyz));
                c.rgb += step(0.985, dot(n, h)) * 0.35 * lit;

                // Shore foam: a wobbling band of fixed width.
                float wobble = sin(i.world.x * 1.3 + _Time.y * 2.0) * sin(i.world.z * 1.1 - _Time.y * 1.6) * 0.12;
                float foam = 1.0 - step(_FoamWidth + wobble, depth);
                float3 foamLight = lerp(_ShadowTint.rgb * ZxAmbient(), _LightColor0.rgb, max(lit, 0.35));
                c = lerp(c, fixed4(_Foam.rgb * foamLight, _Foam.a), foam * _Foam.a);

                c.rgb = ZxApplyAtmosphere(c.rgb, i.world);
                return c;
            }
            ENDCG
        }
    }
}
