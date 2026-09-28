Shader "Map/Reference Asphalt"
{
 Properties { _Color("Asphalt Gray", Color)=(0.25,0.255,0.26,1) }
 SubShader {
 Tags { "RenderType"="Opaque" }
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows
 #pragma target 3.0
 struct Input { float3 worldPos; };
 fixed4 _Color;
 float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
 float noise(float2 p) { float2 i=floor(p),f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y); }
 void surf(Input IN, inout SurfaceOutputStandard o) {
 float2 p=IN.worldPos.xz;
 o.Albedo=_Color.rgb+(noise(p*0.7)-0.5)*0.023+(noise(p*18)-0.5)*0.025;
 o.Metallic=0; o.Smoothness=0.06; o.Alpha=1;
 }
 ENDCG
 }
 FallBack "Diffuse"
}
