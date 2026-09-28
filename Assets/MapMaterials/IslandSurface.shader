Shader "Map/Island Facets"
{
 Properties { _Color("Tint",Color)=(1,1,1,1) }
 SubShader {
 Tags { "RenderType"="Opaque" }
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows
 struct Input { float4 color:COLOR; };
 fixed4 _Color;
 void surf(Input IN,inout SurfaceOutputStandard o) {o.Albedo=IN.color.rgb*_Color.rgb;o.Smoothness=.04;o.Metallic=0;o.Alpha=1;}
 ENDCG
 }
 FallBack "Diffuse"
}
