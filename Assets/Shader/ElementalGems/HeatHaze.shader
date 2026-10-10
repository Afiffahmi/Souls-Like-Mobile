Shader "ElementalGems/Heat Haze"
{
    Properties{_MainTex("Soft noise mask",2D)="white"{} _Strength("Distortion",Range(0,.015))=.002}
    SubShader
    {
        Tags{"Queue"="Transparent+5" "RenderType"="Transparent"}
        GrabPass{"_GemHeatBackground"}
        ZWrite Off Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex,_GemHeatBackground;float _Strength;
            struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct V{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 grab:TEXCOORD1;fixed4 color:COLOR;};
            V vert(A i){V o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.grab=ComputeGrabScreenPos(o.vertex);o.color=i.color;return o;}
            fixed4 frag(V i):SV_Target
            {
                float mask=tex2D(_MainTex,i.uv).a*i.color.a;
                float2 wave=float2(sin(i.uv.y*24-_Time.y*8),cos(i.uv.x*21+_Time.y*5));
                i.grab.xy+=wave*_Strength*mask*i.grab.w;
                float4 c=tex2Dproj(_GemHeatBackground,UNITY_PROJ_COORD(i.grab));c.a=mask*.65;return c;
            }
            ENDCG
        }
    }
}
