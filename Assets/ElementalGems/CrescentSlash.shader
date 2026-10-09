Shader "ElementalGems/Broad Crescent Slash"
{
    Properties
    {
        [HDR] _Tint("Element colour", Color)=(0.1,0.8,1,1)
        _Intensity("Glow intensity", Range(0,6))=1.5
        _Age("Normalized lifetime", Range(0,1))=0.25
        _FlowSpeed("Energy flow", Range(0,10))=3
        _Turbulence("Inner-edge turbulence", Range(0,1))=.45
        _Direction("Sweep direction", Float)=1
        _SweepProgress("Animation sweep (negative uses lifetime)", Float)=-1
        _WidthScale("Crescent band width", Range(.4,1.8))=1
    }
    SubShader
    {
        Tags {"Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha One
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Tint;
            float _Intensity, _Age, _FlowSpeed, _Turbulence, _Direction, _WidthScale, _SweepProgress;
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; float2 layer:TEXCOORD1; float4 color:COLOR; };
            struct V { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float2 layer:TEXCOORD1; float4 color:COLOR; };
            V vert(A v) { V o; float r=length(v.vertex.xz); v.vertex.xz*= (v.layer.y+(r-v.layer.y)*_WidthScale)/max(.001,r); o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.layer=v.layer;o.color=v.color;return o; }
            float ridge(float x,float center,float width) { return exp(-pow((x-center)/width,2)); }
            float4 frag(V i):SV_Target
            {
                float u=i.uv.x, v=i.uv.y;
                float phase=u*27-_Age*_FlowSpeed*3+i.layer.y;
                float wave=sin(phase)*sin(phase*.43+v*7);
                float endFade=pow(saturate(sin(u*3.14159265)),.38);
                float reveal=smoothstep(-.09,.05, (_SweepProgress>=0?_SweepProgress:_Age*6)-(_Direction>0?u:1-u));
                float fade=1-smoothstep(_SweepProgress>=0?.8:.25,1,_Age);
                float edge=smoothstep(0,.12,v)*(1-smoothstep(.85,1,v));
                float flowV=v+wave*_Turbulence*.10*(1-v);
                float body=ridge(flowV,.61,.30)*(.67+.12*sin(phase*.61+v*9));
                float core=ridge(flowV,.79,.04)*1.3;
                float bands=ridge(flowV,.51+.08*sin(phase*.35),.047)*.78 + ridge(flowV,.28+.09*sin(phase*.61),.027)*.65;
                float a=(body+core+bands)*edge;
                if(i.layer.x>.5 && i.layer.x<1.5) a=ridge(flowV,.6,.085)*.8 + ridge(flowV,.62,.23)*.22;
                if(i.layer.x>1.5) a=ridge(v,.55,.25)*.13;
                float brightness=_Intensity*(.85+core*.38);
                float3 rgb=lerp(_Tint.rgb, float3(1,1,1), saturate(core*.25))*brightness;
                return float4(rgb, saturate(a)*endFade*fade*reveal*i.color.a*_Tint.a);
            }
            ENDCG
        }
    }
}
