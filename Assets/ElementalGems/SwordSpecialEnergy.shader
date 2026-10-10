Shader "ElementalGems/Sword Special Energy"
{
    Properties
    {
        [HDR] _Tint("Gem colour",Color)=(1,.45,.08,1)
        _Opacity("Opacity",Range(0,1))=.6
        _Intensity("Glow",Range(0,3))=1.25
        _Ribbon("Ribbon mode",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+15" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Tint;
            float _Opacity, _Intensity, _Ribbon;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; float2 uv:TEXCOORD2; float4 color:COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                v.vertex.xyz += v.normal * .006 * (1-saturate(_Ribbon));
                o.pos=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.uv=v.uv; o.color=v.color;
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                if (_Ribbon > .5)
                {
                    float ribbon=pow(saturate(1-abs(i.uv.y*2-1)),1.2)*i.color.a;
                    return float4(_Tint.rgb*_Intensity,ribbon*_Opacity);
                }
                float rim=pow(1-saturate(abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)))),2);
                float flow=.85+.15*sin(i.world.y*28-_Time.y*20);
                float body=(.12+rim*.88)*flow;
                float alpha=body*_Opacity;
                return float4(_Tint.rgb*_Intensity,alpha);
            }
            ENDCG
        }
    }
}
