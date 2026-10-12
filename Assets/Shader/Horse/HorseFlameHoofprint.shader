Shader "Horse/Fading Flame Hoofprint"
{
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
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            V vert(A v){V o;o.position=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            half4 frag(V i):SV_Target
            {
                float2 p=(i.uv-.5)*2; p.x*=1.18;
                float radius=length(p);
                float edge=1-smoothstep(.065,.23,abs(radius-.61));
                float openHeel=smoothstep(-.55,-.25,p.y);
                float ember=.62+.38*sin(atan2(p.y,p.x)*13+_Time.y*9);
                float center=.14*(1-smoothstep(.1,.65,radius));
                float alpha=(edge*openHeel*ember+center)*i.color.a;
                return half4(i.color.rgb*1.7,alpha);
            }
            ENDCG
        }
    }
}
