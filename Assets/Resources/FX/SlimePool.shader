Shader "MonsterDelivery/SlimePool"
{
    Properties { _Opacity ("Opacity", Range(0,1)) = 1 }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Opacity;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            { v2f o; o.position = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float r = length(p), a = atan2(p.y, p.x);
                float edge = r + .012 * sin(a * 7 + _Time.y * .7);
                float mottling = sin(p.x * 12 + sin(p.y * 10 + _Time.y)) * sin(p.y * 9 - _Time.y * .6);
                float ripple = pow(saturate(sin(r * 30 - _Time.y * 2.5 + sin(a * 3) * smoothstep(.2,.7,r))), 18);
                float rim = smoothstep(.87,.94,edge) * (1 - smoothstep(.96,1,edge));
                float3 color = lerp(float3(.08,.27,.008), float3(.25,.53,.025), .6 + mottling * .17);
                color += ripple * float3(.07,.13,.015) + rim * float3(.3,.38,.055);
                return fixed4(color, (1 - smoothstep(.94,1,edge)) * .94 * _Opacity);
            }
            ENDCG
        }
    }
}
