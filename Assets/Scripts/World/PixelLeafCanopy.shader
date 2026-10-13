Shader "Nusantara/Pixel Leaf Canopy"
{
    Properties
    {
        _MainTex("Point filtered leaf clusters", 2D) = "white" {}
        _Color("Leaf colour", Color) = (.52,.70,.24,1)
        _ShadowColor("Cool canopy shade", Color) = (.22,.45,.32,1)
        _Wind("Leaf sway metres", Range(0,.6)) = .18
        _WindSpeed("Leaf sway speed", Range(0,3)) = .8
        _Cutoff("Leaf cutout", Range(0,1)) = .5
        _ShadeSteps("Lighting bands", Range(2,6)) = 3
    }
    SubShader
    {
        Tags {"RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        Cull Off
        CGPROGRAM
        #pragma surface surf Leaves vertex:Sway addshadow noforwardadd exclude_path:deferred
        #pragma target 3.0
        #pragma multi_compile_instancing
        sampler2D _MainTex;
        fixed4 _Color, _ShadowColor;
        float _Wind, _WindSpeed, _Cutoff, _ShadeSteps;
        struct Input { float2 uv_MainTex; float4 color:COLOR; };
        void Sway(inout appdata_full v)
        {
            float3 world = mul(unity_ObjectToWorld,v.vertex).xyz;
            float wave = sin(dot(world.xz,float2(.45,.28))-_Time.y*_WindSpeed*1.4);
            wave += .2*sin(world.z*1.2+_Time.y*_WindSpeed*2);
            world.xz += float2(1,.55)*wave*_Wind*v.color.a;
            v.vertex = mul(unity_WorldToObject,float4(world,1));
        }
        half4 LightingLeaves(SurfaceOutput s, half3 lightDir, half atten)
        {
            half wrap = saturate(dot(s.Normal,lightDir)*.4+.6);
            half band = floor(wrap*max(_ShadeSteps-1,1)+.5)/max(_ShadeSteps-1,1);
            return half4(s.Albedo*_LightColor0.rgb*(.3+.4*band)*atten,1);
        }
        void surf(Input i, inout SurfaceOutput o)
        {
            fixed4 leaf = tex2D(_MainTex,i.uv_MainTex);
            clip(leaf.a-_Cutoff);
            o.Albedo = lerp(_ShadowColor.rgb,_Color.rgb,i.color.r)*leaf.rgb;
            o.Emission = o.Albedo*.12;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Transparent/Cutout/Diffuse"
}
