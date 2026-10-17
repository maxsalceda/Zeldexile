// Round, soft-edged particles tinted by their vertex color (fire, spray, mist), with the shared fog.
// Needs no texture: the disc is drawn from the quad's UVs.
Shader "Zeldexile/SoftParticle"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Softness ("Edge Softness", Range(0.01,0.5)) = 0.3
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Atmosphere.cginc"

            fixed4 _Color;
            half _Softness;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color * _Color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d = distance(i.uv, float2(0.5, 0.5)) * 2.0;
                float a = 1.0 - smoothstep(1.0 - _Softness, 1.0, d);
                fixed4 c = i.color;
                c.rgb = ZxApplyAtmosphere(c.rgb, i.world);
                c.a *= a;
                return c;
            }
            ENDCG
        }
    }
}
