Shader "Nusantara/Turquoise Flowing Water"
{
    Properties
    {
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
            #include "WorldTurquoiseWaterCommon.hlsl"
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
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "WorldTurquoiseWaterCommon.hlsl"
            struct A { float4 vertex:POSITION; float3 normal:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 position:SV_POSITION; float3 world:TEXCOORD0; UNITY_FOG_COORDS(1) };
            V vert(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v); V o;
                o.world = WaterDisplacement(mul(unity_ObjectToWorld, v.vertex).xyz, UnityObjectToWorldNormal(v.normal));
                o.position = UnityWorldToClipPos(o.world); UNITY_TRANSFER_FOG(o,o.position); return o;
            }
            half4 frag(V i):SV_Target
            {
                half4 color = half4(WaterColor(i.world, _WorldSpaceCameraPos.xyz - i.world), 1);
                UNITY_APPLY_FOG(i.fogCoord,color); return color;
            }
            ENDCG
        }
    }
    FallBack Off
}
