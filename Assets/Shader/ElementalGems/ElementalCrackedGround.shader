Shader "ElementalGems/Cracked Ground"
{
    Properties
    {
        _Glow ("Crack Glow", Range(0, 5)) = 2.2
        _Opacity ("Crack Opacity", Range(0,1)) = .65
        _CoreOpacity ("Core Opacity", Range(0,1)) = .85
        [HDR] _ElementTint ("Element Colour", Color) = (1,.2,.025,1)
        _Element ("Element", Float) = 1
        _EffectTime ("Effect Time", Float) = 0
        _Fade ("Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float _Glow, _Opacity, _CoreOpacity, _Element, _EffectTime, _Fade;
            float4 _ElementTint;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float2 local:TEXCOORD1; float4 color:COLOR; };
            Output vert(Input v)
            { Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.local=v.vertex.xz; o.uv=v.uv; o.color=v.color; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 a=floor(p), b=frac(p); b=b*b*(3-2*b);
                return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);
            }
            float fbm(float2 p) { return noise(p)*.6+noise(p*2.1)*.28+noise(p*4.3)*.12; }
            float4 frag(Output i):SV_Target
            {
                float2 p=i.local; float t=_EffectTime;
                float pool=step(.5,i.color.r);
                float across=abs(i.uv.x*2-1);
                float edge=1-smoothstep(.62,1,across);
                float seam=1-smoothstep(.1,.64,across);
                float coverage=lerp(edge,1-smoothstep(.76,1,i.uv.x),pool);
                float3 dark=float3(.025,.02,.018);
                float3 energy=_ElementTint.rgb;
                float intensity=.2, crust=0;
                if (_Element>.5 && _Element<1.5)
                {
                    // Slow flowing magma broken into dark cooling islands, with hotter fine channels.
                    float2 flow=p*4+float2(t*.12,-t*.09);
                    flow+=float2(fbm(p*3+t*.07),fbm(p*3-t*.08))*1.4;
                    float molten=fbm(flow);
                    float vein=1-smoothstep(.025,.105,abs(molten-.5));
                    crust=smoothstep(.57,.72,molten)*pool;
                    intensity=.5+.55*vein+.2*sin(t*2+molten*13);
                    energy=lerp(_ElementTint.rgb,float3(1,.67,.055),vein*.8);
                    energy=lerp(energy,float3(1,.9,.38),pow(vein,6)*.3);
                    dark=float3(.035,.018,.008);
                }
                else if (_Element>1.5 && _Element<2.5)
                {
                    float caustic=pow(saturate(.5+.5*sin(p.x*12+t*2+sin(p.y*8-t*1.7))),9);
                    float ripple=pow(saturate(.5+.5*sin(length(p)*19-t*5)),12);
                    intensity=.2+.6*caustic+.45*ripple;
                    energy=lerp(energy,float3(.5,.9,1),caustic*.6); dark=float3(.01,.04,.055);
                }
                else if (_Element>2.5 && _Element<3.5)
                {
                    float vein=pow(saturate(.5+.5*sin(p.x*14+sin(p.y*10)*2)),10);
                    intensity=.18+vein*(.4+.2*sin(t*2+p.y*4));
                    crust=smoothstep(.45,.75,fbm(p*8))*pool; dark=float3(.012,.04,.012);
                }
                else if (_Element>3.5 && _Element<4.5)
                {
                    float crystal=step(.7,noise(floor(p*15)));
                    intensity=.09+crystal*.35; crust=.45*pool; dark=float3(.055,.033,.018);
                }
                else if (_Element>4.5 && _Element<5.5)
                {
                    float charge=pow(saturate(.5+.5*sin(p.x*18+p.y*11-t*21+noise(p*9)*5)),22);
                    float flash=step(.82,noise(p*3+floor(t*16)));
                    intensity=.08+charge*1.5+flash*.5;
                    energy=lerp(energy,float3(.8,.9,1),saturate(charge+flash)*.65); dark=float3(.012,.012,.03);
                }
                else if (_Element>5.5 && _Element<6.5)
                {
                    float sweep=pow(saturate(.5+.5*sin(atan2(p.y,p.x)*4+length(p)*11-t*6)),12);
                    intensity=.06+sweep*.65; energy=lerp(energy,float3(.8,1,.95),sweep*.5);
                    coverage*=.55+.35*sweep;
                }
                else if (_Element>6.5)
                {
                    float vortex=pow(saturate(.5+.5*sin(atan2(p.y,p.x)*5+length(p)*14+t*3)),6);
                    intensity=.08+vortex*.7; dark=float3(.009,.003,.015);
                    crust=smoothstep(.5,.72,fbm(p*4+t*.1))*pool;
                }
                else { energy=float3(.2,.15,.09); intensity=.04; }
                float fill=lerp(seam,1,pool)*(1-crust*.92);
                float3 colour=lerp(dark,energy*_Glow*max(.03,intensity),fill);
                float alpha=coverage*lerp(_Opacity,_CoreOpacity,pool)*i.color.a*_Fade;
                return float4(colour,alpha);
            }
            ENDCG
        }
    }
}
