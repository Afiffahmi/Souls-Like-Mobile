Shader "Nusantara/World Scale Road"
{
    Properties
    {
        _MainTex("Road texture",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _RoadTile("Texture repeat in metres",Float)=.8
        _Glossiness("Smoothness",Range(0,1))=.08
        _Metallic("Metallic",Range(0,1))=0
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Nusantara fullforwardshadows
        #include "Assets/Scripts/World/NusantaraToonBRDF.cginc"
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness,_Metallic;
        float _RoadTile;
        struct Input {float3 worldPos;};
        void surf(Input i,inout SurfaceOutputStandard o)
        {o.Albedo=tex2D(_MainTex,i.worldPos.xz/max(.1,_RoadTile)).rgb*_Color.rgb;o.Smoothness=_Glossiness;o.Metallic=_Metallic;o.Alpha=1;}
        ENDCG
    }
    Fallback "Standard"
}
