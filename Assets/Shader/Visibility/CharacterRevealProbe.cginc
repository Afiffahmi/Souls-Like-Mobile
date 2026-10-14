#include "UnityCG.cginc"
sampler2D _MainTex;
float4 _MainTex_ST, _Color;
float _Cutoff;
float4x4 _CharacterRevealProbeVP;
float3 _CharacterRevealProbeCenter;
float4 _CharacterRevealProbeForward;
#ifdef REVEAL_PROBE_FOLIAGE
sampler2D _AlphaTex;
float _Wind;
#endif
struct RevealProbeInput { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct RevealProbeOutput { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; };
RevealProbeOutput vertRevealProbe(RevealProbeInput v)
{
    UNITY_SETUP_INSTANCE_ID(v);
    RevealProbeOutput o;
#ifdef REVEAL_PROBE_FOLIAGE
    float3 originalWorld = mul(unity_ObjectToWorld, v.vertex).xyz;
    float strength = saturate(v.vertex.y) * _Wind;
    v.vertex.x += sin(originalWorld.x*.3 + originalWorld.z*.55 + _Time.y*1.8) * strength;
    v.vertex.z += cos(originalWorld.z*.4 - _Time.y*1.5) * strength*.6;
#endif
    o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.pos = mul(_CharacterRevealProbeVP, float4(o.world, 1));
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    return o;
}
fixed4 fragRevealProbe(RevealProbeOutput i) : SV_Target
{
    // Props behind the player cannot hide them, regardless of the circle radius.
    clip(-dot(i.world - _CharacterRevealProbeCenter, _CharacterRevealProbeForward.xyz) - _CharacterRevealProbeForward.w);
#ifdef REVEAL_PROBE_FOLIAGE
    clip(tex2D(_MainTex,i.uv).a * _Color.a * tex2D(_AlphaTex,i.uv).r - _Cutoff);
#elif defined(_ALPHATEST_ON)
    clip(tex2D(_MainTex,i.uv).a * _Color.a - _Cutoff);
#elif defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
    clip(tex2D(_MainTex,i.uv).a * _Color.a - 0.5);
#endif
    return 1;
}
