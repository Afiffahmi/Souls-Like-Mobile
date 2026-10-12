Shader "Horse/Moving Flame Aura"
{
    Properties { _Wind("Wind ribbon", Float)=0 }
    SubShader
    {
        Tags { "Queue"="Transparent+14" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Wind;
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; float2 seed:TEXCOORD1; float4 color:COLOR; };
            struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float seed:TEXCOORD1; float4 color:COLOR; };
            V vert(A v) { V o; o.position=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.seed=v.seed.x; o.color=v.color; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) { float2 a=floor(p), b=frac(p); b=b*b*(3-2*b); return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y); }
            half4 frag(V i):SV_Target
            {
                float across=abs(i.uv.y*2-1);
                float grain=noise(float2(i.uv.x*7-_Time.y*3+i.seed,i.uv.y*4+i.seed));
                float edge=1-smoothstep(.35+grain*.22,.85+grain*.15,across);
                float core=1-smoothstep(.02,.3,across);
                float flicker=.78+.22*sin(i.uv.x*20-_Time.y*10+i.seed);
                float wisps=smoothstep(.13,.48,grain+(1-i.uv.x)*.38);
                float alpha=edge*wisps*flicker*i.color.a;
                float3 tint=i.color.rgb*(.8+core*.35);
                if (_Wind>.5) { alpha=pow(saturate(1-across),1.3)*i.color.a; tint=i.color.rgb; }
                return half4(tint,alpha);
            }
            ENDCG
        }
    }
}
