Shader "Map/Predawn Sky"
{
    Properties
    {
        _Zenith ("Upper Sky", Color) = (0.025,0.055,0.13,1)
        _Horizon ("Horizon", Color) = (0.25,0.28,0.40,1)
        _Dawn ("First Light", Color) = (0.55,0.29,0.22,1)
        _Ground ("Below Horizon", Color) = (0.09,0.12,0.20,1)
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
            struct v2f { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            fixed4 _Zenith, _Horizon, _Dawn, _Ground;
            v2f vert(appdata_base v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.direction = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float3 sky = lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(d.y), 0.45));
                // The sun remains below the horizon; only its warm glow is visible.
                float east = pow(saturate(dot(d.xz / max(length(d.xz), 0.001), normalize(float2(-1,0.18)))), 5);
                sky = lerp(sky, _Dawn.rgb, east * exp(-abs(d.y) * 7) * 0.8);
                sky = lerp(sky, _Ground.rgb, saturate(-d.y * 5));
                return fixed4(sky, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
