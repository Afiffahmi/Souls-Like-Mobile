Shader "Nusantara/Kampung Foliage"
{
    Properties
    {
        _MainTex("Foliage",2D)="white"{}
        _BumpMap("Normal",2D)="bump"{}
        _AlphaTex("Separate alpha",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _UseVertexColor("Particle colors",Range(0,1))=0
        _Cutoff("Alpha cutoff",Range(0,1))=.4
        _Wind("Wind",Range(0,.3))=.045
    }
    SubShader
    {
        Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"}
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard alphatest:_Cutoff vertex:wind addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing
        sampler2D _MainTex,_BumpMap,_AlphaTex;fixed4 _Color;float _Wind,_UseVertexColor;
        struct Input{float2 uv_MainTex;float2 uv_BumpMap;float4 color:COLOR;};
        void wind(inout appdata_full v)
        {
            float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
            float strength=saturate(v.vertex.y)*_Wind;
            v.vertex.x+=sin(world.x*.3+world.z*.55+_Time.y*1.8)*strength;
            v.vertex.z+=cos(world.z*.4-_Time.y*1.5)*strength*.6;
        }
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            fixed4 leaf=tex2D(_MainTex,i.uv_MainTex)*_Color*lerp(fixed4(1,1,1,1),i.color,_UseVertexColor);
            o.Albedo=leaf.rgb;o.Alpha=leaf.a*tex2D(_AlphaTex,i.uv_MainTex).r;o.Normal=UnpackNormal(tex2D(_BumpMap,i.uv_BumpMap));o.Smoothness=.1;o.Metallic=0;
        }
        ENDCG
    }
    FallBack "Transparent/Cutout/Diffuse"
}
