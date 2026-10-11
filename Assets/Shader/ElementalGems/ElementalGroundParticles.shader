Shader "ElementalGems/Ground Particles"
{
    Properties
    {
        [HDR] _ElementTint ("Element Colour", Color) = (1,.2,.025,1)
        _Element ("Element", Float) = 1
        _EffectTime ("Effect Time", Float) = 0
        _Fade ("Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+12" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _ElementTint; float _Element, _EffectTime, _Fade;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            float4 frag(Output i):SV_Target
            {
                float2 p=i.uv*2-1;
                float shape=saturate(1-dot(p,p));
                float3 tint=_ElementTint.rgb;
                if (_Element>.5 && _Element<1.5)
                {
                    // Tiny incandescent chips and sparks, never a flame silhouette.
                    float chip=saturate(1-abs(p.x)*1.25-abs(p.y)*.9);
                    shape=chip*chip*(.65+.35*sin(_EffectTime*31+i.uv.x*7));
                    tint=lerp(tint,float3(1,.8,.2),saturate(1-dot(p,p))*.7);
                }
                else if (_Element>1.5 && _Element<2.5) shape=pow(shape,2);
                else if (_Element>2.5 && _Element<3.5) shape=saturate(1-abs(p.x+p.y*.3)*2-abs(p.y)*.8);
                else if (_Element>3.5 && _Element<4.5) shape=saturate(1-abs(p.x)-abs(p.y));
                else if (_Element>4.5 && _Element<5.5)
                {
                    float bolt=abs(p.x-(abs(p.y+.2)-abs(p.y-.2))*.8);
                    shape=saturate(1-bolt*12)*saturate(1-abs(p.y)); tint=lerp(tint,1,.35);
                }
                else if (_Element>5.5 && _Element<6.5) shape=saturate(1-abs(p.y-sin(p.x*3)*.15)*8)*saturate(1-abs(p.x));
                else if (_Element>6.5) shape*=saturate(.5+.5*sin(p.y*9+p.x*6-_EffectTime*3));
                return float4(tint*i.color.rgb*1.7,shape*i.color.a*_Fade);
            }
            ENDCG
        }
    }
}
