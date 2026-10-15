// Shared Built-in lighting: band the incident light, not texture colours.
#ifndef NUSANTARA_TOON_LIGHTING_INCLUDED
#define NUSANTARA_TOON_LIGHTING_INCLUDED
inline half NusantaraRamp(half value, half steps)
{
    half x = saturate(value) * max(steps, 2.0h);
    return (floor(x) + smoothstep(0.42h, 0.58h, frac(x))) / max(steps, 2.0h);
}
inline half4 NusantaraLeafLight(SurfaceOutput s, half3 lightDir, half atten, half steps, half wrap)
{
    half ndl = saturate((dot(normalize(s.Normal),lightDir) + wrap) / (1.0h + wrap));
    half falloff = NusantaraRamp(sqrt(saturate(atten)),steps);
    return half4(s.Albedo * _LightColor0.rgb * NusantaraRamp(ndl,steps) * falloff * .8h,s.Alpha);
}
#endif
