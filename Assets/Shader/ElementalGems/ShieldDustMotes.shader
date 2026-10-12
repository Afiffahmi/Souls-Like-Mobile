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
            half4 frag(V i):SV_Target{float2 p=i.uv*2-1;float d=dot(p,p);float alpha=pow(saturate(1-d),2);return half4(i.color.rgb*(1.2+pow(saturate(1-d*4),3)),alpha*i.color.a);}
            ENDCG
        }
    }
}
