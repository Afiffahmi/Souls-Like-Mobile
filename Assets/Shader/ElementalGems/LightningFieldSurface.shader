Shader "ElementalGems/Lightning Field Surface"
{
    Properties { _Fade("Field lifetime fade",Range(0,1))=1 _Intensity("Energy intensity",Range(0,3))=1.2 }
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
            struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct V{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
            V vert(A i){V o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv*2-1;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 f=frac(p),b=floor(p);f=f*f*(3-2*f);return lerp(lerp(hash(b),hash(b+float2(1,0)),f.x),lerp(hash(b+float2(0,1)),hash(b+1),f.x),f.y);}
            float4 frag(V i):SV_Target
            {
                float2 p=i.uv;float r=length(p),a=atan2(p.y,p.x),t=_Time.y;
                float n=noise(p*10+7),fine=noise(p*24-3);
                float edge=.83+.057*sin(a*5+.8)+.039*sin(a*11+2)+.025*sin(a*19);
                float mask=1-smoothstep(edge-.045,edge+.03+(fine-.5)*.04,r);
                float inner=1-smoothstep(.59,.81,r);
                float flow=noise(p*6+float2(t*.19,-t*.12));
                float spiral=a*5+r*39-t*3.8+n*3.1;
                float filaments=pow(saturate(.5+.5*sin(spiral)),16)*smoothstep(.25,.65,flow);
                float undertow=pow(saturate(.5+.5*sin(a*3+r*27+t*2.3+n*4)),8);
                float2 q=p*6+float2(n-.5,fine-.5)*.3;
                float2 cell=floor(q),f=frac(q);float nearest=8,second=8;
                for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                {
                    float2 offset=float2(x,y);
                    float2 d=offset+float2(hash(cell+offset),hash(cell+offset+17.3))-f;
                    float distance=dot(d,d);
                    if(distance<nearest){second=nearest;nearest=distance;}else second=min(second,distance);
                }
                float cracks=(1-smoothstep(.009,.041,second-nearest))*smoothstep(.4,.72,r);
                float forks=pow(saturate(1-abs(fine-.49)*30),3)*smoothstep(.64,.8,r)*.3;
                float core=exp(-r*r*32)*(.55+.12*sin(t*3));
                float3 color=lerp(float3(.036,.025,.047),float3(.035,.052,.16),inner);
                color+=inner*(float3(.035,.16,.36)*flow+float3(.45,.055,.85)*undertow*.6);
                color+=float3(.08,.78,1.1)*filaments*inner*_Intensity;
                color+=float3(.02,.8,.95)*core;
                color+=float3(.24,.48,1)*(cracks+forks)*(.55+.18*sin(t*5+n*13))*_Intensity;
                return float4(color,mask*_Fade*.9);
            }
            ENDCG
        }
    }
}
