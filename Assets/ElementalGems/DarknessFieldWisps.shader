Shader "ElementalGems/Darkness Field Wisps"
{
    Properties { _Fade("Field lifetime fade",Range(0,1))=1 }
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
            float _Fade;
            struct A{float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct V{float4 vertex:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;fixed4 color:COLOR;};
            V vert(A i)
            {
                V o;float phase=i.color.r*43+i.color.g*13,t=_Time.y;
                i.vertex.x+=sin(t*1.8+i.uv.y*7+phase)*i.uv.y*i.uv.y*.035;
                i.vertex.z+=cos(t*1.3+i.uv.y*8+phase)*i.uv.y*i.uv.y*.025;
                o.vertex=UnityObjectToClipPos(i.vertex);o.world=mul(unity_ObjectToWorld,i.vertex).xyz;o.normal=UnityObjectToWorldNormal(i.normal);o.uv=i.uv;o.color=i.color;return o;
            }
            float4 frag(V i):SV_Target
            {
                float facing=abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)));
                float edge=pow(1-facing,2);
                float threads=pow(saturate(.5+.5*sin(i.uv.x*57+i.uv.y*15+sin(i.uv.y*13-_Time.y*2)*2)),10);
                float3 color=float3(.035,.032,.041)+float3(.24,.21,.27)*edge+float3(.11,.105,.125)*threads;
                float tip=1-smoothstep(.8,1,i.uv.y);
                float alpha=smoothstep(0,.1,i.uv.y)*tip*i.color.a*_Fade;
                return float4(color,alpha);
            }
            ENDCG
        }
    }
}
