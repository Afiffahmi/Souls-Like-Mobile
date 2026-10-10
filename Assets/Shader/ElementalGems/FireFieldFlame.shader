Shader "ElementalGems/Fire Field Flame"
{
    Properties
    {
        _Glow("Exposure",Range(0,3))=1.4
        _Speed("Rising turbulence",Range(0,5))=1.6
        _Core("Hot core",Range(0,1))=0
        _Fade("Field lifetime fade",Range(0,1))=1
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Glow,_Speed,_Core,_Fade;
            struct A {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct V {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;float3 normal:TEXCOORD1;float3 world:TEXCOORD2;float phase:TEXCOORD3;};
            V vert(A i)
            {
                V o;float h=i.uv.y;float phase=i.color.r*31+i.color.g*17;
                float t=_Time.y*_Speed+phase;
                // Bend upper rings in two axes without facing geometry toward the camera.
                i.vertex.x+=h*h*(sin(t*2.1-h*5)*.15+sin(t*3.7-h*9)*.055);
                i.vertex.z+=h*h*(cos(t*1.8-h*6)*.13+sin(t*2.9+h*7)*.04);
                i.vertex.y+=sin(t*2.6-h*4)*h*.085;
                o.vertex=UnityObjectToClipPos(i.vertex);o.world=mul(unity_ObjectToWorld,i.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(i.normal);o.uv=i.uv;o.color=i.color;o.phase=phase;return o;
            }
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
            float4 frag(V i):SV_Target
            {
                float h=i.uv.y,t=_Time.y*_Speed+i.phase;
                float a=i.uv.x*6.283185;
                float n=noise(float2(sin(a)*2.5+i.phase,h*5-t*1.8+cos(a)));
                float wisps=.5+.5*sin(a*3+h*9-t*4+n*3);
                float facing=saturate(abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world))));
                float heat=saturate(1-h*.86+(n-.5)*.45+_Core*.22);
                float3 color=lerp(float3(.65,.045,.002),float3(1,.34,.014),smoothstep(.1,.62,heat));
                color=lerp(color,float3(1,.78,.21),smoothstep(.56,.95,heat));
                color=lerp(color,float3(1,.94,.56),_Core*smoothstep(.65,1,heat));
                float alpha=smoothstep(.015,.32,facing)*smoothstep(0,.09,h);
                alpha*=1-smoothstep(.58+wisps*.26,1,h);
                alpha*=lerp(.7,1,n)*i.color.a*_Fade;
                return float4(color*_Glow,alpha);
            }
            ENDCG
        }
    }
}
