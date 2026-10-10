Shader "ElementalGems/Wind Field Surface"
{
    Properties
    {
        _Fade("Field lifetime fade",Range(0,1))=1
        _CoreColor("Deep teal wind",Color)=(.10,.34,.36,1)
        _GustColor("Ivory gusts",Color)=(.94,.96,.81,1)
        _AccentColor("Mint streamers",Color)=(.30,.86,.72,1)
        _DustColor("Warm lifted earth",Color)=(.62,.43,.22,1)
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
            float _Fade;float4 _CoreColor,_GustColor,_AccentColor,_DustColor;
            struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 detail:TEXCOORD1;};
            struct V{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 detail:TEXCOORD1;};
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 f=frac(p),b=floor(p);f=f*f*(3-2*f);return lerp(lerp(hash(b),hash(b+float2(1,0)),f.x),lerp(hash(b+float2(0,1)),hash(b+1),f.x),f.y);}
            V vert(A i)
            {
                V o;
                if(i.uv.x>1.5)
                {
                    float h=i.detail.x,layer=i.detail.y;
                    if(i.uv.x>5.5)
                    {
                        float angle=_Time.y*(1.9+layer*.21),s=sin(angle),c=cos(angle);
                        i.vertex.xz=float2(c*i.vertex.x-s*i.vertex.z,s*i.vertex.x+c*i.vertex.z);
                    }
                    float a=atan2(i.vertex.z,i.vertex.x);
                    float turbulence=1+.065*sin(a*3-h*13+_Time.y*4.5)+.027*sin(a*7+h*19-_Time.y*6);
                    i.vertex.xz*=turbulence;
                    i.vertex.x+=h*h*sin(_Time.y*2.2+h*5)*.035;
                    i.vertex.z+=h*h*cos(_Time.y*2+h*6)*.026;
                    i.vertex.y*=smoothstep(0,1,_Fade);
                }
                o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.detail=i.detail;return o;
            }
            float4 frag(V i):SV_Target
            {
                float t=_Time.y;
                if(i.uv.x>5.5)
                {
                    float u=i.uv.x-6,v=i.uv.y,seed=i.detail.y;
                    float edge=pow(saturate(1-abs(v-.5)*2),1.25);
                    float ends=pow(saturate(sin(u*3.14159)),.65);
                    float tear=.6+.4*noise(float2(u*24-t*3+seed,v*3));
                    float filament=pow(saturate(1-abs(v-.5)*6),3);
                    float3 color=lerp(_AccentColor.rgb,_GustColor.rgb,saturate(seed*.22+filament*.45));
                    return float4(color*(1+filament*.2),edge*ends*tear*_Fade*.8);
                }
                if(i.uv.x>1.5)
                {
                    float inner=step(3.5,i.uv.x),h=i.uv.y;
                    float a=(i.uv.x-2-inner*2)*6.283185;
                    // Periodic angular noise avoids a visible texture seam on either shell.
                    float n=noise(float2(sin(a+t*.7)*3+cos(a+t*.7)*2,h*8-t*2));
                    float fine=noise(float2(cos(a-t*.9)*7+sin(a-t*.9)*4,h*19-t*3));
                    float band=pow(saturate(.5+.5*sin(a*2-h*23+t*9+n*4)),2);
                    float curl=pow(saturate(.5+.5*sin(a*3-h*37+t*14+fine*5)),5);
                    float breakup=smoothstep(.18,.75,n*.62+fine*.38);
                    float body=(.065+band*.30+curl*.12)*(.3+breakup*.7);
                    float alpha=lerp(body,(.16+n*.20+band*.17)*(.5+fine*.5),inner);
                    alpha*=smoothstep(0,.06,h)*(1-smoothstep(.78,1,h))*_Fade;
                    float3 cool=lerp(_CoreColor.rgb,_AccentColor.rgb,band*.55+fine*.18);
                    float3 outer=lerp(cool,_GustColor.rgb,band*.58+curl*.24);
                    float3 color=lerp(outer,cool,inner);
                    color=lerp(_DustColor.rgb,color,smoothstep(.02,.34,h));
                    return float4(color,alpha);
                }
                float2 p=i.uv*2-1;float r=length(p),a=atan2(p.y,p.x+.00001);
                float n=noise(p*13+float2(t*.1,-t*.07)),fine=noise(p*37);
                float mask=1-smoothstep(.12,.38+(fine-.5)*.025,r);
                float stream=pow(saturate(.5+.5*sin(a*4+r*50+t*9+n*4)),12);
                float3 color=lerp(_DustColor.rgb,_AccentColor.rgb,stream*.45);
                return float4(color,mask*_Fade*(.018+stream*.065));
            }
            ENDCG
        }
    }
}

