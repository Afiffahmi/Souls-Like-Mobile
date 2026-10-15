Shader "ElementalGems/Shield Dust Motes"
{
    SubShader
    {
        Tags {"Queue"="Transparent+19" "RenderType"="Transparent"}
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            V vert(A v){V o;o.p=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;
                float d=dot(p,p);
                float halo=pow(saturate(1-d),3);
                float core=pow(saturate(1-d*5),2);
                // The halo belongs to this particle, so it also glows without scene-wide bloom.
                half3 color=i.color.rgb*(2.4+core*3.6);
                return half4(color,(halo*.65+core*.7)*i.color.a);
            }
            ENDCG
        }
    }
}
