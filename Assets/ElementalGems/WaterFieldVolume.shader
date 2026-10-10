Shader "ElementalGems/Water Field Volume"
{
    Properties { _Fade("Field lifetime fade",Range(0,1))=1 _Droplet("Droplet mode",Float)=0 }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Fade,_Droplet;
            struct A{float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct V{float4 vertex:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;fixed4 color:COLOR;};
            V vert(A i)
            {
                V o;float t=_Time.y*2+i.color.r*13;
                i.vertex.y+=(1-_Droplet)*sin(i.uv.x*15-t)*i.uv.y*.012;
                o.vertex=UnityObjectToClipPos(i.vertex);o.world=mul(unity_ObjectToWorld,i.vertex).xyz;o.normal=UnityObjectToWorldNormal(i.normal);o.uv=i.uv;o.color=i.color;return o;
            }
            float4 frag(V i):SV_Target
            {
                float facing=abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)));
                float edge=pow(1-facing,2);
                float streak=pow(saturate(.5+.5*sin(i.uv.x*36+i.uv.y*9-_Time.y*3)),10);
                float foam=smoothstep(.7,.97,i.uv.y)*(.65+.35*sin(i.uv.x*29));
                float3 color=lerp(float3(.025,.19,.29),float3(.22,.63,.73),edge*.55+i.uv.y*.3);
                color+=float3(.07,.16,.18)*streak;
                color=lerp(color,float3(.76,.96,1),_Droplet>0.5?pow(facing,9)*.8:foam*.8);
                float ends=_Droplet>.5?1:smoothstep(0,.12,i.uv.x)*(1-smoothstep(.88,1,i.uv.x))*smoothstep(0,.12,i.uv.y);
                return float4(color,i.color.a*_Fade*ends*(.7+edge*.25));
            }
            ENDCG
        }
    }
}
