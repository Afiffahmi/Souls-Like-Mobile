Shader "ElementalGems/Enemy Eye Energy"
{
    Properties
    {
        [HDR] _Tint("Element color", Color) = (1,.2,.03,1)
        _Intensity("Brightness", Float) = 4
        _Shape("Shape: ribbon or core", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZTest LEqual
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Intensity, _Shape;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.color=v.color;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float alpha, hot;
                if (_Shape > .5)
                {
                    float r2=dot(i.uv*2-1,i.uv*2-1);
                    alpha=exp(-r2*5)*smoothstep(1,.25,r2);
                    hot=exp(-r2*65);
                }
                else
                {
                    float crossSection=abs(i.uv.y*2-1);
                    alpha=exp(-crossSection*crossSection*6)*smoothstep(1,.82,i.uv.x);
                    hot=exp(-crossSection*crossSection*105)*pow(saturate(1-i.uv.x),1.6);
                }
                float3 tint=_Tint.rgb*i.color.rgb;
                float3 color=lerp(tint,float3(1,.97,.82),hot*.97)*_Intensity;
                return half4(color,alpha*_Tint.a*i.color.a);
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZTest LEqual
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float4 _Tint;
            float _Intensity, _Shape;
            struct Attributes { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float alpha, hot;
                if (_Shape > .5)
                {
                    float r2=dot(i.uv*2-1,i.uv*2-1);
                    alpha=exp(-r2*5)*smoothstep(1,.25,r2);
                    hot=exp(-r2*65);
                }
                else
                {
                    float crossSection=abs(i.uv.y*2-1);
                    alpha=exp(-crossSection*crossSection*6)*smoothstep(1,.82,i.uv.x);
                    hot=exp(-crossSection*crossSection*105)*pow(saturate(1-i.uv.x),1.6);
                }
                float3 tint=_Tint.rgb*i.color.rgb;
                float3 color=lerp(tint,float3(1,.97,.82),hot*.97)*_Intensity;
                return half4(color,alpha*_Tint.a*i.color.a);
            }
            ENDCG
        }
    }
}
