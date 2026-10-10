Shader "ElementalGems/Darkness Field Surface"
{
    Properties { _Fade("Field lifetime fade",Range(0,1))=1 }
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
            float _Fade;
            struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct V{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float crest:TEXCOORD1;};
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 f=frac(p),b=floor(p);f=f*f*(3-2*f);return lerp(lerp(hash(b),hash(b+float2(1,0)),f.x),lerp(hash(b+float2(0,1)),hash(b+1),f.x),f.y);}
            V vert(A i)
            {
                V o;float2 p=i.uv*2-1;float r=length(p),a=atan2(p.y,p.x+.00001),t=_Time.y;
                float crest=exp(-pow((r-(.52+.035*sin(a*5-t*.8)))/.085,2));
                float peak=.45+.55*pow(.5+.5*sin(a*4-t),3);
                i.vertex.y+=(crest*peak*.23+exp(-r*r*75)*.13)*(1-smoothstep(.74,.92,r))*_Fade;
                o.vertex=UnityObjectToClipPos(i.vertex);o.uv=p;o.crest=crest*peak;return o;
            }
            float4 frag(V i):SV_Target
            {
                float2 p=i.uv;float r=length(p),a=atan2(p.y,p.x+.00001),t=_Time.y;
                float n=noise(p*13+float2(t*.045,-t*.065)),fine=noise(p*39);
                float edge=.86+.045*sin(a*5+2)+.032*sin(a*13+.6)+.018*sin(a*23);
                float mask=1-smoothstep(edge-.065,edge+.02+(fine-.5)*.03,r);
                float stream=pow(saturate(.5+.5*sin(a*5+r*56+t*2.2+n*4)),12);
                float grain=pow(saturate(.5+.5*sin(a*8+r*105+t*3.1+n*6)),14);
                float center=1-smoothstep(.13,.2,r);
                float rim=exp(-pow((r-.195-.006*sin(a*5-t))/.012,2));
                float3 shadow=float3(.015,.014,.021)+float3(.10,.092,.115)*stream+float3(.035,.036,.046)*grain;
                shadow+=float3(.16,.14,.18)*i.crest*(.5+n*.5);
                shadow=lerp(shadow,float3(.001,.001,.003),center);
                shadow+=float3(.24,.19,.22)*rim*(.7+.15*sin(t*1.5));
                return float4(shadow,mask*_Fade*.98);
            }
            ENDCG
        }
    }
}
