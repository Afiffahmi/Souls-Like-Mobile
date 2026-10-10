Shader "ElementalGems/Fire Field Smoke"
{
    Properties { _MainTex("Existing smoke",2D)="white"{} _Fade("Field lifetime fade",Range(0,1))=1 }
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
            sampler2D _MainTex;float _Fade;
            struct A {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct V {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            V vert(A i){V o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color;return o;}
            float4 frag(V i):SV_Target
            {
                float2 uv=i.uv;float t=_Time.y;
                uv.x+=sin(uv.y*8-t*1.2)*.035*sin(uv.y*3.14159);
                float4 tex=tex2D(_MainTex,uv);
                float density=smoothstep(.012,.48,tex.a);
                density*=smoothstep(0,.35,saturate(1-length((i.uv-.5)*2)));
                float shade=.65+tex.r*.65;
                return float4(i.color.rgb*shade,density*i.color.a*_Fade);
            }
            ENDCG
        }
    }
}
