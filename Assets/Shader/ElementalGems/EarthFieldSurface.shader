Shader "ElementalGems/Earth Field Surface"
{
    Properties
    {
        _Fade("Lifetime fade",Range(0,1))=1
        _FieldAge("Eruption age",Float)=5
        _ParticleMode("Flying chip material",Float)=0
        _RockColor("Ochre stone",Color)=(.43,.29,.14,1)
        _GlowColor("Amber fissures",Color)=(1,.58,.12,1)
        _CrystalColor("Mineral accents",Color)=(.56,.38,.63,1)
    }
    SubShader
    {
        Tags {"Queue"="Transparent-12" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off Offset -1,-1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Fade,_FieldAge,_ParticleMode;float4 _RockColor,_GlowColor,_CrystalColor;
            struct A{float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float2 detail:TEXCOORD1;float4 color:COLOR;};
            struct V{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float3 normal:TEXCOORD1;float3 local:TEXCOORD2;float4 color:COLOR;};
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 b=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(b),hash(b+float2(1,0)),f.x),lerp(hash(b+float2(0,1)),hash(b+1),f.x),f.y);}
            V vert(A i)
            {
                V o;o.local=i.vertex.xyz;
                if(i.uv.x>1.5&&_ParticleMode<.5)
                {
                    float rise=smoothstep(0,.28,_FieldAge-i.detail.x)*smoothstep(0,1,_Fade);
                    i.vertex.y=lerp(-.32,i.vertex.y,rise);
                }
                o.vertex=UnityObjectToClipPos(i.vertex);o.normal=UnityObjectToWorldNormal(i.normal);o.uv=i.uv;o.color=i.color;return o;
            }
            float4 frag(V i):SV_Target
            {
                if(i.uv.x>1.5)
                {
                    float crystal=step(3.5,i.uv.x);float2 uv=float2(frac(i.uv.x),i.uv.y);
                    float grain=noise(uv*float2(21,13)),strata=sin(uv.y*43+noise(uv*7)*4)*.5+.5;
                    float light=.38+.62*saturate(dot(normalize(i.normal),normalize(float3(-.4,.85,.35))));
                    float edge=pow(saturate(.5+.5*sin(uv.x*37+uv.y*19+grain*3)),24);
                    float3 stone=_RockColor.rgb*(.7+grain*.5+strata*.15)*light*1.65;
                    stone+=_GlowColor.rgb*edge*.12*(.5+.5*sin(_Time.y*2+uv.y*5));
                    float3 mineral=_CrystalColor.rgb*(.55+light*.9)+float3(.58,.47,.60)*pow(light,7)*.4;
                    return float4(lerp(stone,mineral,crystal)*i.color.rgb,_Fade*i.color.a);
                }
                float2 p=i.uv*2-1;float r=length(p),a=atan2(p.y,p.x+.00001);
                float grain=noise(p*39),n=noise(p*11);
                float boundary=.86+.055*sin(a*5+1)+.035*sin(a*11)+.02*sin(a*19+2);
                float mask=1-smoothstep(boundary-.13,boundary+.015,r+(grain-.5)*.025);
                float2 q=p*7.5+float2(noise(p*8),noise(p*8+13.2))*1.25,b=floor(q),f=frac(q);float first=10,second=10;
                for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                {
                    float2 offset=float2(x,y),cell=b+offset;
                    float2 cellDelta=offset+float2(hash(cell),hash(cell+17.3))-f;
                    float d=dot(cellDelta,cellDelta);if(d<first){second=first;first=d;}else second=min(second,d);
                }
                float crack=1-smoothstep(.012,.085,second-first);
                float bright=1-smoothstep(.005,.026,second-first);
                float center=1-smoothstep(.04,.36,r);
                float pulse=.7+.3*sin(_Time.y*2.5+r*8+n*4);
                float3 dirt=lerp(float3(.10,.065,.036),float3(.34,.23,.12),n*.7+grain*.3);
                float3 color=dirt*(1-crack*.8)+_GlowColor.rgb*bright*(.22+center*.85)*pulse;
                float shock=exp(-abs(r-(_FieldAge*.95))*65)*(1-smoothstep(.25,.8,_FieldAge));
                color+=_GlowColor.rgb*shock*.45;
                return float4(color,mask*_Fade*(.78+shock*.1));
            }
            ENDCG
        }
    }
}


