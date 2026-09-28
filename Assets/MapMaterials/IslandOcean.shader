Shader "Map/Island Ocean"
{
 Properties { _Color("Ocean Blue",Color)=(0.025,0.38,0.64,1) }
 SubShader {
 Tags { "RenderType"="Opaque" }
 CGPROGRAM
 #pragma surface surf Standard vertex:vert fullforwardshadows
 #pragma target 3.0
 struct Input { float3 worldPos; float4 color:COLOR; };
 fixed4 _Color;
 void vert(inout appdata_full v) {float3 w=mul(unity_ObjectToWorld,v.vertex).xyz;v.vertex.y+=sin(w.x*.19+_Time.y*.8)*.08+cos(w.z*.17+_Time.y*.65)*.06;}
 void surf(Input IN,inout SurfaceOutputStandard o) {
 float wave=sin(IN.worldPos.x*.28+IN.worldPos.z*.19+sin(IN.worldPos.z*.13)*2+_Time.y*.55)*.003;
 o.Albedo=(_Color.rgb+wave)*IN.color.rgb;o.Emission=o.Albedo*.16;o.Metallic=0;o.Smoothness=.28;o.Alpha=1;
 }
 ENDCG
 }
 FallBack "Diffuse"
}
