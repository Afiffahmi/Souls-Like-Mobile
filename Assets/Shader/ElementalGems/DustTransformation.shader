Shader "ElementalGems/Dust Transformation"
{
    Properties { _MainTex("Surface",2D)="white"{} _Color("Surface colour",Color)=(1,1,1,1) [HDR]_DustTint("Dust colour",Color)=(.4,.8,1,1) _Progress("Dissolved",Range(0,1))=0 _Seed("Pattern",Float)=0 }
    SubShader
    {
        Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"}
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST,_Color,_DustTint;float _Progress,_Seed;
            struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;float3 normal:NORMAL;};
            struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;};
            V vert(A v){V o;o.p=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o;}
            float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7))+_Seed)*43758.5453);}
            half4 frag(V i):SV_Target
            {
                float grain=hash(floor(i.world*19));
                clip(grain-_Progress*1.02+.01);
                float edge=(1-smoothstep(0,.095,grain-_Progress))*step(.001,_Progress);
                float light=.65+.35*saturate(dot(normalize(i.normal),normalize(float3(.3,.8,.4))));
                float4 surface=tex2D(_MainTex,i.uv)*_Color;
                return half4(lerp(surface.rgb*light,_DustTint.rgb*2,edge),1);
            }
            ENDCG
        }
    }
}
