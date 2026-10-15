Shader "ElementalGems/Surface Aura Flame"
{
    Properties { [HDR] _Tint("Element Colour",Color)=(.65,.12,.95,1) _Intensity("Glow",Range(0,3))=1 _NeonEmission("Neon Emission",Range(1,4))=2 }
    SubShader
    {
        Tags { "Queue"="Transparent+16" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Tint; float _Intensity, _NeonEmission;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            float4 frag(v2f i):SV_Target
            {
                float y=saturate(i.uv.y);
                float bend=sin(y*9-_Time.y*7)*.10*y+sin(y*17+_Time.y*3)*.025;
                float width=lerp(.26,.012,y);
                float d=abs(i.uv.x-.5-bend)/max(.01,width);
                float edge=pow(saturate(1-d),1.7);
                float tip=sin(y*3.14159);
                float alpha=edge*tip*i.color.a;
                return float4(_Tint.rgb*_Intensity*i.color.rgb*_NeonEmission,alpha);
            }
            ENDCG
        }
    }
}
