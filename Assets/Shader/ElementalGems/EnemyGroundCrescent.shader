Shader "ElementalGems/Enemy Ground Crescent"
{
    Properties { [HDR] _Tint("Gem colour", Color)=(1,.3,.05,1) _Intensity("Brightness", Float)=2.5 }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off ZTest LEqual Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Tint; float _Intensity;
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(A i) { V o; o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;return o; }
            half4 frag(V i):SV_Target
            {
                float edge=exp(-pow((i.uv.y-.88)/.08,2));
                float flow=sin(i.uv.x*31-_Time.y*12)*sin(i.uv.y*19+i.uv.x*11);
                float alpha=smoothstep(0,.35+flow*.1,i.uv.y)*(1-smoothstep(.94,1,i.uv.y));
                return half4(lerp(_Tint.rgb,float3(1,1,1),edge*.4)*_Intensity,alpha*_Tint.a);
            }
            ENDCG
        }
    }
}
