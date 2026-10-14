#ifndef NUSANTARA_TURQUOISE_WATER
#define NUSANTARA_TURQUOISE_WATER
#include "WorldShorelineCommon.hlsl"
float4 _BaseColor, _DeepColor, _FoamColor;
float _WaveScale, _WaveSpeed, _WaveHeight, _CrestStrength, _FoamWidth, _Gloss;
float4 _FlowDirection;
float _NusantaraWaterEditorTime, _NusantaraWaterEditorPreview;
float WaterTime()
{
    return lerp(_Time.y, _NusantaraWaterEditorTime, saturate(_NusantaraWaterEditorPreview)) * _WaveSpeed;
}
void WaterShape(float2 p, float t, out float height, out float2 slope, out float2 crests)
{
    float k = _WaveScale * 6.2831853;
    float2 flow = normalize(_FlowDirection.xy + float2(.0001, .0001));
    float2 crossFlow = float2(-flow.y, flow.x);
    float bend = dot(p, crossFlow) * k * .72 + t * .24;
    float a = dot(p, flow) * k - t * 1.14 + sin(bend) * .75;
    float2 da = flow * k + cos(bend) * .75 * crossFlow * k * .72;
    float2 d2 = normalize(flow * .4 + crossFlow * .92);
    float b = dot(p, d2) * k * 1.23 - t * .87 + .8;
    float2 db = d2 * k * 1.23;
    float2 d3 = normalize(flow * .82 - crossFlow * .57);
    float c = dot(p, d3) * k * 1.89 + t * .61;
    float2 dc = d3 * k * 1.89;
    height = _WaveHeight * (.64 * sin(a) + .25 * sin(b) + .11 * sin(c));
    slope = _WaveHeight * (.64 * cos(a) * da + .25 * cos(b) * db + .11 * cos(c) * dc);
    crests = float2(.5 + .5 * sin(a), .5 + .5 * sin(b));
}
float3 WaterDisplacement(float3 positionWS, float3 normalWS)
{
    float h; float2 slope, crests;
    WaterShape(positionWS.xz, WaterTime(), h, slope, crests);
    if (_ShoreEnabled > .5)
    {
        float4 shore=ShoreData(positionWS.xz);
        float shallow=(1-smoothstep(.6,5,shore.r))*shore.g;
        float seconds=lerp(_Time.y,_NusantaraWaterEditorTime,saturate(_NusantaraWaterEditorPreview));
        h=h*lerp(1,.08,shallow)+ShoreSurge(positionWS.xz,seconds)*shallow;
    }
    return positionWS + float3(0, h * saturate(normalWS.y), 0);
}
half3 WaterColor(float3 positionWS, float3 viewDirection)
{
    float h; float2 slope, crests;
    WaterShape(positionWS.xz, WaterTime(), h, slope, crests);
    float3 normal = normalize(float3(-slope.x, 1, -slope.y));
    float3 view = normalize(viewDirection);
    float3 light = normalize(float3(-.35, .83, -.43));
    float facing = saturate(dot(normal, light));
    float fresnel = pow(1 - saturate(dot(normal, view)), 4);
    float broadSpec = pow(saturate(dot(normal, normalize(view + light))), 16);
    float glint = pow(saturate(dot(normal, normalize(view + light))), lerp(48, 110, _Gloss));
    float elevation = h / max(_WaveHeight, .001);
    half3 color = lerp(_DeepColor.rgb, _BaseColor.rgb, saturate(.48 + elevation * .24 + (facing - .6) * .5));
    float width = lerp(24, 5, saturate(_FoamWidth * 5));
    float crest = pow(crests.x, width) * smoothstep(.18, .9, crests.y)
        + pow(crests.y, width + 4) * smoothstep(.3, .95, 1 - crests.x) * .4;
    float detail = 1 - smoothstep(.09, .4, max(length(ddx(positionWS.xz)), length(ddy(positionWS.xz))) * _WaveScale);
    float thinCrest = pow(crests.x, 150) * smoothstep(.05, .55, crests.y);
    float lightRim = (crest * (.3 + .5 * broadSpec) + thinCrest * .48) * _CrestStrength * detail;
    color = lerp(color, _FoamColor.rgb, saturate(lightRim));
    color += _FoamColor.rgb * (broadSpec * .10 + glint * .22 + fresnel * .18);
    if (_ShoreEnabled > .5)
    {
        float4 shore=ShoreData(positionWS.xz);
        float shallow=(1-smoothstep(.3,3,shore.r))*shore.g;
        color=lerp(color,_BaseColor.rgb*1.12,shallow*.50);
        color=lerp(color,_ShoreFoamColor.rgb,ShoreFoam(positionWS));
    }
    return color;
}
#endif
