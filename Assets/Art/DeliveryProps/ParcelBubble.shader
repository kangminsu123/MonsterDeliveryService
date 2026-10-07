Shader "Delivery/Parcel Bubble"
{
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct Output { float4 vertex:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
            Output vert(Input v)
            {
                Output o; o.vertex = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal); return o;
            }
            fixed4 frag(Output i):SV_Target
            {
                float3 n = normalize(i.normal), view = normalize(_WorldSpaceCameraPos - i.world);
                float edge = 1 - saturate(dot(n, view));
                // A faint additive aura leaves the parcel visible without glass glare or rainbow bands.
                float glow = pow(edge, 5) * .18 + pow(edge, 2) * .025;
                return fixed4(.9, .96, 1, glow);
            }
            ENDCG
        }
    }
}
