Shader "ElementalGems/Army Elemental Energy"
{
    Properties
    {
        [HDR] _Tint("Element Colour", Color)=(.65,.12,.95,1)
        _Opacity("Opacity", Range(0,1))=.85
        _Intensity("Glow", Range(0,3))=1.5
        _OuterShell("Outer flame layer", Float)=0
        _ShellWidth("Outer width in metres", Range(0,.2))=.065
    }
    SubShader
    {
        Tags { "Queue"="Transparent+14" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Tint;
            float _Opacity, _Intensity, _OuterShell, _ShellWidth;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; };
            float hash(float3 p) { p=frac(p*.3183099+.1); p*=17; return frac(p.x*p.y*p.z*(p.x+p.y+p.z)); }
            float noise(float3 p)
            {
                float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+float3(1,1,1)),f.x),f.y),f.z);
            }
            v2f vert(appdata v)
            {
                v2f o;
                float3 w=mul(unity_ObjectToWorld,v.vertex).xyz;
                float3 n=UnityObjectToWorldNormal(v.normal);
                float flame=.5+.5*sin(w.x*15+w.z*12+w.y*8-_Time.y*6);
                w+=n*_ShellWidth*_OuterShell*(.35+flame*.65);
                w.y+=_OuterShell*flame*_ShellWidth*1.5;
                o.pos=mul(UNITY_MATRIX_VP,float4(w,1)); o.world=w; o.normal=n; return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float rim=pow(1-saturate(abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)))),1.7);
                float3 flow=i.world*float3(5,2,5)-float3(0,_Time.y*1.7,0);
                float n=noise(flow), fine=noise(flow*2.7+13.1);
                float filaments=pow(saturate(1-abs(sin(i.world.y*13+i.world.x*7+i.world.z*9+n*8-_Time.y*3))),12);
                float core=.28+rim*.7+filaments*.28;
                float outer=rim*smoothstep(.34,.72,n*.7+fine*.3)*.75;
                float alpha=lerp(core,outer,saturate(_OuterShell))*_Opacity;
                float3 col=_Tint.rgb*_Intensity*(.85+rim*.5+filaments*.18);
                return float4(col,saturate(alpha));
            }
            ENDCG
        }
    }
}
