Shader "MonsterDelivery/FountainInk"
{
    Properties
    {
        _MainTex ("Ink silhouette", 2D) = "white" {}
        _Color ("Ink tint", Color) = (1,1,1,1)
        _EdgeColor ("Ink edge", Color) = (.58,.19,.95,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color, _EdgeColor;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert(appdata v)
            {
                v2f o; o.position = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color * _Color; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float alpha = tex2D(_MainTex, i.uv).a;
                float rim = smoothstep(.03,.45,alpha) * (1 - smoothstep(.5,.85,alpha));
                fixed4 color = i.color;
                color.rgb = lerp(color.rgb, _EdgeColor.rgb, rim * .85);
                color.a *= alpha;
                return color;
            }
            ENDCG
        }
    }
}