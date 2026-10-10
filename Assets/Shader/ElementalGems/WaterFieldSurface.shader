Shader "ElementalGems/Water Field Surface"
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
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Fade;
            struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct V{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float crest:TEXCOORD2;};
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 f=frac(p),b=floor(p);f=f*f*(3-2*f);return lerp(lerp(hash(b),hash(b+float2(1,0)),f.x),lerp(hash(b+float2(0,1)),hash(b+1),f.x),f.y);}
            V vert(A i)
            {
                V o;float2 p=i.uv*2-1;float r=length(p),a=atan2(p.y,p.x+.00001),t=_Time.y;
                float waveRadius=.53+.045*sin(a*3-t*1.15)+.025*sin(a*7+t*.8);
                float crest=exp(-pow((r-waveRadius)/.085,2));
                float broken=.4+.6*pow(.5+.5*sin(a*3-t*1.6),2);
                float ripple=sin(r*37+a*2-t*3)*.008;
                i.vertex.y+=(crest*(.025+broken*.05)+(.018+ripple)*smoothstep(.15,.6,r))*(1-smoothstep(.78,.95,r))*_Fade;
                o.vertex=UnityObjectToClipPos(i.vertex);o.world=mul(unity_ObjectToWorld,i.vertex).xyz;o.uv=p;o.crest=crest*broken;return o;
            }
            float4 frag(V i):SV_Target
            {
                float2 p=i.uv;float r=length(p),a=atan2(p.y,p.x+.00001),t=_Time.y;
                float n=noise(p*12+float2(t*.11,-t*.13)),fine=noise(p*39);
                float edge=.87+.043*sin(a*5+.7)+.03*sin(a*11+1.7)+.014*sin(a*21);
                float mask=1-smoothstep(edge-.055,edge+.02+(fine-.5)*.025,r);
                float flow=a*4+r*43-t*3.5+n*4.5;
                float ribbons=pow(saturate(.5+.5*sin(flow)),10);
                float small=pow(saturate(.5+.5*sin(a*7+r*74-t*5+n*3)),18);
                float3 normal=normalize(cross(ddy(i.world),ddx(i.world)));normal*=normal.y<0?-1:1;
                float light=.72+.28*saturate(dot(normal,normalize(float3(-.4,1,-.5))));
                float3 water=lerp(float3(.012,.045,.11),float3(.025,.27,.39),smoothstep(.12,.68,r));
                water+=float3(.045,.14,.18)*ribbons+float3(.06,.14,.17)*small;
                float foam=smoothstep(.38,.8,i.crest)*(.5+.5*n)+small*smoothstep(.64,.86,r)*.38;
                foam*=.5+.5*fine;
                water=lerp(water,float3(.62,.91,.95),saturate(foam));
                float fresnel=pow(1-saturate(abs(dot(normal,normalize(_WorldSpaceCameraPos-i.world)))),3);
                water+=float3(.08,.18,.2)*fresnel;
                return float4(water*light,mask*_Fade*.94);
            }
            ENDCG
        }
    }
}
