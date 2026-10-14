Shader "Nusantara/Environment/Toon Standard"
{
 Properties
 {
  _Color("Color",Color)=(1,1,1,1)
  _MainTex("Albedo",2D)="white"{}
  _Cutoff("Alpha Cutoff",Range(0,1))=0.5
  _Glossiness("Smoothness",Range(0,1))=0.5
  _GlossMapScale("Smoothness Scale",Range(0,1))=1
  [Enum(Metallic Alpha,0,Albedo Alpha,1)] _SmoothnessTextureChannel("Smoothness texture channel",Float)=0
  _Metallic("Metallic",Range(0,1))=0
  _MetallicGlossMap("Metallic",2D)="white"{}
  [ToggleOff] _SpecularHighlights("Specular Highlights",Float)=1
  [ToggleOff] _GlossyReflections("Glossy Reflections",Float)=1
  _BumpScale("Scale",Float)=1
  _BumpMap("Normal Map",2D)="bump"{}
  _Parallax("Height Scale",Range(0.005,0.08))=0.02
  _ParallaxMap("Height Map",2D)="black"{}
  _OcclusionStrength("Strength",Range(0,1))=1
  _OcclusionMap("Occlusion",2D)="white"{}
  _EmissionColor("Color",Color)=(0,0,0)
  _EmissionMap("Emission",2D)="white"{}
  _DetailMask("Detail Mask",2D)="white"{}
  _DetailAlbedoMap("Detail Albedo x2",2D)="grey"{}
  _DetailNormalMapScale("Scale",Float)=1
  _DetailNormalMap("Normal Map",2D)="bump"{}
  [Enum(UV0,0,UV1,1)] _UVSec("UV Set for secondary textures",Float)=0
  [HideInInspector] _Mode("__mode",Float)=0
  [HideInInspector] _SrcBlend("__src",Float)=1
  [HideInInspector] _DstBlend("__dst",Float)=0
  [HideInInspector] _ZWrite("__zw",Float)=1
 }
 SubShader
 {
  Tags {"RenderType"="Opaque" "PerformanceChecks"="False"}
  LOD 300
  Pass
  {
   Name "FORWARDBase"
   Tags {"LightMode"="ForwardBase"}
   Blend [_SrcBlend] [_DstBlend]
   ZWrite [_ZWrite]
   CGPROGRAM
   #pragma target 3.0
   #pragma vertex vertForwardBase
   #pragma fragment fragRevealBase
   #pragma shader_feature_local _NORMALMAP
   #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
   #pragma shader_feature_local _EMISSION
   #pragma shader_feature_local _METALLICGLOSSMAP
   #pragma shader_feature_local _DETAIL_MULX2
   #pragma shader_feature_local _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
   #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
   #pragma shader_feature_local _GLOSSYREFLECTIONS_OFF
   #pragma shader_feature_local _PARALLAXMAP
   #pragma multi_compile_fwdbase
   #pragma multi_compile_fog
   #pragma multi_compile_instancing
   #define UNITY_SETUP_BRDF_INPUT MetallicSetup
   #include "Assets/Scripts/World/NusantaraToonBRDF.cginc"
   #include "UnityStandardCore.cginc"
   
   half4 fragRevealBase(VertexOutputForwardBase i) : SV_Target
   {
    #if UNITY_PACK_WORLDPOS_WITH_TANGENT
    float3 world = float3(i.tangentToWorldAndPackedData[0].w, i.tangentToWorldAndPackedData[1].w, i.tangentToWorldAndPackedData[2].w);
    #else
    float3 world = i.posWorld;
    #endif
    
    return fragForwardBaseInternal(i);
   }
   ENDCG
  }
  Pass
  {
   Name "FORWARDAdd"
   Tags {"LightMode"="ForwardAdd"}
   Blend [_SrcBlend] One
   ZWrite Off
   Fog { Color (0,0,0,0) }
   CGPROGRAM
   #pragma target 3.0
   #pragma vertex vertForwardAdd
   #pragma fragment fragRevealAdd
   #pragma shader_feature_local _NORMALMAP
   #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
   #pragma shader_feature_local _EMISSION
   #pragma shader_feature_local _METALLICGLOSSMAP
   #pragma shader_feature_local _DETAIL_MULX2
   #pragma shader_feature_local _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
   #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
   #pragma shader_feature_local _GLOSSYREFLECTIONS_OFF
   #pragma shader_feature_local _PARALLAXMAP
   #pragma multi_compile_fwdadd_fullshadows
   #pragma multi_compile_fog
   #pragma multi_compile_instancing
   #define UNITY_SETUP_BRDF_INPUT MetallicSetup
   #include "Assets/Scripts/World/NusantaraToonBRDF.cginc"
   #include "UnityStandardCore.cginc"
   
   half4 fragRevealAdd(VertexOutputForwardAdd i) : SV_Target
   {
    float3 world = i.posWorld;
    return fragForwardAddInternal(i);
   }
   ENDCG
  }
  UsePass "Standard/SHADOWCASTER"
  UsePass "Standard/META"
 }
 FallBack "Standard"
 CustomEditor "StandardShaderGUI"
}
