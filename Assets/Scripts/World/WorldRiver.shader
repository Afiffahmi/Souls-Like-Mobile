Shader "Nusantara/River Water"
{
    Properties { _BaseColor("Water", Color) = (.03,.32,.35,1) _FoamColor("Ripples",Color) = (.34,.67,.61,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor; half4 _FoamColor;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; };
            V vert(A v) { UNITY_SETUP_INSTANCE_ID(v); V o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); float3 w=TransformObjectToWorld(v.positionOS.xyz); o.uv=float2(w.x,w.z+w.y)/4; o.fog=ComputeFogFactor(o.positionCS.z); return o; }
            half4 frag(V i):SV_Target
            {
                float t=_Time.y;
                float ripple=sin(i.uv.x*4.3+t*.37+sin(i.uv.y*2.1-t*.24))*sin(i.uv.y*3.7-t*.29);
                float foam=smoothstep(.75,.99,ripple)*.28;
                half3 c=lerp(_BaseColor.rgb,_FoamColor.rgb,foam+.075*(ripple+1));
                return half4(MixFog(c,i.fog),1);
            }
            ENDHLSL
        }
    }
    // The existing game uses Built-in rendering. Keep a compatible pass alongside
    // the URP pass so installing URP alone never makes this material magenta.
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _BaseColor;
            fixed4 _FoamColor;
            struct A { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; UNITY_FOG_COORDS(1) };
            V vert(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                V o; o.position=UnityObjectToClipPos(v.vertex);
                float3 w=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.uv=float2(w.x,w.z+w.y)/4;
                UNITY_TRANSFER_FOG(o,o.position);
                return o;
            }
            fixed4 frag(V i):SV_Target
            {
                float t=_Time.y;
                float ripple=sin(i.uv.x*4.3+t*.37+sin(i.uv.y*2.1-t*.24))*sin(i.uv.y*3.7-t*.29);
                float foam=smoothstep(.75,.99,ripple)*.28;
                fixed4 color=fixed4(lerp(_BaseColor.rgb,_FoamColor.rgb,foam+.075*(ripple+1)),1);
                UNITY_APPLY_FOG(i.fogCoord,color);
                return color;
            }
            ENDCG
        }
    }
}
