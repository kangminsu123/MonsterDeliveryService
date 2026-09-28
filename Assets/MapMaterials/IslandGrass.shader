Shader "Map/Low Poly Grass"
{
 Properties {
  _Color("Sunlit Green",Color)=(0.48,0.65,0.23,1)
  _Shade("Meadow Green",Color)=(0.39,0.56,0.18,1)
  _FacetSize("Facet Size",Range(1,12))=4.5
 }
 SubShader {
 Tags { "RenderType"="Opaque" }
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows
 #pragma target 3.0
 struct Input { float3 worldPos; };
 fixed4 _Color,_Shade;float _FacetSize;
 float hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p) {float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 void surf(Input IN,inout SurfaceOutputStandard o) {
  // Equilateral triangular facets in world space, with a single color per face.
  float2 p=IN.worldPos.xz/max(_FacetSize,.1);
  float2 lattice=float2(p.x-p.y*.5773503,p.y*1.1547005);
  float2 cell=floor(lattice),f=frac(lattice);
  float upper=step(1,f.x+f.y);
  float2 center=cell+(1+upper)/3;
  float broad=noise(center*.19+13);
  float tone=saturate(.12+broad*.55+hash(cell+upper*37.2)*.32);
  o.Albedo=lerp(_Shade.rgb,_Color.rgb,tone);
  o.Metallic=0;o.Smoothness=.02;o.Alpha=1;
 }
 ENDCG
 }
 FallBack "Diffuse"
}
