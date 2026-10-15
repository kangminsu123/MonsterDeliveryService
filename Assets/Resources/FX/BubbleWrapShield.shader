Shader "MonsterDelivery/BubbleWrapShield"
{
    Properties
    {
        _Color ("Shield Tint", Color) = (.35, 1, .8, 1)
        _Alpha ("Visibility", Float) = 1
        _Hit ("Block Flash", Float) = 0
        _BubbleTime ("Bubble Animation Time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _Alpha, _Hit, _BubbleTime;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal); o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float edge = 1 - saturate(dot(normalize(i.normal), normalize(_WorldSpaceCameraPos - i.world)));
                float2 grid = i.uv * float2(20, 10);
                float2 id = floor(grid);
                float seed = frac(sin(dot(id, float2(127.1, 311.7))) * 43758.5453);
                float2 cell = frac(grid) - .5;
                cell += float2(sin(seed * 17), cos(seed * 23)) * .045;
                // Each cell inflates, pops into an expanding ripple, rests, then reforms.
                float phase = frac(_BubbleTime * (.65 + seed * .35) + seed);
                float inflate = smoothstep(.08, .68, phase);
                float pop = smoothstep(.72, .93, phase);
                float radius = lerp(.1, .3, inflate) + pop * .23;
                float alive = smoothstep(0, .12, phase) * (1 - smoothstep(.82, .98, phase));
                float flash = exp(-pow((phase - .8) / .055, 2));
                float distance = length(cell);
                float bubbles = (1 - smoothstep(.02, .055, abs(distance - radius))) * alive;
                float puff = exp(-dot(cell, cell) * 24) * flash;
                float breathe = .9 + .1 * sin(_Time.y * 2.3);
                float alpha = (.018 + pow(edge, 3) * .48 + bubbles * (.055 + edge * .09)) * breathe;
                alpha += puff * .15;
                alpha += _Hit * (.18 + bubbles * .18);
                float3 color = lerp(_Color.rgb, float3(.85, 1, 1), saturate(pow(edge, 5) * .5 + _Hit * .5 + flash * .6));
                return fixed4(color, alpha * _Alpha);
            }
            ENDCG
        }
    }
}
