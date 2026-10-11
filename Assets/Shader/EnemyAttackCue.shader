Shader "SoulsLike/Enemy Attack Cue"
{
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.color = input.color; output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half soft = 1 - smoothstep(.25, .5, abs(input.uv.y - .5));
                return half4(input.color.rgb * 1.6, input.color.a * soft);
            }
            ENDCG
        }
    }
}
