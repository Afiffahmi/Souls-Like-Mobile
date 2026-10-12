Shader "Nusantara/Kampung Worn Path"
{
    Properties
    {
        _MainTex("Worn sand and pebbles",2D)="white"{}
        _BumpMap("Surface normal",2D)="bump"{}
        _Rough("Roughness",2D)="white"{}
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex,_BumpMap,_Rough;
        struct Input {float3 worldPos;};
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            float2 uv=i.worldPos.xz/3;
            float variation=.92+.08*sin(i.worldPos.x*.043+sin(i.worldPos.z*.026));
            o.Albedo=tex2D(_MainTex,uv).rgb*variation;
            o.Normal=UnpackNormal(tex2D(_BumpMap,uv));
            o.Smoothness=(1-tex2D(_Rough,uv).r)*.35;o.Metallic=0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
