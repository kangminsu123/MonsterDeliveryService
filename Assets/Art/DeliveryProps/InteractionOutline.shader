Shader "Delivery/Interaction Outline"
{
    Properties { _Color ("Outline Color", Color) = (1,1,1,1) _Width ("Width in pixels", Float) = 4.5 }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Opaque" }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Back ColorMask 0
            Stencil { Ref 64 ReadMask 64 WriteMask 64 Comp Always Pass Replace }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 vert(float4 vertex:POSITION):SV_POSITION { return UnityObjectToClipPos(vertex); }
            fixed4 frag():SV_Target { return 0; }
            ENDCG
        }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Front
            Stencil { Ref 64 ReadMask 64 Comp NotEqual Pass Keep }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color; float _Width;
            struct Input { float4 vertex:POSITION; float3 normal:NORMAL; };
            float4 vert(Input v):SV_POSITION
            {
                float4 clip = UnityObjectToClipPos(v.vertex);
                float2 direction = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal).xy;
                direction /= max(length(direction), .00001);
                clip.xy += direction * (2 * _Width / _ScreenParams.xy) * clip.w;
                return clip;
            }
            fixed4 frag():SV_Target { return _Color; }
            ENDCG
        }
    }
}
