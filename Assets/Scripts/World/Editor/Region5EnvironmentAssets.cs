using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Nusantara.World.Editor
{
    // Small shared opaque meshes, rather than hundreds of per-instance primitives/materials.
    public static class Region5EnvironmentAssets
    {
        public const string Folder = "Assets/Models/Map/Props/Region5/Enhancement";
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
        public static void Create()
        {
            EnsureFolder(Folder);
            Build("moss_rock", new Color(.25f,.32f,.20f), g => Mound(g, Vector3.zero, new Vector3(4,2.5f,3), 9));
            Build("fern_cluster", new Color(.13f,.36f,.14f), g =>
            {
                for(int i=0;i<9;i++)
                {
                    float a=i*Mathf.PI*2/9;
                    Vector3 direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    for(int k=0;k<5;k++)
                    {
                        float t=(k+1)/5f;var p=direction*t*1.5f+Vector3.up*Mathf.Sin(t*Mathf.PI)*.8f;
                        var side=Vector3.Cross(direction,Vector3.up)*(.42f*(1-t)+.1f);
                        Leaf(g,p-side,p+direction*.4f,p+side,.08f);
                    }
                }
            });
            Build("broadleaf_bush",new Color(.19f,.40f,.16f),g =>
            {
                for(int i=0;i<10;i++) {float a=i*2.4f;var p=new Vector3(Mathf.Sin(a),.3f+i*.08f,Mathf.Cos(a))*.8f;
                    Leaf(g,p,new Vector3(Mathf.Sin(a)*1.8f,1.5f,Mathf.Cos(a)*1.8f),p+new Vector3(Mathf.Cos(a)*.6f,0,-Mathf.Sin(a)*.6f),.18f);}
            });
            Build("river_reeds",new Color(.40f,.43f,.16f),g =>
            {
                for(int i=0;i<12;i++) {float a=i*2.4f;var p=new Vector3(Mathf.Sin(a)*.7f,0,Mathf.Cos(a)*.7f);float h=1.3f+(i%4)*.3f;
                    g.Box(p+Vector3.up*h/2,new Vector3(.055f,h,.055f),Quaternion.Euler(0,i*20,(i%3-1)*8));
                    Leaf(g,p+Vector3.up*.5f,p+new Vector3(.5f,h*.8f,.3f),p+Vector3.up*.9f,.04f);}
            });
            Build("guardian_shrine",new Color(.33f,.38f,.29f),g =>
            {
                g.Box(new Vector3(0,.3f,0),new Vector3(7,.6f,6),Quaternion.identity);
                g.Box(new Vector3(0,1.2f,0),new Vector3(3,1.8f,2.5f),Quaternion.identity);
                foreach(float x in new[]{-2.7f,2.7f})g.Box(new Vector3(x,3,1),new Vector3(.8f,6,.8f),Quaternion.Euler(0,0,x*2));
                g.Box(new Vector3(0,6,1),new Vector3(7,.8f,1.4f),Quaternion.identity);
                Mound(g,new Vector3(0,2.1f,0),new Vector3(1.3f,1.5f,1),8);
            });
            Build("crocodile_nest",new Color(.38f,.29f,.16f),g =>
            {
                for(int i=0;i<16;i++) {float a=i*Mathf.PI*2/16;g.Box(new Vector3(Mathf.Cos(a)*2.8f,.15f,Mathf.Sin(a)*2.2f),new Vector3(1.4f,.3f,.3f),Quaternion.Euler(0,-a*Mathf.Rad2Deg+90,5));}
                for(int i=0;i<5;i++)Mound(g,new Vector3((i%3-1)*.8f,.05f,(i/3-.5f)*.8f),new Vector3(.35f,.5f,.45f),8);
            });
            Build("small_rib_bones",new Color(.68f,.64f,.48f),g =>
            {
                g.Box(new Vector3(0,.25f,0),new Vector3(.3f,.3f,9),Quaternion.identity);
                for(int i=0;i<6;i++)for(int side=-1;side<=1;side+=2)
                    g.Box(new Vector3(side*1.2f,.65f,-3.8f+i*1.5f),new Vector3(2.7f,.22f,.25f),Quaternion.Euler(0,0,side*26));
            });
            Build("fishing_net",new Color(.47f,.37f,.21f),g =>
            {
                foreach(float x in new[]{-2f,2f})g.Box(new Vector3(x,1.7f,0),new Vector3(.14f,3.4f,.14f),Quaternion.identity);
                for(int i=0;i<9;i++)g.Box(new Vector3(-2+i*.5f,1.8f,0),new Vector3(.035f,2.3f,.035f),Quaternion.identity);
                for(int i=0;i<7;i++)g.Box(new Vector3(0,.7f+i*.35f,0),new Vector3(4,.03f,.03f),Quaternion.identity);
            });
            Build("market_stall",new Color(.44f,.26f,.12f),g =>
            {
                foreach(float x in new[]{-2f,2f})foreach(float z in new[]{-1.4f,1.4f})g.Box(new Vector3(x,1.7f,z),new Vector3(.16f,3.4f,.16f),Quaternion.identity);
                g.Box(new Vector3(0,1.05f,0),new Vector3(4.5f,.2f,2.7f),Quaternion.identity);
                g.Box(new Vector3(0,3.5f,0),new Vector3(5.2f,.25f,4),Quaternion.Euler(0,0,5));
            });
            Build("hanging_vines",new Color(.15f,.26f,.11f),g =>
            {
                for(int i=0;i<6;i++) {float h=3+i%3;g.Box(new Vector3(i*.35f,h/2,Mathf.Sin(i)*.15f),new Vector3(.065f,h,.065f),Quaternion.Euler(0,0,(i%3-1)*7));
                    for(int k=0;k<4;k++)Leaf(g,new Vector3(i*.35f,k*.7f,0),new Vector3(i*.35f+.4f,k*.7f+.3f,.15f),new Vector3(i*.35f,k*.7f+.4f,0),.04f);}
            });
            Build("cave_crystals",new Color(.08f,.60f,.65f),g =>
            {
                for(int i=0;i<5;i++)Mound(g,new Vector3(Mathf.Sin(i*2.4f),0,Mathf.Cos(i*2.4f)),new Vector3(.45f,1.4f+(i%3)*.5f,.45f),5);
            }, true);
            Build("forest_mushrooms",new Color(.55f,.34f,.16f),g =>
            {
                for(int i=0;i<5;i++) {var p=new Vector3(Mathf.Sin(i*2.4f)*.6f,0,Mathf.Cos(i*2.4f)*.6f);
                    g.Box(p+Vector3.up*.18f,new Vector3(.09f,.36f,.09f),Quaternion.identity);Mound(g,p+Vector3.up*.3f,new Vector3(.3f,.16f,.3f),8);}
            });
            Build("scenic_island",new Color(.52f,.48f,.30f),g => Island(g,24));
            AssetDatabase.SaveAssets();
        }
        static void Leaf(WorldGenerationGeometry g,Vector3 a,Vector3 b,Vector3 c,float rise)
        {
            var middle=(a+b+c)/3+Vector3.up*rise;
            g.Quad(a,middle,b,b);g.Quad(b,middle,c,c);g.Quad(c,middle,a,a);
            g.Quad(b,middle,a,a);g.Quad(c,middle,b,b);g.Quad(a,middle,c,c);
        }
        public static void Mound(WorldGenerationGeometry g,Vector3 center,Vector3 size,int sides)
        {
            Vector3 Ring(int i,float radius,float y)
            {float a=i*Mathf.PI*2/sides;float irregular=1+.10f*Mathf.Sin(i*3.7f);return center+Vector3.Scale(new Vector3(Mathf.Cos(a)*radius*irregular,y,Mathf.Sin(a)*radius*irregular),size);}
            for(int i=0;i<sides;i++)
            {
                g.Quad(Ring(i,1,0),Ring(i,.7f,.7f),Ring(i+1,.7f,.7f),Ring(i+1,1,0));
                g.Quad(Ring(i,.7f,.7f),center+Vector3.up*size.y,Ring(i+1,.7f,.7f),Ring(i+1,.7f,.7f));
            }
        }
        static void Island(WorldGenerationGeometry g,int sides)
        {
            Vector3 P(int i,float radius,float height)
            {float a=i*Mathf.PI*2/sides;float r=radius*(.88f+.09f*Mathf.Sin(a*3+.7f)+.025f*Mathf.Cos(a*7));return new Vector3(Mathf.Cos(a)*r,height,Mathf.Sin(a)*r);}
            for(int i=0;i<sides;i++)
            {
                g.Quad(P(i,1,-.04f),P(i,.82f,.07f),P(i+1,.82f,.07f),P(i+1,1,-.04f));
                g.Quad(P(i,.82f,.07f),P(i,.46f,.28f),P(i+1,.46f,.28f),P(i+1,.82f,.07f));
                g.Quad(P(i,.46f,.28f),new Vector3(0,.32f,0),P(i+1,.46f,.28f),P(i+1,.46f,.28f));
            }
        }
        static void Build(string name,Color color,Action<WorldGenerationGeometry> build,bool emissive=false)
        {
            string path=Folder+"/"+name+".prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)return;
            var mat=new Material(Shader.Find(WorldAssetLayout.LitShader)){name="Region5 "+name,enableInstancing=true};
            WorldAssetLayout.SetColor(mat,color);
            if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",.1f);
            if(emissive) {mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*1.4f);}
            AssetDatabase.CreateAsset(mat,WorldAssetLayout.Materials+"/Region5_detail_"+name+".mat");
            var geometry=new WorldGenerationGeometry();build(geometry);var mesh=geometry.Mesh(name);
            AssetDatabase.CreateAsset(mesh,Folder+"/"+name+".asset");
            var low=WorldGenerationGeometry.Simplify(mesh,6);AssetDatabase.CreateAsset(low,Folder+"/"+name+"_LOD.asset");
            var root=new GameObject(name);var renderers=new List<Renderer>();
            foreach(var m in new[]{mesh,low})
            {
                var child=new GameObject(renderers.Count==0?"Detail":"Distance");child.transform.SetParent(root.transform,false);
                child.AddComponent<MeshFilter>().sharedMesh=m;var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;
                renderer.shadowCastingMode=name=="guardian_shrine"&&renderers.Count==0?ShadowCastingMode.On:ShadowCastingMode.Off;
                renderer.receiveShadows=true;renderers.Add(renderer);
            }
            var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.04f,new[]{renderers[0]}),new LOD(.008f,new[]{renderers[1]})});lod.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);
        }
        public static Material RefreshCanopyMaterial()
        {
            const string path="Assets/Materials/Region5_canopy_vfx.mat";
            const string texturePath="Assets/Textures/Region5_canopy_leaf_clump.asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(texture==null)
            {
                var source=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/VFX/Particle System/Assets/leaf.png");
                if(source==null)throw new InvalidOperationException("The project's leaf sprite is missing.");
                // Read back without changing the source texture's import flags. A shared clump
                // sprite retains the leaf artwork, with many leaves per particle and irregular edges.
                var temporary=RenderTexture.GetTemporary(source.width,source.height,0);var previous=RenderTexture.active;
                var readable=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
                try {Graphics.Blit(source,temporary);RenderTexture.active=temporary;readable.ReadPixels(new Rect(0,0,source.width,source.height),0,0);readable.Apply();}
                finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temporary);}
                const int side=256;var pixels=new Color[side*side];
                for(int leaf=0;leaf<32;leaf++)
                {
                    float a=leaf*2.39996f;float reach=Mathf.Sqrt((leaf+.5f)/32f)*.30f;
                    Vector2 center=new Vector2(.5f+Mathf.Cos(a)*reach,.5f+Mathf.Sin(a)*reach);
                    float width=.16f+(leaf%4)*.025f,height=width*1.4f;
                    float angle=a+.6f*Mathf.Sin(leaf*1.7f),cos=Mathf.Cos(angle),sin=Mathf.Sin(angle);
                    float extent=height*.65f;int x0=Mathf.Max(0,(int)((center.x-extent)*side)),x1=Mathf.Min(side-1,(int)((center.x+extent)*side));
                    int y0=Mathf.Max(0,(int)((center.y-extent)*side)),y1=Mathf.Min(side-1,(int)((center.y+extent)*side));
                    var tint=Color.Lerp(new Color(.12f,.25f,.075f),new Color(.38f,.52f,.18f),(leaf%7)/6f);
                    for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                    {
                        float dx=(x+.5f)/side-center.x,dy=(y+.5f)/side-center.y;
                        float u=(cos*dx+sin*dy)/width+.5f,v=(-sin*dx+cos*dy)/height+.5f;
                        if(u<0||v<0||u>1||v>1)continue;var sample=readable.GetPixelBilinear(u,v);if(sample.a<.01f)continue;
                        int index=y*side+x;Color next=tint*(.65f+sample.g*.4f);next.a=sample.a;
                        Color old=pixels[index];float alpha=next.a+old.a*(1-next.a);
                        pixels[index]=new Color((next.r*next.a+old.r*old.a*(1-next.a))/alpha,(next.g*next.a+old.g*old.a*(1-next.a))/alpha,(next.b*next.a+old.b*old.a*(1-next.a))/alpha,alpha);
                    }
                }
                Object.DestroyImmediate(readable);texture=new Texture2D(side,side,TextureFormat.RGBA32,true){name="Region5 leaf VFX clump",wrapMode=TextureWrapMode.Clamp};
                texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,texturePath);
            }
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find(WorldAssetLayout.UsesURP?"Universal Render Pipeline/Particles/Unlit":"Particles/Standard Unlit"));AssetDatabase.CreateAsset(material,path);}
            material.shader=Shader.Find(WorldAssetLayout.UsesURP?"Universal Render Pipeline/Particles/Unlit":"Particles/Standard Unlit");
            WorldAssetLayout.SetTexture(material,texture);WorldAssetLayout.SetColor(material,Color.white);
            material.shaderKeywords=Array.Empty<string>();material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Mode",1);material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.35f);
            material.SetFloat("_Surface",0);material.SetFloat("_SrcBlend",(int)BlendMode.One);material.SetFloat("_DstBlend",(int)BlendMode.Zero);material.SetFloat("_ZWrite",1);
            material.renderQueue=(int)RenderQueue.AlphaTest;material.SetOverrideTag("RenderType","TransparentCutout");EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();return material;
        }
    }
}
