Shader "ElementalGems/Fantasy Particles"
{
    Properties
    {
        _MainTex("Shape",2D)="white"{}
        [HDR] _Tint("Tint / glow",Color)=(1,1,1,1)
        _Glow("Brightness",Range(0,6))=1
        _Fade("Field lifetime fade",Range(0,1))=1
        _Flow("Flow / turbulence",Vector)=(0,0,0,0)
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend",Float)=1
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha [_DstBlend]
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _Tint,_Flow;float _Glow,_Fade;
            struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct V{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            V vert(A i){V o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color;return o;}
            fixed4 frag(V i):SV_Target
            {
                float2 uv=i.uv;
                uv.x+=sin(uv.y*17-_Time.y*_Flow.x)*_Flow.y*sin(uv.y*3.14159);
                float4 tex=tex2D(_MainTex,uv);
                float flicker=1+_Flow.z*sin(_Time.y*13+uv.y*8)*sin(_Time.y*7+uv.x*11);
                return float4(tex.rgb*i.color.rgb*_Tint.rgb*_Glow*flicker,tex.a*i.color.a*_Tint.a*_Fade);
            }
            ENDCG
        }
    }
}
