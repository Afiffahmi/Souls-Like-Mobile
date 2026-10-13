Shader "Hidden/Nusantara/Drifting Cloud Shadows"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "WorldCloudNoise.cginc"
            float4 _CloudOffset,_CloudOrigin,_CloudRight,_CloudUp,_CloudForward;
            float _CloudScale,_CloudCoverage,_CloudStrength,_CloudHeight,_CookieSpan;
            half4 frag(v2f_img i):SV_Target
            {
                float3 p=_CloudOrigin.xyz+((i.uv.x-.5)*_CloudRight.xyz+(i.uv.y-.5)*_CloudUp.xyz)*_CookieSpan;
                // Project through the sun's rays onto the same cloud plane used by the sky.
                float y=min(_CloudForward.y,-.08);
                p+=_CloudForward.xyz*((_CloudHeight-p.y)/y);
                float cloud=WorldCloudCover(p.xz,_CloudOffset.xy,_CloudScale,_CloudCoverage);
                float light=1-cloud*_CloudStrength;
                return half4(light,light,light,light);
            }
            ENDCG
        }
    }
    Fallback Off
}
