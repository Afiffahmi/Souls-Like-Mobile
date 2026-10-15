using System;
using UnityEditor;
using UnityEngine;

namespace Nusantara.World.Editor
{
    public static class WorldSurfaceDetails
    {
        const string Folder=WorldSurfacePaint.Root+"/Details";
        const string Path=Folder+"/Library.asset";
        public static WorldSurfaceDetailLibrary Library()
        {
            var data=AssetDatabase.LoadAssetAtPath<WorldSurfaceDetailLibrary>(Path);
            if(data!=null)return data;
            Region5EnvironmentAssets.EnsureFolder(Folder);
            data=ScriptableObject.CreateInstance<WorldSurfaceDetailLibrary>();AssetDatabase.CreateAsset(data,Path);return data;
        }
        public static int Slot(WorldSurfaceStyle style)
        {
            var data=Library();
            if(string.IsNullOrEmpty(style.assetId))style.assetId="custom-"+Guid.NewGuid().ToString("N");
            int index=data.entries.FindIndex(s=>s.assetId==style.assetId);
            bool changed=index<0;
            if(index<0)
            {
                if(data.entries.Count>=254)throw new InvalidOperationException("Surface detail library is full (254 styles).");
                index=data.entries.Count;data.entries.Add(new WorldSurfaceStyle());
            }
            var entry=data.entries[index];
            changed|=entry.texture!=style.texture||entry.color!=style.color||entry.patternSize!=style.patternSize||entry.variation!=style.variation;
            if(changed)
            {
                data.entries[index]=new WorldSurfaceStyle{assetId=style.assetId,region=style.region,name=style.name,color=style.color,texture=style.texture,patternSize=style.patternSize,variation=style.variation};
                Rebuild(data);
            }
            return index+1;
        }
        public static void Rebuild(WorldSurfaceDetailLibrary data)
        {
            const int size=256;
            var array=new Texture2DArray(size,size,Mathf.Max(1,data.entries.Count),TextureFormat.RGBA32,true,false){name="Road detail textures",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            try
            {
                for(int layer=0;layer<data.entries.Count;layer++)
                {
                    var style=data.entries[layer];var pixels=new Color[size*size];
                    if(style.texture!=null&&!style.texture.isReadable)throw new InvalidOperationException("Enable Read/Write on "+style.texture.name);
                    for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                    {var c=style.texture!=null?style.texture.GetPixelBilinear((x+.5f)/size,(y+.5f)/size):Color.white;c*=style.color;c.a=1;pixels[y*size+x]=c;}
                    array.SetPixels(pixels,layer);
                }
                array.Apply(true,false);
                if(data.textures==null){AssetDatabase.CreateAsset(array,Folder+"/Textures.asset");data.textures=array;array=null;}
                else{EditorUtility.CopySerialized(array,data.textures);EditorUtility.SetDirty(data.textures);}
                if(data.parameters==null)
                {
                    data.parameters=new Texture2D(256,1,TextureFormat.RGBAFloat,false,true){name="Road detail scales",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                    AssetDatabase.CreateAsset(data.parameters,Folder+"/Parameters.asset");
                }
                var values=new Color[256];
                for(int i=0;i<data.entries.Count;i++)values[i+1]=new Color(Mathf.Max(.1f,data.entries[i].patternSize),data.entries[i].variation,0,1);
                data.parameters.SetPixels(values);data.parameters.Apply(false);EditorUtility.SetDirty(data.parameters);EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data.textures);AssetDatabase.SaveAssetIfDirty(data.parameters);AssetDatabase.SaveAssetIfDirty(data);
            }
            finally{if(array!=null)UnityEngine.Object.DestroyImmediate(array);}
        }
        public static Texture2D Prepare(Material material,Texture2D legacy)
        {
            var data=Library();if(data.textures==null)throw new InvalidOperationException("Choose a surface first.");
            var map=material.GetTexture("_SurfaceDetailControl") as Texture2D;
            string path=System.IO.Path.ChangeExtension(AssetDatabase.GetAssetPath(legacy),null)+"Details.asset";
            if(map==null)map=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(map==null)
            {
                map=new Texture2D(legacy.width,legacy.height,TextureFormat.RGBA32,false,true){name=legacy.name+" detail coverage",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                map.SetPixels32(new Color32[map.width*map.height]);map.Apply(false);AssetDatabase.CreateAsset(map,path);
            }
            if(material.GetTexture("_SurfaceDetailControl")!=map||material.GetTexture("_SurfaceDetailArray")!=data.textures||material.GetFloat("_SurfaceDetailEnabled")<.5f)
            {
                Undo.RegisterCompleteObjectUndo(material,"Enable fine surface detail");
                material.SetTexture("_SurfaceDetailControl",map);material.SetTexture("_SurfaceDetailArray",data.textures);material.SetTexture("_SurfaceDetailParams",data.parameters);
                material.SetFloat("_SurfaceDetailEnabled",1);EditorUtility.SetDirty(material);
            }
            return map;
        }
        public static Color Blend(Color value,int slot,float amount,bool erase)
        {
            amount=Mathf.Clamp01(amount);
            float a=value.g*(1-amount),b=value.a*(1-amount);
            if(!erase)
            {
                int first=Mathf.RoundToInt(value.r*255),second=Mathf.RoundToInt(value.b*255);
                if(first==slot){a+=amount;}
                else if(second==slot){b+=amount;}
                else if(a<=b){value.r=slot/255f;a+=amount;}
                else{value.b=slot/255f;b+=amount;}
            }
            value.g=a;value.a=b;return value;
        }
        public static bool Stamp(Texture2D map,Rect bounds,Vector2 center,float radius,float strength,float hardness,int slot,bool erase)
        {
            int x0=Mathf.Clamp(Mathf.FloorToInt((center.x-radius-bounds.xMin)/bounds.width*map.width),0,map.width);
            int y0=Mathf.Clamp(Mathf.FloorToInt((center.y-radius-bounds.yMin)/bounds.height*map.height),0,map.height);
            int x1=Mathf.Clamp(Mathf.CeilToInt((center.x+radius-bounds.xMin)/bounds.width*map.width),0,map.width);
            int y1=Mathf.Clamp(Mathf.CeilToInt((center.y+radius-bounds.yMin)/bounds.height*map.height),0,map.height);
            int w=x1-x0,h=y1-y0;if(w<=0||h<=0)return false;
            var pixels=map.GetPixels(x0,y0,w,h);bool changed=false;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                var p=new Vector2(bounds.xMin+(x+x0+.5f)*bounds.width/map.width,bounds.yMin+(y+y0+.5f)*bounds.height/map.height);
                float amount=Mathf.Clamp01(strength)*WorldTerrainBrushMath.Falloff(Vector2.Distance(p,center),radius,hardness);
                if(amount<=0)continue;pixels[y*w+x]=Blend(pixels[y*w+x],slot,amount,erase);changed=true;
            }
            if(changed)map.SetPixels(x0,y0,w,h,pixels);return changed;
        }
    }
}
