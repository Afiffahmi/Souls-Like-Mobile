Shader "Hidden/NewImageEffectShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PixelSize ("Pixel Size (Source Pixels)", Range(1, 8)) = 3
        _DetailPreservation ("Detail Preservation", Range(0, 1)) = 0.25
        _CelStrength ("Soft Cel Shading", Range(0, 1)) = 0.45
        _ShadeLevels ("Lighting Bands", Range(3, 12)) = 6
        _Exposure ("Brightness (Stops)", Range(-1, 1)) = 0.55
        _Saturation ("Saturation", Range(0, 2)) = 1.25
        _Vibrance ("Vibrance", Range(0, 1)) = 0.3
        _Contrast ("Contrast", Range(0.5, 1.5)) = 1.08
        _ShadowLift ("Shadow Detail", Range(0, 0.2)) = 0.045
        _ShadowTint ("Shadow Tint", Color) = (0.83, 0.91, 1, 1)
        _HighlightTint ("Sunlit Tint", Color) = (1, 0.98, 0.92, 1)
        _TintStrength ("Colour Mood", Range(0, 1)) = 0.3
        [Toggle] _StylizedSun ("Crisp Gameplay Shadows", Float) = 1
        _ShadowStrength ("Cast Shadow Strength", Range(0, 1)) = 0.82
        _CanopyStrength ("Drifting Canopy Strength", Range(0, 0.8)) = 0.35
        _CanopySize ("Canopy World Size", Range(5, 100)) = 32
        _CanopySpeed ("Canopy Drift Speed", Range(0, 2)) = 0.4
        _NeonStrength ("Neon Glow", Range(0, 2)) = 0.75
        _NeonThreshold ("Neon Brightness Threshold", Range(0.5, 3)) = 1.05
        _NeonRadius ("Neon Halo Radius", Range(0.5, 3)) = 1.25
        _NeonSaturation ("Neon Colour", Range(1, 3)) = 1.8
        [HideInInspector] _NeonTex ("Neon Buffer", 2D) = "black" {}
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
            sampler2D _NeonTex;
            float _NeonStrength, _NeonThreshold, _NeonSaturation;
            float4 _MainTex_TexelSize;
            float _PixelSize;
            half _DetailPreservation;
            half _CelStrength;
            float _ShadeLevels;
            float _Exposure, _Saturation, _Vibrance, _Contrast, _ShadowLift, _TintStrength;
            float4 _ShadowTint, _HighlightTint;

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
                float peak = max(color.r, max(color.g, color.b));
                float hot = smoothstep(_NeonThreshold * 0.75, _NeonThreshold * 1.8, peak);
                float low = min(color.r, min(color.g, color.b));
                // Deepen the hue around bright cores, while neutral highlights stay neutral.
                float3 neon = max(color - low * (1.0 - 1.0 / max(_NeonSaturation, 1.0)), 0.0);
                neon *= peak / max(max(neon.r, max(neon.g, neon.b)), 0.0001);
                color = lerp(color, neon, hot);
                color += tex2D(_NeonTex, i.uv).rgb * _NeonStrength;

                // Grade in perceptual space in both Gamma and Linear projects.
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    color = LinearToGammaSpace(max(color, 0.0));
                #endif
                color *= exp2(_Exposure);
                float gray = dot(color, float3(0.2126, 0.7152, 0.0722));
                float chroma = max(color.r, max(color.g, color.b)) - min(color.r, min(color.g, color.b));
                // Already vivid gems and emissive effects need less saturation boost.
                float saturation = _Saturation + _Vibrance * (1.0 - saturate(chroma * 2.0));
                color = max(lerp(gray.xxx, color, saturation), 0.0);
                color = max((color - 0.45) * _Contrast + 0.45, 0.0);
                float shadowMask = 1.0 - smoothstep(0.05, 0.55, gray);
                color += _ShadowLift * shadowMask;
                float3 mood = lerp(_HighlightTint.rgb, _ShadowTint.rgb, shadowMask);
                color *= lerp(float3(1, 1, 1), mood, _TintStrength);

                // Shape brightness rather than individual RGB channels to preserve hues.
                // Perceptual spacing gives dark surfaces more useful shading bands.
                float luminance = max(dot(color, float3(0.2126, 0.7152, 0.0722)), 0.0);
                float levels = max(floor(_ShadeLevels + 0.5), 3.0);
                float shade = luminance * levels;
                float band = (floor(shade) + smoothstep(0.2, 0.8, frac(shade))) / levels;
                float celLuminance = band;
                color *= lerp(1.0, celLuminance / max(luminance, 0.0001), saturate(_CelStrength));
                // Compress brightness as a whole so hot cyan/purple never clips to white.
                float brightest = max(color.r, max(color.g, color.b));
                float excess = max(brightest - 0.85, 0.0);
                float mapped = 0.85 + excess / (1.0 + excess / 0.15);
                color *= brightest > 0.85 ? mapped / max(brightest, 0.0001) : 1.0;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    color = GammaToLinearSpace(max(color, 0.0));
                #endif
                return half4(color, original.a);
            }
            ENDCG
        }
        // GPU light cookie, world-projected by the real directional light.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment canopy
            #include "UnityCG.cginc"
            float _CanopyStrength, _CanopySpeed;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float noise(float2 uv, float cells)
            {
                float2 p = uv * cells;
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float2 a = cell - floor(cell / cells) * cells;
                float2 b = a + 1.0;
                b -= floor(b / cells) * cells;
                return lerp(lerp(hash(a), hash(float2(b.x, a.y)), f.x),
                    lerp(hash(float2(a.x, b.y)), hash(b), f.x), f.y);
            }
            half4 canopy(v2f_img i) : SV_Target
            {
                float2 uv = i.uv + _Time.y * _CanopySpeed * float2(0.012, 0.008);
                float n = noise(uv, 8.0) * 0.60 + noise(uv + 0.37, 24.0) * 0.25
                    + noise(uv - 0.19, 64.0) * 0.15;
                float patch = smoothstep(0.40, 0.60, n);
                patch = floor(patch * 6.0 + 0.5) / 6.0;
                float light = lerp(1.0 - _CanopyStrength, 1.0, patch);
                // Built-in directional cookies read alpha.
                return half4(light, light, light, light);
            }
            ENDCG
        }
        // Extract before grading so the terrain's boosted colours do not bloom.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment extract
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _NeonThreshold, _NeonSaturation;
            half3 bright(float2 uv)
            {
                float3 c = max(tex2D(_MainTex, uv).rgb, 0.0);
                float peak = max(c.r, max(c.g, c.b));
                float knee = max(_NeonThreshold * 0.25, 0.001);
                float soft = clamp(peak - _NeonThreshold + knee, 0.0, 2.0 * knee);
                soft = soft * soft / (4.0 * knee);
                float weight = max(peak - _NeonThreshold, soft) / max(peak, 0.0001);
                float low = min(c.r, min(c.g, c.b));
                c = max(c - low * (1.0 - 1.0 / max(_NeonSaturation, 1.0)), 0.0);
                c *= peak / max(max(c.r, max(c.g, c.b)), 0.0001);
                // Bound extreme single-pixel emitters to avoid huge flickering halos.
                return c * weight * min(1.0, 6.0 / max(peak, 0.0001));
            }
            half4 extract(v2f_img i) : SV_Target
            {
                float2 d = _MainTex_TexelSize.xy;
                half3 glow = bright(i.uv + d * float2(-1, -1))
                    + bright(i.uv + d * float2(1, -1))
                    + bright(i.uv + d * float2(-1, 1))
                    + bright(i.uv + d * float2(1, 1));
                return half4(glow * 0.25, 1);
            }
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment blur
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _GlowDirection;
            float _NeonRadius;
            half4 blur(v2f_img i) : SV_Target
            {
                float2 d = _MainTex_TexelSize.xy * _GlowDirection.xy * _NeonRadius;
                half3 c = tex2D(_MainTex, i.uv).rgb * 0.227027;
                c += tex2D(_MainTex, i.uv + d * 1.384615).rgb * 0.316216;
                c += tex2D(_MainTex, i.uv - d * 1.384615).rgb * 0.316216;
                c += tex2D(_MainTex, i.uv + d * 3.230769).rgb * 0.070270;
                c += tex2D(_MainTex, i.uv - d * 3.230769).rgb * 0.070270;
                return half4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
