Shader "ElementalGems/Ground Field"
{
    Properties
    {
        [HDR] _Tint("Element glow",Color)=(1,.25,.015,1)
        _Fade("Lifetime fade",Range(0,1))=1
        _Style("Element style",Float)=1
        _Intensity("Brightness",Range(0,4))=1.2
        _Speed("Flow speed",Range(0,8))=1.5
    }
    SubShader
    {
        Tags {"Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha One
        Cull Off ZWrite Off Offset -1,-1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Tint;float _Fade,_Style,_Intensity,_Speed;
            struct A {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
            V vert(A i){V o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv*2-1;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 f=frac(p);float2 q=f*f*(3-2*f);float2 a=floor(p);return lerp(lerp(hash(a),hash(a+float2(1,0)),q.x),lerp(hash(a+float2(0,1)),hash(a+1),q.x),q.y);}
            float4 frag(V i):SV_Target
            {
                float r=length(i.uv);clip(1-r);
                float t=_Time.y*_Speed,a=atan2(i.uv.y,i.uv.x);
                float n=noise(i.uv*10+float2(t*.25,-t*.37));
                float ring=exp(-pow((r-.975)/.009,2))*.75;
                float fill=pow(saturate(1-r*r),.6),pattern=0;
                if(_Style<.5) pattern=noise(i.uv*16)*.12;
                else if(_Style<1.5) pattern=pow(n,2)*.55+pow(saturate(sin(n*16+r*18-t*3)),12)*.4;
                else if(_Style<2.5) pattern=pow(saturate(sin(r*40-t*3+n*2)),12)*.5+n*.12;
                else if(_Style<3.5) pattern=pow(saturate(sin(a*7+r*20-t+n*4)),14)*.5+n*.14;
                else if(_Style<4.5) pattern=pow(saturate(1-abs(n-.5)*18),3)*.75;
                else if(_Style<5.5) pattern=pow(saturate(1-abs(sin(a*6+r*18+floor(t*8)*3))*.7-abs(n-.5)*6),6)*1.5;
                else if(_Style<6.5) pattern=pow(saturate(sin(a*3+r*27-t*4)),18)*.5;
                else pattern=pow(saturate(sin(a*4-r*22+t*2+n*3)),7)*.5+n*.16;
                return float4(_Tint.rgb*_Intensity,(ring+pattern*fill)*_Fade*_Tint.a);
            }
            ENDCG
        }
    }
}
