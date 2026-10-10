Shader "ElementalGems/Nature Field Surface"
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
            struct V{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
            V vert(A i){V o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv*2-1;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 f=frac(p),b=floor(p);f=f*f*(3-2*f);return lerp(lerp(hash(b),hash(b+float2(1,0)),f.x),lerp(hash(b+float2(0,1)),hash(b+1),f.x),f.y);}
            float4 frag(V i):SV_Target
            {
                float2 p=i.uv;float r=length(p),a=atan2(p.y,p.x);
                float n=noise(p*7+3),fine=noise(p*37);
                float edge=.82+.058*sin(a*5+2)+.04*sin(a*9-.8)+.024*sin(a*17+1);
                float mask=1-smoothstep(edge-.075,edge+.025+(fine-.5)*.045,r);
                float moss=smoothstep(.24,.66,n)*(.65+fine*.35);
                float roots=pow(saturate(1-abs(noise(p*12+n)-.5)*15),2);
                float spores=pow(saturate((fine-.71)*4.2),4)*moss;
                float3 color=lerp(float3(.064,.043,.027),float3(.12,.21,.085),moss);
                color=lerp(color,float3(.09,.085,.04),roots*.55);
                color+=float3(.07,.35,.23)*spores*(.78+.22*sin(_Time.y*2+n*16));
                return float4(color,mask*_Fade*.93);
            }
            ENDCG
        }
    }
}
