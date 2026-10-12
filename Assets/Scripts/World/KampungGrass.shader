Shader "Nusantara/Kampung Living Grass"
{
    Properties
    {
        _Wind("Wind strength",Range(0,.3))=.055
        _FadeStart("Fade start metres",Float)=42
        _FadeEnd("Fade end metres",Float)=76
        _Density("Quality density",Range(.1,1))=1
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "Queue"="Geometry+5"}
        Cull Off
        Pass
        {
            Tags {"LightMode"="ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            float _Wind,_FadeStart,_FadeEnd,_Density;
            struct A {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct V {float4 pos:SV_POSITION;float3 world:TEXCOORD0;fixed3 light:TEXCOORD1;fixed4 color:COLOR;UNITY_FOG_COORDS(2)};
            V vert(A i)
            {
                V o;float3 world=mul(unity_ObjectToWorld,i.vertex).xyz;
                float wave=sin(world.x*.6+world.z*.4+_Time.y*1.6)+.35*sin(world.z*1.3-_Time.y*2.1);
                world.xz+=float2(1,.6)*wave*_Wind*i.uv.y*i.uv.y;
                o.pos=UnityWorldToClipPos(world);o.world=world;o.color=i.color;
                float3 n=UnityObjectToWorldNormal(i.normal);
                o.light=ShadeSH9(float4(n,1)) + _LightColor0.rgb*(.3+.7*saturate(dot(n,normalize(_WorldSpaceLightPos0.xyz))));
                UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(V i):SV_Target
            {
                float distanceToCamera=distance(i.world,_WorldSpaceCameraPos);
                float fade=saturate((_FadeEnd-distanceToCamera)/max(1,_FadeEnd-_FadeStart));
                float threshold=frac(sin(dot(floor(i.world.xz*9),float2(127.1,311.7)))*43758.5453);
                clip(fade*_Density-threshold);
                fixed4 c=fixed4(i.color.rgb*i.light,1);UNITY_APPLY_FOG(i.fogCoord,c);return c;
            }
            ENDCG
        }
    }
}
