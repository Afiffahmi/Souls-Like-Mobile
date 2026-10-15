Shader "Nusantara/Drifting Cloud Sky"
{
    Properties
    {
        _Daylight("Daylight",Range(0,1))=1
        _CloudCoverage("Cloud coverage",Range(0,1))=.52
        _CloudScale("Cloud size metres",Float)=180
        _CloudHeight("Cloud altitude metres",Float)=500
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "WorldCloudNoise.cginc"
            float _Daylight,_CloudCoverage,_CloudScale,_CloudHeight;
            float4 _CloudOffset,_SunDirection,_MoonDirection;
            struct V {float4 pos:SV_POSITION;float3 direction:TEXCOORD0;};
            V vert(appdata_base v){V o;o.pos=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;}
            half4 frag(V i):SV_Target
            {
                float3 ray=normalize(i.direction);
                float height=saturate(ray.y);
                float3 day=lerp(float3(.48,.60,.67),float3(.16,.36,.57),pow(height,.6));
                float3 night=lerp(float3(.022,.030,.065),float3(.004,.008,.025),height);
                float3 sky=lerp(night,day,_Daylight);
                float sunset=(1-abs(_Daylight*2-1))*pow(1-height,4);
                sky=lerp(sky,float3(.66,.34,.20),sunset*.6);
                float sun=pow(saturate(dot(ray,normalize(_SunDirection.xyz))),1500);
                float moon=pow(saturate(dot(ray,normalize(_MoonDirection.xyz))),2200);
                sky+=float3(1,.78,.45)*sun*_Daylight;
                sky+=float3(.48,.60,.82)*moon*(1-_Daylight);
                float2 starCell=floor(ray.xz/max(ray.y+.35,.1)*450);
                float star=step(.9985,CloudHash(starCell))*pow(height,.4);
                sky+=star*(1-_Daylight)*.48;
                float plane=max(1,_CloudHeight-_WorldSpaceCameraPos.y)/max(ray.y,.025);
                float2 world=_WorldSpaceCameraPos.xz+ray.xz*plane;
                float cloud=WorldCloudCover(world,_CloudOffset.xy,_CloudScale,_CloudCoverage);
                cloud*=smoothstep(.08,.30,ray.y);
                float3 cloudColor=lerp(float3(.045,.055,.085),float3(.66,.68,.63),_Daylight);
                cloudColor=lerp(cloudColor,float3(.70,.43,.27),sunset*.3);
                sky=lerp(sky,cloudColor,cloud*.9);
                return half4(sky,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
