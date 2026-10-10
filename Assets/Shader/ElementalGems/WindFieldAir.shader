Shader "ElementalGems/Wind Field Air"
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
            struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct V{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            V vert(A i){V o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color;return o;}
            float4 frag(V i):SV_Target
            {
                float lengthFade=sin(i.uv.x*3.14159);
                float stripe=pow(saturate(1-abs(i.uv.y-.5)*2),1.5);
                float streak=.75+.25*sin(i.uv.x*21-_Time.y*8);
                return float4(lerp(float3(.32,.78,.69),float3(1,.96,.78),stripe)*i.color.rgb,lengthFade*stripe*streak*i.color.a*_Fade*.7);
            }
            ENDCG
        }
    }
}

