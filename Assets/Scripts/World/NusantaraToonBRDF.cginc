#ifndef NUSANTARA_TOON_BRDF_INCLUDED
#define NUSANTARA_TOON_BRDF_INCLUDED
#include "UnityPBSLighting.cginc"
inline half NusantaraDirectRamp(half value)
{
    half x = saturate(value) * 4.0h;
    return (floor(x) + smoothstep(0.42h,0.58h,frac(x))) * 0.25h;
}
inline half4 NusantaraBRDF(half3 diffuse, half3 specular, half reflectivity,
    half smoothness, float3 normal, float3 viewDir, UnityLight light, UnityIndirect indirect)
{
    half source = max(dot(_LightColor0.rgb,half3(0.2126h,0.7152h,0.0722h)),0.0001h);
    half attenuation = saturate(dot(light.color,half3(0.2126h,0.7152h,0.0722h))/source);
    // Band distance falloff separately from the surface angle. Quantizing their
    // product erased grazing light on ground and made a torch illuminate only a dot.
    half falloff = NusantaraDirectRamp(sqrt(attenuation));
    light.color *= falloff / max(attenuation,0.0001h);
    return BRDF1_Unity_PBS(diffuse,specular,reflectivity,smoothness,normal,viewDir,light,indirect);
}
#undef UNITY_BRDF_PBS
#define UNITY_BRDF_PBS NusantaraBRDF
inline half4 LightingNusantara(SurfaceOutputStandard s, float3 viewDir, UnityGI gi)
{
    s.Normal = normalize(s.Normal);
    half oneMinusReflectivity;
    half3 specular;
    s.Albedo = DiffuseAndSpecularFromMetallic(s.Albedo,s.Metallic,specular,oneMinusReflectivity);
    half4 c = NusantaraBRDF(s.Albedo,specular,oneMinusReflectivity,s.Smoothness,s.Normal,viewDir,gi.light,gi.indirect);
    c.a = s.Alpha;
    return c;
}
inline void LightingNusantara_GI(SurfaceOutputStandard s, UnityGIInput data, inout UnityGI gi)
{
    LightingStandard_GI(s,data,gi);
}
#endif
