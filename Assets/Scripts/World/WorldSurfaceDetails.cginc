#ifndef NUSANTARA_SURFACE_DETAILS
#define NUSANTARA_SURFACE_DETAILS
UNITY_DECLARE_TEX2DARRAY(_SurfaceDetailArray);
sampler2D _SurfaceDetailControl, _SurfaceDetailParams;
float4 _SurfaceDetailControl_TexelSize;
float _SurfaceDetailEnabled;
half3 SurfaceDetailSample(float encoded, float2 world)
{
    float index=floor(encoded*255+.5);
    float4 settings=tex2Dlod(_SurfaceDetailParams,float4((index+.5)/256, .5,0,0));
    float2 uv=world/max(.1,settings.x);
    half3 c=UNITY_SAMPLE_TEX2DARRAY(_SurfaceDetailArray,float3(uv,max(0,index-1))).rgb;
    float noise=sin(world.x*1.31+sin(world.y*.91))*sin(world.y*1.17);
    return c*(1+noise*settings.y*.3);
}
half4 SurfaceDetailAt(float2 texel,float2 world)
{
    float2 uv=(texel+.5)*_SurfaceDetailControl_TexelSize.xy;
    half4 c=tex2Dlod(_SurfaceDetailControl,float4(uv,0,0));
    half4 result=0;
    if(c.g>.001){result.rgb+=SurfaceDetailSample(c.r,world)*c.g;result.a+=c.g;}
    if(c.a>.001){result.rgb+=SurfaceDetailSample(c.b,world)*c.a;result.a+=c.a;}
    return result;
}
half3 ApplySurfaceDetails(half3 original,float2 world,float2 uv)
{
    if(_SurfaceDetailEnabled<.5)return original;
    float inside=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
    if(inside<.5)return original;
    float2 texel=uv*_SurfaceDetailControl_TexelSize.zw-.5;
    float2 cell=floor(texel),f=frac(texel);
    half4 detail=lerp(lerp(SurfaceDetailAt(cell,world),SurfaceDetailAt(cell+float2(1,0),world),f.x),
        lerp(SurfaceDetailAt(cell+float2(0,1),world),SurfaceDetailAt(cell+1,world),f.x),f.y);
    return original*(1-saturate(detail.a))+detail.rgb;
}
#endif
