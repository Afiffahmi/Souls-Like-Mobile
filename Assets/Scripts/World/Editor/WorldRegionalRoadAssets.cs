using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nusantara.World.Editor
{
    public static class WorldRegionalRoadAssets
    {
        public const string Root=WorldSurfacePaint.Root+"/RegionalRoads";
        static readonly string[] elements={"Common","Natural start","Water","Fire","Nature","Earth","Nature + Water","Lightning","Wind","Darkness"};
        static readonly string[] folders={"Common","Region1_NaturalStart","Region1_Water","Region2_Fire","Region3_Nature","Region4_Earth","Region5_NatureWater","Region6_Lightning","Region7_Wind","Region8_Darkness"};
        static readonly string[] kinds={"TravelRoad","NaturalTrail","StonePath"};
        static readonly string[] labels={"Travel road","Natural trail","Stone path"};
        public static string RegionLabel(int region)=>region==0?"Common surfaces":$"Region {region} — {elements[Mathf.Clamp(region,0,9)]}";
        public static string Folder(int region)=>region==0?WorldSurfacePaint.Root:Root+"/"+folders[Mathf.Clamp(region,1,9)];

        struct Theme
        {
            public Color soil, stone, accent, joint;
            public Theme(string soil,string stone,string accent,string joint)
            {this.soil=Hex(soil);this.stone=Hex(stone);this.accent=Hex(accent);this.joint=Hex(joint);}
        }
        static Color Hex(string text){ColorUtility.TryParseHtmlString("#"+text,out var color);return color;}
        static Theme GetTheme(int region)
        {
            if(region==1)return new Theme("8A7653","AAA28B","668B45","4E503C");
            switch(region-1)
            {
                case 1:return new Theme("627F82","8BA7AB","4CA6A2","354F54");
                case 2:return new Theme("724B40","625450","D3783E","352F32");
                case 3:return new Theme("756345","8C9170","577B3B","384332");
                case 4:return new Theme("A3895E","C0AC81","887244","5B503D");
                case 5:return new Theme("566348","79816B","438F78","303F37");
                case 6:return new Theme("626477","8C8CA4","D8C979","363849");
                case 7:return new Theme("A4B3AD","CDD3BE","91BFCC","6A8187");
                default:return new Theme("493C54","665771","9765AA","252131");
            }
        }
        static int Wrap(int value,int period)=>(value%period+period)%period;
        static float Hash(int x,int y,int seed)
        {
            unchecked{uint h=(uint)x*374761393u+(uint)y*668265263u+(uint)seed*2246822519u;h=(h^(h>>13))*1274126177u;return ((h^(h>>16))&0xffffff)/16777215f;}
        }
        static float Noise(float u,float v,int frequency,int seed)
        {
            float x=u*frequency,y=v*frequency;int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);
            float tx=x-ix,ty=y-iy;tx=tx*tx*(3-2*tx);ty=ty*ty*(3-2*ty);
            float H(int a,int b)=>Hash(Wrap(a,frequency),Wrap(b,frequency),seed);
            return Mathf.Lerp(Mathf.Lerp(H(ix,iy),H(ix+1,iy),tx),Mathf.Lerp(H(ix,iy+1),H(ix+1,iy+1),tx),ty);
        }
        static float Fbm(float u,float v,int seed)=>Noise(u,v,4,seed)*.5f+Noise(u,v,8,seed+1)*.27f+Noise(u,v,16,seed+2)*.15f+Noise(u,v,32,seed+3)*.08f;
        static float Smooth(float low,float high,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(low,high,value));
        static float Cells(float u,float v,int frequency,int seed,out float shade)
        {
            float x=u*frequency,y=v*frequency;int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);float first=100,second=100;shade=0;
            for(int oy=-1;oy<=1;oy++)for(int ox=-1;ox<=1;ox++)
            {
                int a=ix+ox,b=iy+oy;float px=a+.2f+.6f*Hash(Wrap(a,frequency),Wrap(b,frequency),seed);
                float py=b+.2f+.6f*Hash(Wrap(a,frequency),Wrap(b,frequency),seed+31);
                float d=(x-px)*(x-px)+(y-py)*(y-py);
                if(d<first){second=first;first=d;shade=Hash(Wrap(a,frequency),Wrap(b,frequency),seed+77);}else if(d<second)second=d;
            }
            return Mathf.Sqrt(second)-Mathf.Sqrt(first);
        }
        public static Color Sample(int region,int kind,float u,float v)
        {
            var theme=GetTheme(region);int seed=region*107+kind*17;
            float noise=Fbm(u,v,seed),grain=Noise(u,v,96,seed+70),joint=0;
            Color color;
            if(kind==0)
            {
                float gap=Cells(u,v,6,seed,out float shade);
                float stone=Smooth(.035f,.12f,gap);joint=1-stone;
                color=Color.Lerp(theme.soil*.72f,Color.Lerp(theme.soil,theme.stone,.62f)*(.8f+shade*.3f),stone);
            }
            else if(kind==1)
            {
                color=theme.soil*(.85f+noise*.3f);
                float gap=Cells(u,v,10,seed,out float shade);
                float pebble=Smooth(.3f,.5f,gap)*Smooth(.4f,.7f,shade);
                color=Color.Lerp(color,theme.stone,pebble*.75f);joint=Smooth(.43f,.65f,noise)*.55f;
            }
            else
            {
                float y=v*4;int row=Mathf.FloorToInt(y);float x=u*4+(Wrap(row,2)==0?0:.5f);
                float fx=Mathf.Repeat(x,1),fy=Mathf.Repeat(y,1);
                float edge=Mathf.Min(Mathf.Min(fx,1-fx),Mathf.Min(fy,1-fy));
                float stone=Smooth(.02f,.075f,edge);joint=1-stone;
                float shade=Hash(Wrap(Mathf.FloorToInt(x),4),Wrap(row,4),seed);
                color=Color.Lerp(theme.joint,theme.stone*(.84f+shade*.26f),stone);
            }
            float tau=Mathf.PI*2;
            float vein=1-Smooth(.018f,.09f,Mathf.Abs(Mathf.Sin(tau*(u*3+v*2)+Mathf.Sin(tau*v*4)*.55f)));
            float accent;
            switch(region-1)
            {
                case 0:accent=joint*.2f+Smooth(.5f,.7f,noise)*.12f;break;
                case 1:accent=joint*Smooth(.3f,.64f,noise)*.65f;break;
                case 2:accent=joint*Smooth(.43f,.63f,noise)*.85f;break;
                case 3:accent=Smooth(.44f,.64f,noise)*.7f+joint*.25f;break;
                case 4:accent=joint*.12f+vein*.12f;break;
                case 5:accent=Smooth(.48f,.66f,noise)*.55f+joint*.4f;break;
                case 6:accent=vein*Smooth(.27f,.5f,noise)*.72f;break;
                case 7:accent=(.5f+.5f*Mathf.Sin(tau*u*4+Mathf.Sin(tau*v*2)*1.5f))*.18f+joint*.3f;break;
                default:accent=joint*Smooth(.28f,.64f,noise)*.7f+vein*.28f;break;
            }
            color=Color.Lerp(color,theme.accent,Mathf.Clamp01(accent));
            color*=.91f+noise*.14f+(grain-.5f)*.07f;color.a=1;return color;
        }
        static Texture2D Texture(int region,int kind,string path)
        {
            if(!File.Exists(path))
            {
                const int size=512;
                var texture=new Texture2D(size,size,TextureFormat.RGB24,false);
                try
                {
                    var pixels=new Color[size*size];
                    for(int y=0;y<size;y++)for(int x=0;x<size;x++)pixels[y*size+x]=Sample(region,kind,x/(float)(size-1),y/(float)(size-1));
                    texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
                }
                finally{UnityEngine.Object.DestroyImmediate(texture);}
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            }
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Texture could not be imported: "+path);
            bool change=!importer.isReadable||importer.wrapMode!=TextureWrapMode.Repeat||!importer.mipmapEnabled||!importer.sRGBTexture||importer.textureCompression!=TextureImporterCompression.Uncompressed;
            if(change)
            {
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.isReadable=true;
                importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Bilinear;importer.mipmapEnabled=true;
                importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaSource=TextureImporterAlphaSource.None;importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        [MenuItem("Tools/Nusantara/Road Assets/Create Regional Road Assets (1-9)")]
        public static void CreateFromMenu(){Debug.Log(Ensure());WorldRoadBrushWindow.Open();}
        public static string Ensure()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode before creating road assets.");
            var palette=WorldSurfacePaint.EnsurePalette();int added=0;
            var shader=Shader.Find("Nusantara/World Scale Road");
            if(shader==null)throw new InvalidOperationException("The road material shader is unavailable.");
            for(int region=1;region<=9;region++)
            {
                string folder=Folder(region);Region5EnvironmentAssets.EnsureFolder(folder);
                for(int kind=0;kind<3;kind++)
                {
                    var texture=Texture(region,kind,folder+"/"+kinds[kind]+".png");
                    AssetDatabase.SetLabels(texture,new[]{"NusantaraRoad","Region"+region,elements[region].Replace(" + ","")});
                    string id="nusantara-road-r"+region+"-"+kinds[kind];
                    if(!palette.styles.Any(s=>s!=null&&s.assetId==id))
                    {
                        palette.styles.Add(new WorldSurfaceStyle{assetId=id,region=region,name=elements[region]+" — "+labels[kind],color=Color.white,texture=texture,variation=.04f,patternSize=kind==1?.8f:2});added++;
                    }
                    string matPath=folder+"/"+kinds[kind]+".mat";
                    if(AssetDatabase.LoadAssetAtPath<Material>(matPath)==null)
                    {
                        var material=new Material(shader){name=$"R{region} {elements[region]} {labels[kind]}"};
                        material.SetTexture("_MainTex",texture);material.SetColor("_Color",Color.white);material.SetFloat("_Glossiness",region==2||region==6?.22f:.08f);
                        material.SetFloat("_RoadTile",kind==1?.8f:2);
                        material.SetFloat("_Metallic",0);material.enableInstancing=true;AssetDatabase.CreateAsset(material,matPath);
                    }
                }
            }
            EditorUtility.SetDirty(palette);AssetDatabase.SaveAssetIfDirty(palette);
            foreach(var window in Resources.FindObjectsOfTypeAll<WorldRoadBrushWindow>())window.RefreshRegionalAssets();
            return $"Ready: 9 regional road groups, 27 seamless textures and 27 materials. Added {added} brush presets. Existing presets and painted ground preserved.";
        }
    }
}
