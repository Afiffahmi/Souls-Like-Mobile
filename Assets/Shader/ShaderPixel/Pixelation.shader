Shader "Hidden/NewImageEffectShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PixelSize ("Pixel Size (Source Pixels)", Range(1, 8)) = 3
        _DetailPreservation ("Detail Preservation", Range(0, 1)) = 0.25
        _CelStrength ("Soft Cel Shading", Range(0, 1)) = 0.45
        _ShadeLevels ("Lighting Bands", Range(3, 12)) = 6
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _PixelSize;
            half _DetailPreservation;
            half _CelStrength;
            float _ShadeLevels;

            half4 frag (v2f_img i) : SV_Target
            {
                // Size cells in source pixels so they stay square on every aspect ratio.
                float2 sourceSize = max(_MainTex_TexelSize.zw, float2(1.0, 1.0));
                float pixelSize = floor(clamp(_PixelSize, 1.0, 8.0) + 0.5);
                float2 cellStart = floor(i.uv * sourceSize / pixelSize) * pixelSize;
                float2 cellEnd = min(cellStart + pixelSize, sourceSize);
                float2 halfTexel = 0.5 / sourceSize;
                // Center the final partial cell too, without sampling beyond the image.
                float2 sampleUV = clamp((cellStart + cellEnd) * 0.5 / sourceSize,
                    halfTexel, 1.0 - halfTexel);

                half4 original = tex2D(_MainTex, i.uv);
                half3 pixelated = tex2D(_MainTex, sampleUV).rgb;
                // Keep some native detail in thin geometry and distant silhouettes.
                half3 color = lerp(pixelated, original.rgb, saturate(_DetailPreservation));

                // Shape brightness rather than individual RGB channels to preserve hues.
                // Perceptual spacing gives dark surfaces more useful shading bands.
                float luminance = max(dot(color, float3(0.2126, 0.7152, 0.0722)), 0.0);
                float levels = max(floor(_ShadeLevels + 0.5), 3.0);
                #if defined(UNITY_COLORSPACE_GAMMA)
                    float shade = luminance * levels;
                #else
                    float shade = sqrt(luminance) * levels;
                #endif
                float band = (floor(shade) + smoothstep(0.2, 0.8, frac(shade))) / levels;
                #if defined(UNITY_COLORSPACE_GAMMA)
                    float celLuminance = band;
                #else
                    float celLuminance = band * band;
                #endif
                color *= lerp(1.0, celLuminance / max(luminance, 0.0001), saturate(_CelStrength));
                return half4(color, original.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
