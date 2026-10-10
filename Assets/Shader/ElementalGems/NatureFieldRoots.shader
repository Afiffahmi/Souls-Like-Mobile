Shader "ElementalGems/Nature Field Roots"
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
            struct V{float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;float2 uv:TEXCOORD1;fixed4 color:COLOR;};
            V vert(A i)
            {
                V o;float weight=i.uv.x>1.5?.6:i.uv.y*i.uv.y;
                float phase=i.color.r*19+i.color.g*7;
                i.vertex.x+=sin(_Time.y*1.5+phase+i.uv.y*5)*weight*.022;
                i.vertex.z+=cos(_Time.y*1.2+phase+i.uv.y*7)*weight*.017;
                o.vertex=UnityObjectToClipPos(i.vertex);o.normal=UnityObjectToWorldNormal(i.normal);o.uv=i.uv;o.color=i.color;return o;
            }
            float4 frag(V i):SV_Target
            {
                float3 normal=normalize(i.normal);
                float light=.75+.25*saturate(dot(normal,normalize(float3(-.35,.85,-.4))));
                float3 color;
                if(i.uv.x>1.5)
                {
                    float u=i.uv.x-2;
                    float vein=1-smoothstep(.015,.07,abs(u-.5));
                    color=lerp(float3(.055,.24,.09),float3(.2,.5,.2),i.uv.y*.6+vein*.35);
                    color+=float3(.03,.15,.09)*vein;
                }
                else
                {
                    float grain=.5+.5*sin(i.uv.x*32+i.uv.y*9+sin(i.uv.y*23)*1.5);
                    float moss=smoothstep(.22,.75,.5+.5*sin(i.uv.x*18-i.uv.y*9));
                    color=lerp(float3(.28,.19,.105),float3(.65,.5,.28),grain*.65+.2);
                    color=lerp(color,float3(.17,.43,.19),moss*.45);
                    float vein=pow(saturate(.5+.5*sin(i.uv.x*26+i.uv.y*8)),24)*moss;
                    color+=float3(.08,.48,.34)*vein*(.8+.2*sin(_Time.y*1.5+i.uv.y*8));
                }
                return float4(color*light*i.color.rgb,i.color.a*_Fade);
            }
            ENDCG
        }
    }
}
