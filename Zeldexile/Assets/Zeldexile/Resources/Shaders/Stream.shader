// Running water for streams, rivers and waterfalls: the lake's faceted look, plus foam streaks that flow
// along the channel (uv.y = meters downstream), white water where the bed is steep (vertex color r) and a
// foam band where the water meets the banks.
Shader "Zeldexile/Stream"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.42,0.84,0.86,0.5)
        _Deep ("Deep", Color) = (0.14,0.50,0.70,0.85)
        _Foam ("Foam", Color) = (0.97,0.99,1,0.95)
        _DepthRange ("Depth Range (m)", Float) = 1.2
        _FoamWidth ("Bank Foam Width (m)", Float) = 0.06
        _FlowSpeed ("Flow Speed (m/s)", Float) = 1.4
        _WaveHeight ("Wave Height (m)", Float) = 0.04
        _ShadowTint ("Shadow Tint", Color) = (0.70,0.76,0.92,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent-1" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "Atmosphere.cginc"

            fixed4 _Shallow, _Deep, _Foam, _ShadowTint;
            float _DepthRange, _FoamWidth, _FlowSpeed, _WaveHeight;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                float4 screen : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float steep : TEXCOORD4;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y;
                world.y += sin(v.uv.y * 1.7 - t * 3.1 + v.uv.x * 3.0) * _WaveHeight * (1.0 + v.color.r * 2.0);
                o.world = world;
                o.pos = UnityWorldToClipPos(world);
                o.screen = ComputeScreenPos(o.pos);
                o.eyeDepth = -UnityWorldToViewPos(world).z;
                o.uv = v.uv;
                o.steep = v.color.r;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(cross(ddy(i.world), ddx(i.world)));
                if (n.y < 0) n = -n;
                float ndl = dot(n, normalize(_WorldSpaceLightPos0.xyz));
                float lit = smoothstep(-0.2, 0.1, ndl) * ZxCloudShadow(i.world);

                float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                float depth = max(0, scene - i.eyeDepth);

                fixed4 c = lerp(_Shallow, _Deep, saturate(depth / _DepthRange));
                c.rgb *= lerp(_ShadowTint.rgb * ZxAmbient(), _LightColor0.rgb, lit);

                // Flow: streaks drift downstream, faster and denser on steep stretches.
                float speed = _FlowSpeed * (1.0 + i.steep * 2.5);
                float2 flow = float2(i.uv.x * 3.0, i.uv.y * 0.9 - _Time.y * speed);
                float streak = ZxNoise(flow * float2(1.0, 0.35)) * 0.7 + ZxNoise(flow * float2(2.3, 0.8) + 7.0) * 0.3;
                float foam = step(0.86 - i.steep * 0.6, streak);
                // Bank foam where the water is shallow against the ground.
                float wobble = sin(i.world.x * 1.7 + _Time.y * 2.4) * 0.08;
                foam = max(foam, 1.0 - step(_FoamWidth + wobble, depth));
                foam = max(foam, smoothstep(0.35, 0.8, i.steep));
                float3 foamLight = lerp(_ShadowTint.rgb * ZxAmbient(), _LightColor0.rgb, max(lit, 0.4));
                c = lerp(c, fixed4(_Foam.rgb * foamLight, _Foam.a), foam * _Foam.a * 0.85);

                // Sun glint.
                float3 view = normalize(_WorldSpaceCameraPos - i.world);
                float3 h = normalize(view + normalize(_WorldSpaceLightPos0.xyz));
                c.rgb += step(0.985, dot(n, h)) * 0.3 * lit;

                c.rgb = ZxApplyAtmosphere(c.rgb, i.world);
                return c;
            }
            ENDCG
        }
    }
}
