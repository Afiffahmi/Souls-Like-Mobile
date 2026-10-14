Shader "Nusantara/Kampung Blended Ground"
{
    Properties
    {
        _Forest("Forest leaf litter",2D)="white"{}
        _ForestNormal("Forest normal",2D)="bump"{}
        _Mud("Damp soil",2D)="white"{}
        _MudNormal("Damp soil normal",2D)="bump"{}
        _Bank("Sand and river pebbles",2D)="white"{}
        _BankNormal("Riverbank normal",2D)="bump"{}
        _ForestRough("Forest roughness",2D)="white"{}
        _MudRough("Mud roughness",2D)="white"{}
        _BankRough("Riverbank roughness",2D)="white"{}
        _Original("Original regional ground",2D)="white"{}
        _OriginalColor("Original ground color",Color)=(.28,.38,.2,1)
        _Tint("Village ground tint",Color)=(1,1,1,1)
        _Tile("Texture metres",Float)=3
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        LOD 250
        CGPROGRAM
        #pragma surface surf Nusantara fullforwardshadows
        #pragma target 3.0
        #include "NusantaraToonBRDF.cginc"
        #pragma multi_compile_instancing
        sampler2D _Forest,_ForestNormal,_Mud,_MudNormal,_Bank,_BankNormal,_ForestRough,_MudRough,_BankRough,_Original;
        fixed4 _OriginalColor,_Tint;
        float _Tile;
        struct Input {float3 worldPos;float4 color:COLOR;};
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            float2 uv=i.worldPos.xz/max(.5,_Tile);
            float wet=saturate(i.color.r),worn=saturate(i.color.g),shade=saturate(i.color.b),area=saturate(i.color.a);
            float macro=.88+.10*sin(i.worldPos.x*.029+sin(i.worldPos.z*.017)*2)+.07*cos(i.worldPos.z*.041);
            fixed3 forest=tex2D(_Forest,uv).rgb;
            fixed3 mud=tex2D(_Mud,uv*.85).rgb;
            fixed3 bank=tex2D(_Bank,uv*1.15).rgb;
            float mudMix=saturate(wet*.7+worn*.75);
            float bankMix=saturate(wet*(1-shade)*.65-worn*.35);
            fixed3 ground=lerp(lerp(forest,mud,mudMix),bank,bankMix)*macro*_Tint.rgb;
            fixed3 original=tex2D(_Original,i.worldPos.xz/32).rgb*_OriginalColor.rgb;
            o.Albedo=lerp(original,ground,area);
            float3 normal=normalize(lerp(UnpackNormal(tex2D(_ForestNormal,uv)),UnpackNormal(tex2D(_MudNormal,uv*.85)),mudMix));
            normal=normalize(lerp(normal,UnpackNormal(tex2D(_BankNormal,uv*1.15)),bankMix));
            o.Normal=normalize(lerp(float3(0,0,1),normal,area*.55));
            float rough=lerp(lerp(tex2D(_ForestRough,uv).r,tex2D(_MudRough,uv*.85).r,mudMix),tex2D(_BankRough,uv*1.15).r,bankMix);
            o.Metallic=0;o.Smoothness=lerp(.12,(1-rough)*.3+wet*.08,area);o.Occlusion=1-shade*area*.12;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
