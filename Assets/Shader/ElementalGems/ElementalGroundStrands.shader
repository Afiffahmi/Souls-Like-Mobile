Shader "ElementalGems/Ground Strands"
{
    Properties
    {
        [HDR] _ElementTint ("Element Colour", Color) = (.5,.4,1,1)
        _Element ("Element", Float) = 5
        _Fade ("Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+13" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _ElementTint; float _Element, _Fade;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            float4 frag(Output i):SV_Target
            {
                float core=pow(saturate(1-abs(i.uv.y*2-1)),2);
                float lightning=step(4.5,_Element)*(1-step(5.5,_Element));
                float3 tint=lerp(_ElementTint.rgb,1,core*.6*lightning);
                return float4(tint*lerp(1.1,2.5,lightning),core*i.color.a*_Fade);
            }
            ENDCG
        }
    }
}
