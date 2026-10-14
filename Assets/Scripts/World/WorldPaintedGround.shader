Shader "Nusantara/Painted Regional Ground"
{
    Properties
    {
        _MainTex("Original ground", 2D) = "white" {}
        _Color("Original ground tint", Color) = (1,1,1,1)
        _Glossiness("Smoothness", Range(0,1)) = .15
        _Metallic("Metallic", Range(0,1)) = 0
        _SurfacePaint("Painted surfaces", 2D) = "black" {}
        _SurfaceRect("World X Z Width Depth", Vector) = (0,0,1,1)
        _SurfaceDetailControl("Fine surface coverage", 2D) = "black" {}
        _SurfaceDetailArray("Fine surface textures", 2DArray) = "" {}
        _SurfaceDetailParams("Fine surface scales", 2D) = "black" {}
        _SurfaceDetailEnabled("Enable fine surfaces", Float) = 0
        _RegionMap("Mainland region paint", 2D) = "black" {}
        _RegionRect("Mainland X Z Width Depth", Vector) = (0,0,1,1)
        _RegionEnabled("Use painted regions", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Nusantara fullforwardshadows
        #pragma target 3.5
        #pragma multi_compile_instancing
        #pragma shader_feature_local _GLOSSYREFLECTIONS_OFF
        #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
        #include "Assets/Scripts/World/NusantaraToonBRDF.cginc"
        #include "Assets/Scripts/World/WorldSurfaceDetails.cginc"
        sampler2D _MainTex, _SurfacePaint, _RegionMap;
        fixed4 _Color;
        float4 _SurfaceRect;
        float4 _RegionRect;
        float _RegionEnabled;
        half _Glossiness, _Metallic;
        struct Input { float2 uv_MainTex; float3 worldPos; };
        void surf(Input i, inout SurfaceOutputStandard o)
        {
            // Use one world-space texture lattice across all regions and render tiles.
            float2 groundUV = i.worldPos.xz / 32.0;
            fixed3 original = tex2D(_MainTex, groundUV).rgb * _Color.rgb;
            float2 regionUV = (i.worldPos.xz - _RegionRect.xy) / max(_RegionRect.zw, .001);
            float4 region = tex2D(_RegionMap, regionUV);
            float regionInside = step(0,regionUV.x)*step(regionUV.x,0.999999)*step(0,regionUV.y)*step(regionUV.y,0.999999);
            #ifdef UNITY_COLORSPACE_GAMMA
            region.rgb = LinearToGammaSpace(region.rgb);
            #endif
            original = lerp(original, tex2D(_MainTex, groundUV).rgb * region.rgb, _RegionEnabled * regionInside * step(.5/255.0,region.a));
            float2 uv = (i.worldPos.xz - _SurfaceRect.xy) / max(_SurfaceRect.zw, .001);
            fixed4 paint = tex2D(_SurfacePaint, uv);
            #ifdef UNITY_COLORSPACE_GAMMA
            paint.rgb = LinearToGammaSpace(paint.rgb / max(paint.a, .00001)) * paint.a;
            #endif
            // RGB is premultiplied by coverage in linear space: filtering keeps soft edges clean.
            float inside = step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
            paint *= inside;
            o.Albedo = original * (1-paint.a) + paint.rgb;
            o.Albedo = ApplySurfaceDetails(o.Albedo,i.worldPos.xz,uv);
            o.Metallic = _Metallic * (1-paint.a);
            o.Smoothness = lerp(_Glossiness, .08, paint.a);
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Standard"
}

