Shader "ElementalGems/Lightning Field Bolt"
{
    Properties { _Fade("Field lifetime fade",Range(0,1))=1 _Glow("Brightness",Range(0,4))=1.7 }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha One
        Cull Back ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Fade,_Glow;
            struct A{float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct V{float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;float2 uv:TEXCOORD2;fixed4 color:COLOR;};
            V vert(A i)
            {
                V o;float phase=i.color.r*21+i.color.b*7;
                float tick=floor(_Time.y*13+phase);
                float h=i.uv.y;
                i.vertex.x+=sin(tick*2.3+h*25+phase)*.025*sin(h*3.14159);
                i.vertex.z+=cos(tick*1.7+h*21+phase)*.025*sin(h*3.14159);
                o.vertex=UnityObjectToClipPos(i.vertex);o.world=mul(unity_ObjectToWorld,i.vertex).xyz;o.normal=UnityObjectToWorldNormal(i.normal);o.uv=i.uv;o.color=i.color;return o;
            }
            float4 frag(V i):SV_Target
            {
                float facing=abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)));
                float3 color=lerp(float3(.14,.31,1),float3(.78,.96,1),smoothstep(.12,.75,facing));
                float flutter=.83+.17*sin(_Time.y*27+i.color.r*43+i.uv.y*12);
                return float4(color*_Glow, i.color.a*_Fade*flutter);
            }
            ENDCG
        }
    }
}
