Shader "Map/Sidewalk Concrete"
{
    Properties
    {
        _Color ("Light Warm Gray", Color) = (0.84,0.82,0.76,1)
        _JointColor ("Soft Joint Gray", Color) = (0.53,0.52,0.48,1)
        _JointWidth ("Joint Width", Range(0.005,0.1)) = 0.025
        _NoiseAmount ("Concrete Grain", Range(0,0.3)) = 0.045
    }
    SubShader
    {
        // Slab joints use each cube's local coordinates; dynamic batching rewrites them.
        Tags { "RenderType"="Opaque" "DisableBatching"="True" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        struct Input { float3 worldPos; };
        fixed4 _Color, _JointColor;
        float _JointWidth, _NoiseAmount;
        float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float noise(float2 p)
        {
            float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 p=IN.worldPos.xz;
            // One slab across the entire sidewalk width; joint at each cube end.
            float localX=mul(unity_WorldToObject,float4(IN.worldPos,1)).x;
            float slabLength=length(mul((float3x3)unity_ObjectToWorld,float3(1,0,0)));
            float d=max(0,0.5-abs(localX))*slabLength;
            // Short overlapping cubes form a smooth corner, not tiny separate bricks.
            if(slabLength<1.8) d=1;
            float aa=max(fwidth(d),0.002);
            float face=smoothstep(_JointWidth*0.5-aa,_JointWidth*0.5+aa,d);
            float grain=(noise(p*3.1)-0.5)*0.5+(noise(p*24)-0.5)*0.3+(noise(p*83)-0.5)*0.2;
            float variation=(noise(p*0.35)-0.5)*0.012;
            float3 concrete=_Color.rgb+grain*_NoiseAmount+variation;
            // Weathering stays in world space so overlapping corner pieces match.
            float mottling=noise(p*0.85)*0.55+noise(p*2.8+17)*0.3+noise(p*8.5)*0.15;
            float stains=smoothstep(0.43,0.76,mottling);
            float dust=smoothstep(0.48,0.8,noise(p*0.26+39))*noise(p*4.7);
            float speckleFade=1-saturate(max(length(ddx(p)),length(ddy(p)))*16);
            float pores=smoothstep(0.64,0.86,noise(p*27))*speckleFade;
            concrete-=0.65*(stains*float3(0.16,0.155,0.14)+dust*float3(0.075,0.08,0.08))+pores*0.14;
            concrete+=(noise(p*12)-0.5)*0.035;
            float bevel=smoothstep(_JointWidth*0.5,_JointWidth*0.5+0.035,d);
            concrete*=lerp(0.97,1,bevel);
            o.Albedo=lerp(_JointColor.rgb,concrete,face);
            o.Metallic=0;
            o.Smoothness=0.08;
            o.Occlusion=lerp(0.95,1,face);
            o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
