#ifndef NUSANTARA_WORLD_CLOUD_NOISE
#define NUSANTARA_WORLD_CLOUD_NOISE
float CloudHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float CloudNoise(float2 p)
{
    float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(CloudHash(cell),CloudHash(cell+float2(1,0)),f.x),
        lerp(CloudHash(cell+float2(0,1)),CloudHash(cell+1),f.x),f.y);
}
float WorldCloudCover(float2 world, float2 offset, float scale, float coverage)
{
    // Translate the entire cloud field. Its shape never oscillates around a fixed centre.
    float2 p=(world-offset)/max(scale,1);
    float n=CloudNoise(p)*.65+CloudNoise(p*2.03+17.1)*.25+CloudNoise(p*4.01-7.4)*.10;
    float threshold=lerp(.82,.25,saturate(coverage));
    return smoothstep(threshold-.09,threshold+.09,n);
}
#endif
