Shader "ElementalGems/Fire Field Scorch"
{
    Properties
    {
        _Fade("Lifetime fade",Range(0,1))=1
        _Intensity("Fissure glow",Range(0,3))=1.4
    }
    SubShader
    {
        Tags {"Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off Offset -1,-1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Fade,_Intensity;
            struct A {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
            V vert(A i){V o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv*2-1;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
            float4 frag(V i):SV_Target
            {
                float2 p=i.uv;float r=length(p),a=atan2(p.y,p.x);
                float n=noise(p*9+3.7),fine=noise(p*26);
                float edge=.80+.068*sin(a*5+1.3)+.042*sin(a*9-.7)+.025*sin(a*17+2);
                float mask=1-smoothstep(edge-.12,edge+.03+(n-.5)*.075,r);
                float2 q=p*5.5+float2(n-.5,noise(p*8+8)-.5)*.8;
                float2 cell=floor(q),f=frac(q);float near1=8,near2=8;
                // Nine nearby cells form angular fissures; no animated ground sliding.
                for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                {
                    float2 o=float2(x,y);
                    float2 v=o+float2(hash(cell+o),hash(cell+o+19.7))-f;
                    float d=dot(v,v);
                    if(d<near1){near2=near1;near1=d;}else near2=min(near2,d);
                }
                float fissure=1-smoothstep(.018,.072,near2-near1);
                fissure*=smoothstep(.26,.62,n)*(1-smoothstep(.48,.88,r));
                float pulse=.82+.18*sin(_Time.y*3.3+n*17);
                float3 ash=lerp(float3(.038,.027,.023),float3(.13,.07,.036),fine*.55+n*.25);
                float3 lava=lerp(float3(.85,.08,.003),float3(1,.48,.025),fine)*_Intensity*pulse;
                return float4(lerp(ash,lava,fissure),mask*_Fade*.88);
            }
            ENDCG
        }
    }
}
