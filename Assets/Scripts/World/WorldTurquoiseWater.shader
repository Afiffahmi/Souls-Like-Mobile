Shader "Nusantara/Turquoise Flowing Water"
{
    Properties
    {
        [Toggle] _ShoreEnabled("Enable shoreline wash", Float) = 0
        _ShoreMap("Baked beach depth and slope", 2D) = "black" {}
        _ShoreRect("Beach map X Z Width Depth", Vector) = (0,0,1,1)
        _ShoreLevel("Still sea level", Float) = 0
        _ShoreAmplitude("Shore wash height (metres)", Range(0,.9)) = .45
        _ShorePeriod("Shore wash cycle (seconds)", Range(1,12)) = 4.2
        _ShoreFoamWidth("White shoreline width (metres)", Range(.1,3)) = .8
        _ShoreFoamColor("Shore foam", Color) = (.94,1,.99,1)
        _BaseColor("Turquoise surface", Color) = (.04,.76,.82,1)
        _DeepColor("Blue troughs", Color) = (.025,.37,.59,1)
        _FoamColor("Glowing wave highlights", Color) = (.52,1,.96,1)
        _WaveScale("Wave pattern scale (per metre)", Range(.03,.7)) = .145
        _WaveSpeed("Wave motion speed", Range(0,3)) = .7
        _WaveHeight("Surface wave height (metres)", Range(0,1.5)) = .55
        _CrestStrength("Bright crest strength", Range(0,1)) = .75
        _FoamWidth("Wave highlight width", Range(.005,.2)) = .065
        _Gloss("Liquid gloss", Range(0,1)) = .7
        _FlowDirection("Flow direction X / Z", Vector) = (.65,.32,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Scripts/World/WorldShorelineCommon.hlsl"
        #include "Assets/Scripts/World/WorldTurquoiseWaterCommon.hlsl"
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half fog:TEXCOORD1; };
            V vert(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v); V o;
                o.world = WaterDisplacement(TransformObjectToWorld(v.positionOS.xyz), TransformObjectToWorldNormal(v.normalOS));
                o.positionCS = TransformWorldToHClip(o.world); o.fog = ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 frag(V i):SV_Target
            { return half4(MixFog(WaterColor(i.world, _WorldSpaceCameraPos.xyz - i.world), i.fog), 1); }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        Cull Off
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Nusantara vertex:WaterVertex fullforwardshadows addshadow exclude_path:deferred
        #pragma multi_compile_instancing
        #include "NusantaraToonBRDF.cginc"
        #include "Assets/Scripts/World/WorldShorelineCommon.hlsl"
        #include "Assets/Scripts/World/WorldTurquoiseWaterCommon.hlsl"
        struct Input { float3 worldPos; };
        void WaterVertex(inout appdata_full v)
        {
            float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
            float3 n=UnityObjectToWorldNormal(v.normal);
            float h; float2 slope,crests;
            WaterShape(world.xz,WaterTime(),h,slope,crests);
            world=WaterDisplacement(world,n);
            v.vertex=mul(unity_WorldToObject,float4(world,1));
            if(n.y>.5) v.normal=normalize(mul((float3x3)unity_WorldToObject,float3(-slope.x,1,-slope.y)));
        }
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            o.Albedo=WaterColor(i.worldPos,_WorldSpaceCameraPos.xyz-i.worldPos);
            float shoreFoam=ShoreFoam(i.worldPos);
            o.Emission=_ShoreFoamColor.rgb*shoreFoam*.55;
            o.Metallic=0;o.Smoothness=lerp(_Gloss*.65,.08,shoreFoam);o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

