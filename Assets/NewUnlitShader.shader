Shader "Unlit/NewUnlitShader"
{
    Properties
    {}
    SubShader
    {
        Tags {"RenderType" = "Opaque" "RenderPipeline"="UniversalPipeline"}

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma exclude_renderers gles xbox360 ps3
            #pragma exclude_renderers gles xbox360 ps3
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            struct App
            {
                float4 positionOS : POSITION;
                half3 normal : NORMAL;
            };

            struct v2f
            {
                float4 positionHCS : SV_POSITION;
                half3 normal : TEXCOORD0;
                half3 worldPos : TEXCOORD0;
                half3 viewDir : TEXCOORD0;
            };

            v2f vert(App IN)
            {
                v2f OUT;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normal = TransformObjectToWorldNormal(IN.normal);
                OUT.worldPos = mul(unity_ObjectToWorld, IN.positionOS);
                OUT.viewDir = normalize(GetWorldSpaceViewDir(OUT.worldPos));  // Add a semicolon here
                return OUT;
            }


            half4 frag(v2f IN) : SV_Target
            {
                float dotProduct = dot(IN.normal, IN.viewDir);
                dotProduct = step(0.5, dotProduct);
                half3 fillColor = IN.normal * 0.5 + 0.5;
                half3 finalColor = fillColor;
                return half4(finalColor, 1.0);
            }


            ENDHLSL
        }
    }
}
