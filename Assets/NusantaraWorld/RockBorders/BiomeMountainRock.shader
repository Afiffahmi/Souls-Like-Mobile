Shader "Nusantara/Biome Mountain Rock"
{
 Properties {
  _MainTex("Rock detail",2D)="white"{}
  _RegionMap("Region ownership",2D)="white"{}
  _RegionRect("Map bounds",Vector)=(2048,-256,2304,1536)
  _AshAmount("Ash and pale crest detail",Range(0,1))=.75
 }
 SubShader {
  Tags { "RenderType"="Opaque" }
  LOD 250
  CGPROGRAM
  #pragma surface surf Mountain fullforwardshadows vertex:vert addshadow exclude_path:deferred
  #pragma target 3.0
  #pragma multi_compile_instancing
  sampler2D _MainTex,_RegionMap;
  float4 _RegionRect;half _AshAmount;
  struct Input {float2 uv_MainTex;float3 worldPos;float3 worldNormal;float localHeight;float objectHeight;};
  void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.localHeight=v.vertex.y;o.objectHeight=length(unity_ObjectToWorld._m01_m11_m21);}
  float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
  float noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+float3(1,1,1)),f.x),f.y),f.z);}
  half4 LightingMountain(SurfaceOutput s,half3 lightDir,half atten){half ndl=saturate(dot(s.Normal,lightDir));return half4(s.Albedo*(.34+_LightColor0.rgb*(.32+.68*ndl)*atten),s.Alpha);}
  void surf(Input IN,inout SurfaceOutput o){
   float id=floor(tex2D(_RegionMap,(IN.worldPos.xz-_RegionRect.xy)/_RegionRect.zw).a*255+.5);
   float3 t=tex2D(_MainTex,IN.uv_MainTex).rgb;
   float lum=dot(t,float3(.299,.587,.114));float grain=saturate(pow(lum,.55)*1.65);
   float n=noise(IN.worldPos*.16),fine=noise(IN.worldPos*.74);
   float3 rock=float3(.15,.17,.18),cover=float3(.19,.29,.09);float moss=.75,ash=.14;
   if(id>1.5&&id<2.5){rock=float3(.10,.16,.19);cover=float3(.10,.26,.23);moss=.52;ash=.20;}
   if(id>2.5&&id<3.5){rock=float3(.10,.105,.11);cover=float3(.42,.42,.40);moss=0;ash=.9;}
   if(id>3.5&&id<4.5){rock=float3(.15,.18,.12);cover=float3(.20,.33,.075);moss=.88;ash=.10;}
   if(id>4.5&&id<5.5){rock=float3(.25,.205,.155);cover=float3(.35,.29,.20);moss=.12;ash=.42;}
   if(id>5.5&&id<6.5){rock=float3(.13,.18,.16);cover=float3(.20,.32,.10);moss=.85;ash=.16;}
   if(id>6.5){rock=float3(.15,.13,.20);cover=float3(.30,.29,.37);moss=.04;ash=.55;}
   float3 base=rock*(.50+grain*1.45)*( .85+fine*.30);
   float up=saturate(IN.worldNormal.y);
   float green=saturate((up*.9+n*.7-.4))*moss*(1-smoothstep(.65,.95,IN.localHeight));
   base=lerp(base,cover*(.7+grain*.65),green);
   float snowline=lerp(.92,.58,saturate((IN.objectHeight-28)/48));
   float cap=smoothstep(snowline-.1,snowline+.13,IN.localHeight+n*.18-.09)*saturate(up*1.4+.15)*ash*_AshAmount;
   float streak=smoothstep(.53,.82,grain+n*.25)*smoothstep(.35,.85,IN.localHeight)*ash*.30;
   base=lerp(base,float3(.55,.57,.57)*(.7+fine*.4),saturate(cap+streak));
   o.Albedo=base;o.Alpha=1;o.Specular=0;o.Gloss=0;
  }
  ENDCG
 }
 FallBack "Standard"
}

