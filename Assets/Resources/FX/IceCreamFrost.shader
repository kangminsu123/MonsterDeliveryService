Shader "MonsterDelivery/IceCreamFrost"
{
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz; o.normal=UnityObjectToWorldNormal(v.normal); o.uv=v.uv; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float edge=1-saturate(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)));
                float border=1-smoothstep(.015,.04,min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y)));
                float crack=1-smoothstep(.006,.018,abs(frac(i.uv.x*3+i.uv.y*2)-.5));
                float alpha=.16+pow(edge,3)*.35+border*.42+crack*.12;
                return fixed4(lerp(float3(.35,.72,.94),float3(.85,.98,1),border+crack*.3),alpha);
            }
            ENDCG
        }
    }
}
