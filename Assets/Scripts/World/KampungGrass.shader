Shader "Nusantara/Kampung Living Grass"
{
    Properties
    {
        _RootColor("Grass roots", Color) = (.30,.38,.28,1)
        _TipColor("Fresh lime tips", Color) = (.46,.54,.34,1)
        _PatchColor("Cool green patches", Color) = (.35,.46,.32,1)
        _PatchSize("Colour patch metres", Float) = 4
        _Wind("Wind strength", Range(0,.3)) = .09
        _WindSpeed("Wind speed", Range(0,3)) = 1.1
        _ShadeSteps("Lighting bands", Range(2,6)) = 3
        _FadeStart("Fade start metres", Float) = 42
        _FadeEnd("Fade end metres", Float) = 76
        _Density("Quality density", Range(.1,1)) = 1
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "Queue"="Geometry+5"}
        Cull Off
        CGPROGRAM
        // Built-in is the project's active pipeline. Receive real tree shadows
        // and the existing drifting sunlight cookie through surface lighting.
        #pragma surface surf Grass vertex:Wind noforwardadd exclude_path:deferred
        #pragma target 3.0
        #pragma multi_compile_instancing
        fixed4 _RootColor, _TipColor, _PatchColor;
        float _PatchSize, _Wind, _WindSpeed, _ShadeSteps, _FadeStart, _FadeEnd, _Density;
        int _GrassInteractorCount;
        float4 _GrassInteractorPositions[24], _GrassInteractorMotion[24];
        float _GrassInteractionStrength, _GrassInteractionHeight;
        struct Input { float3 worldPos; float4 grass; };
        void Wind(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 root = mul(unity_ObjectToWorld,float4(v.texcoord1.xyz,1)).xyz;
            float2 bend=0;
            [loop] for(int actor=0;actor<min(_GrassInteractorCount,24);actor++)
            {
                float4 body=_GrassInteractorPositions[actor];float4 motion=_GrassInteractorMotion[actor];
                float2 delta=root.xz-body.xz;
                float reach=saturate(1-length(delta)/max(body.w,.1));reach=reach*reach*(3-2*reach);
                reach*=saturate(1-abs(root.y-body.y)/max(_GrassInteractionHeight,.1));
                float2 away=delta/max(length(delta),.08);
                bend+=(away*.55+motion.xy*.65)*reach*motion.z;
            }
            float bendLength=length(bend);bend/=max(1,bendLength);
            // Bend the tip outwards and down; UV height keeps every root planted.
            float tip=v.texcoord.y*v.texcoord.y;
            world.xz+=bend*_GrassInteractionStrength*tip;
            world.y-=min(1,bendLength)*_GrassInteractionStrength*.32*tip;
            float wave = sin(dot(world.xz, float2(.48,.32)) - _Time.y * _WindSpeed * 1.4);
            wave += .25 * sin(dot(world.xz, float2(-.7,.55)) - _Time.y * _WindSpeed * 2.1);
            // Roots stay fixed; broad curved blades survive the pixelation pass.
            world.xz += float2(1,.55) * wave * _Wind * tip;
            v.vertex = mul(unity_WorldToObject, float4(world,1));
            o.grass = float4(v.texcoord.y, v.color.a, distance(world,_WorldSpaceCameraPos), wave);
        }
        half4 LightingGrass(SurfaceOutput s, half3 lightDir, half atten)
        {
            half wrap = saturate(dot(s.Normal,lightDir) * .45 + .55);
            half band = floor(wrap * max(_ShadeSteps-1,1) + .5) / max(_ShadeSteps-1,1);
            return half4(s.Albedo * _LightColor0.rgb * (.3 + .4 * band) * atten, 1);
        }
        void surf(Input i, inout SurfaceOutput o)
        {
            float fade = saturate((_FadeEnd-i.grass.z)/max(1,_FadeEnd-_FadeStart));
            // A constant per blade removes whole blades instead of punching holes.
            clip(fade * _Density - i.grass.y);
            float2 p = i.worldPos.xz / max(.5,_PatchSize);
            float patch = saturate(.5 + .3*sin(p.x+sin(p.y*.8)) + .2*cos(p.y*1.3-p.x*.4));
            patch = floor(patch*3+.5)/3;
            fixed3 tips = lerp(_TipColor.rgb,_PatchColor.rgb,patch*.6);
            float height = floor(saturate(i.grass.x)*3+.5)/3;
            o.Albedo = lerp(_RootColor.rgb,tips,height);
            o.Emission = o.Albedo * (.06 + .03 * height);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
