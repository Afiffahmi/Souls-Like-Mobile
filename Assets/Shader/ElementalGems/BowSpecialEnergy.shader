Shader "ElementalGems/BowSpecialEnergy"
{
    Properties
    {
        [HDR] _Tint("Tint", Color) = (1,1,1,1)
        _ShapeMode("Surface: ribbon / glow / plume / wisp / trail", Float) = 0
        _Intensity("Intensity", Float) = 1
        _Opacity("Opacity", Range(0,1)) = 1
        _EffectTime("Effect Time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BowSpecialEnergyCommon.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            float _ShapeMode, _Intensity, _Opacity, _EffectTime;
            CBUFFER_END
            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                o.uv=i.uv; o.color=i.color*_Tint;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half mask=BowEnergyMask(i.uv,_ShapeMode,_EffectTime);
                return half4(i.color.rgb*_Intensity,saturate(i.color.a*_Opacity*mask));
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "BowSpecialEnergyCommon.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            half4 _Tint;
            float _ShapeMode, _Intensity, _Opacity, _EffectTime;
            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS=UnityObjectToClipPos(i.positionOS);
                o.uv=i.uv; o.color=i.color*_Tint;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half mask=BowEnergyMask(i.uv,_ShapeMode,_EffectTime);
                return half4(i.color.rgb*_Intensity,saturate(i.color.a*_Opacity*mask));
            }
            ENDCG
        }
    }
}
