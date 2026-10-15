Shader "Nusantara/Shoreline Wet Sand"
{
    Properties
    {
        _ShoreMap("Baked shoreline",2D)="black"{}
        _ShoreRect("Map X Z Width Depth",Vector)=(0,0,1,1)
        _ShoreEnabled("Enabled",Float)=1
        _ShoreLevel("Sea level",Float)=0
        _ShoreAmplitude("Wash height",Float)=.45
        _ShorePeriod("Wash cycle seconds",Float)=4.2
        _WetTint("Wet sand multiplier",Color)=(.69,.63,.58,1)
    }
    SubShader
    {
        Tags { "Queue"="Geometry+5" "RenderType"="Transparent" }
        Pass
        {
            Blend DstColor Zero
            ZWrite Off
            Offset -1,-1
            Cull Back
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "WorldShorelineCommon.hlsl"
            float4 _WetTint;
            float _NusantaraWaterEditorTime, _NusantaraWaterEditorPreview;
            struct V { float4 pos:SV_POSITION; float3 world:TEXCOORD0; UNITY_FOG_COORDS(1) };
            V vert(appdata_base v) { V o; o.world=mul(unity_ObjectToWorld,v.vertex).xyz; o.pos=UnityObjectToClipPos(v.vertex); UNITY_TRANSFER_FOG(o,o.pos); return o; }
            half4 frag(V i):SV_Target
            {
                float4 data=ShoreData(i.world.xz);
                float seconds=lerp(_Time.y,_NusantaraWaterEditorTime,saturate(_NusantaraWaterEditorPreview));
                float surge=ShoreSurge(i.world.xz,seconds);
                float highWater=_ShoreLevel+_ShoreAmplitude*1.16;
                float wet=(1-smoothstep(highWater-.10,highWater+.12,i.world.y))*data.g*data.a;
                // The upper edge slowly dries while the sea retreats; the sand texture remains visible.
                wet*=lerp(.68,1,saturate((surge/max(_ShoreAmplitude,.001)+1)*.5));
                half4 color=half4(lerp(half3(1,1,1),_WetTint.rgb,wet),1);
                UNITY_APPLY_FOG_COLOR(i.fogCoord,color,half4(1,1,1,1));
                return color;
            }
            ENDCG
        }
    }
}

