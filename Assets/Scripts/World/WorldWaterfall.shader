Shader "Nusantara/Stylized Waterfall"
{
    Properties
    {
        _WaterColor("Turquoise water", Color) = (.04,.69,.78,1)
        _DeepColor("River depths", Color) = (.015,.41,.56,1)
        _HighlightColor("River highlights", Color) = (.326,.623,.61,1)
        _FlowSpeed("Flow speed", Float) = 1.2
        _Foam("Foam amount", Range(0,1)) = .75
        _Width("Width", Float) = 5
        _FallLength("Fall length", Float) = 12
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float4 _WaterColor, _DeepColor, _HighlightColor;
            float _FlowSpeed, _Foam, _Width, _FallLength;
            float _NusantaraWaterEditorTime, _NusantaraWaterEditorPreview;
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; float2 mode:TEXCOORD1; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float2 mode:TEXCOORD1; UNITY_FOG_COORDS(2) };
            float clockTime() { return lerp(_Time.y, _NusantaraWaterEditorTime, _NusantaraWaterEditorPreview); }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            V vert(A v)
            {
                V o; float time=clockTime()*_FlowSpeed;
                if(v.mode.x<.5)
                {
                    v.vertex.z += sin(v.uv.x*36+v.uv.y*15-time*4)*.055*sin(v.uv.y*3.14159);
                    v.vertex.x += sin(v.uv.y*22-time*5)*.025*sin(v.uv.y*3.14159);
                }
                if(v.mode.x>1.5) v.vertex.y += sin(time*5+v.mode.y*23+v.uv.y*6)*.12*v.uv.y;
                o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.mode=v.mode;UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(V i):SV_Target
            {
                float time=clockTime()*_FlowSpeed;
                float3 color; float alpha;
                if(i.mode.x<.5)
                {
                    float2 p=float2(i.uv.x*max(10,_Width*3),i.uv.y*max(2,_FallLength*.23)-time*1.9);
                    p.x += (noise(float2(p.y*.6,i.uv.x*3))-.5)*.8;
                    float broad=noise(p);
                    float fine=noise(p*float2(2.8,1.3)+13);
                    float ribbons=smoothstep(.48,.82,broad)*.7+smoothstep(.7,.9,fine)*.45;
                    float white=smoothstep(.78,.94,noise(p*float2(1.1,2.6)+5))*_Foam;
                    float impact=smoothstep(.72,1,i.uv.y)*(.35+noise(p*2)*.65)*_Foam;
                    color=lerp(_DeepColor.rgb*.6,_WaterColor.rgb*1.05,saturate(i.uv.y*.75+broad*.55));
                    color=lerp(color,_HighlightColor.rgb,saturate(ribbons)*.72);
                    color=lerp(color,lerp(_HighlightColor.rgb,1,.85),saturate(white+impact));
                    float edge=min(i.uv.x,1-i.uv.x);
                    alpha=smoothstep(0,.027+noise(p)*.018,edge)*.96;
                }
                else if(i.mode.x<1.5)
                {
                    float2 p=(i.uv-.5)*2; float radius=length(p);
                    float n=noise(p*7+time*.45);
                    float rings=pow(saturate(sin(radius*24-time*5+n*2)*.5+.5),5);
                    float core=1-smoothstep(.12,.65,radius+n*.15);
                    alpha=(1-smoothstep(.72,1,radius+n*.12))*saturate(core*.9+rings*.55)*_Foam;
                    color=lerp(_WaterColor.rgb,lerp(_HighlightColor.rgb,1,.85),saturate(core+rings*.8));
                }
                else
                {
                    float n=noise(float2(i.uv.x*4+i.mode.y*12,i.uv.y*5-time*2.7));
                    float edge=sin(i.uv.x*3.14159);
                    alpha=edge*(1-smoothstep(.1,1,i.uv.y))*smoothstep(.15,.65,n)*_Foam;
                    color=lerp(_HighlightColor.rgb,1,.8);
                }
                fixed4 result=fixed4(color,alpha); UNITY_APPLY_FOG(i.fogCoord,result);return result;
            }
            ENDCG
        }
    }
    Fallback Off
}
