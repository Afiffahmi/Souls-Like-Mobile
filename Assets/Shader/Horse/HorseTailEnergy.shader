Shader "Horse/Layered Tail Energy"
{
    Properties { _Intensity("Glow intensity",Range(.1,4))=1 _NeonEmission("Neon Emission",Range(1,4))=1.7 }
    SubShader
    {
        Tags { "Queue"="Transparent+15" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Intensity, _NeonEmission;
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            V vert(A v) { V o; o.position=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            half4 frag(V i):SV_Target
            {
                float edge=pow(saturate(1-abs(i.uv.y*2-1)),1.6);
                float shimmer=.82+.18*sin(i.uv.x*36-_Time.y*6);
                return half4(i.color.rgb*_Intensity*shimmer*_NeonEmission,i.color.a*edge);
            }
            ENDCG
        }
    }
}
