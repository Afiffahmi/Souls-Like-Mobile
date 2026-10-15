#ifndef NUSANTARA_SHORELINE
#define NUSANTARA_SHORELINE
sampler2D _ShoreMap;
float4 _ShoreRect;
float _ShoreEnabled, _ShoreLevel, _ShoreAmplitude, _ShorePeriod, _ShoreFoamWidth;
float4 _ShoreFoamColor;

// R = still-water depth, G = ground exists, B = ground slope, A = painted footprint.
float4 ShoreData(float2 p)
{
    float2 uv = (p - _ShoreRect.xy) / max(_ShoreRect.zw, .001);
    float inside = step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
    float4 data = tex2Dlod(_ShoreMap,float4(uv,0,0));
    data.g *= inside * _ShoreEnabled;
    return data;
}
float ShoreSurge(float2 p, float seconds)
{
    float phase = seconds * 6.2831853 / max(_ShorePeriod,.5);
    // Coherent wash with a slight variation along the beach, never tiny rippling teeth.
    phase += sin(p.x*.021+p.y*.017)*.16;
    return _ShoreAmplitude * (sin(phase) + .16*sin(2*phase+.8));
}
float ShoreFoam(float3 p)
{
    if (_ShoreEnabled < .5) return 0;
    float4 data = ShoreData(p.xz);
    float depth = data.r + p.y - _ShoreLevel;
    float distanceToBeach = max(0,depth) / max(data.b,.09);
    float width = _ShoreFoamWidth * (1+.10*sin(p.x*1.5+p.z*1.1));
    float aa = max(fwidth(distanceToBeach),.04);
    return (1-smoothstep(width-aa,width+aa,distanceToBeach)) * data.g;
}
#endif
