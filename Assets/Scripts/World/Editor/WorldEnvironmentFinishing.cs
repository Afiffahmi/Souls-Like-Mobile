using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Nusantara.World.Editor
{
    public static class WorldEnvironmentFinishing
    {
        [MenuItem("Tools/Nusantara/Apply Region 5 Atmosphere")]
        public static string Apply()
        {
            var profile=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            var world=profile.world;
            AddSwampMist(world);
            RefineWaterfall(profile);
            OrientCaveCollision(world);
            AssetDatabase.SaveAssets();
            return "Saved local swamp mist and waterfall rock geometry. No Play mode or tests run.";
        }
        static void OrientCaveCollision(WorldDefinition world)
        {
            var c=world.WorldToChunk(world.locations[6].position);
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>($"{WorldReferenceBuilder.Root}/Chunks/{c.x:D2}_{c.y:D2}/Cave.asset");
            if(mesh==null||mesh.name.Contains("air-facing"))return;
            var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)(triangles[i+1],triangles[i+2])=(triangles[i+2],triangles[i+1]);
            mesh.triangles=triangles;mesh.RecalculateNormals();mesh.name="Gua Sunyi air-facing cave collision";EditorUtility.SetDirty(mesh);
        }
        static void AddSwampMist(WorldDefinition world)
        {
            var location=world.locations[3];var c=world.WorldToChunk(location.position);
            string path=$"{WorldReferenceBuilder.Root}/Chunks/{c.x:D2}_{c.y:D2}/Props.prefab";
            if(!File.Exists(path))return;
            string materialPath=WorldAssetLayout.MaterialPath("SwampMist");
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null)
            {
                var texture=new Texture2D(64,64,TextureFormat.RGBA32,true){name="Soft mist",wrapMode=TextureWrapMode.Clamp};
                var colors=new Color[64*64];
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {
                    float r=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))/31.5f;
                    float a=Mathf.Pow(Mathf.Clamp01(1-r*r),3);colors[y*64+x]=new Color(1,1,1,a);
                }
                texture.SetPixels(colors);texture.Apply();AssetDatabase.CreateAsset(texture,WorldAssetLayout.UtilityTexturePath("SwampMistTexture.asset"));
                material=new Material(Shader.Find(WorldAssetLayout.MistShader)){name="Swamp mist",renderQueue=3000};
                WorldAssetLayout.SetTexture(material,texture);WorldAssetLayout.SetColor(material,new Color(.54f,.66f,.58f,.22f));
                material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);
                if(WorldAssetLayout.UsesURP)material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");
                AssetDatabase.CreateAsset(material,materialPath);
            }
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                if(root.transform.Find("Rawa Berkabus ground mist")!=null)return;
                var fog=new GameObject("Rawa Berkabus ground mist");fog.transform.SetParent(root.transform,false);
                fog.transform.localPosition=location.position-world.ChunkOrigin(c)+Vector3.up*2;
                var ps=fog.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.loop=true;main.prewarm=true;main.duration=20;main.startLifetime=24;main.startSpeed=.35f;main.startSize=new ParticleSystem.MinMaxCurve(22,42);main.startColor=new Color(1,1,1,.35f);main.maxParticles=64;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.playOnAwake=true;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(280,3,280);
                var emission=ps.emission;emission.rateOverTime=2;
                var noise=ps.noise;noise.enabled=true;noise.strength=.8f;noise.frequency=.025f;noise.scrollSpeed=.04f;
                var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        static void RefineWaterfall(WorldGeographyProfile profile)
        {
            var w=profile.world;var location=w.locations[4];var c=w.WorldToChunk(location.position);Vector3 origin=w.ChunkOrigin(c);
            string folder=$"{WorldReferenceBuilder.Root}/Chunks/{c.x:D2}_{c.y:D2}";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/WaterfallCliff.asset");if(old==null)return;
            var geometry=new WorldGenerationGeometry();Vector3 center=location.position-origin;
            Vector3 P(float x,float y,float z)=>new Vector3(center.x+x,y,center.z+z);
            // An irregular rock escarpment, with a high pool feeding the three curtains.
            for(int i=0;i<12;i++)
            {
                float x0=-150+i*25,x1=x0+25;
                float h0=89+Mathf.Abs(x0)*.16f+Mathf.Sin(i*2.7f)*5;
                float h1=89+Mathf.Abs(x1)*.16f+Mathf.Sin((i+1)*2.7f)*5;
                if(Mathf.Abs(x0)<75)h0=89;if(Mathf.Abs(x1)<75)h1=89;
                float z0=110+Mathf.Sin(i*1.3f)*5,z1=110+Mathf.Sin((i+1)*1.3f)*5;
                geometry.Quad(P(x0,31,z0-5),P(x0,h0,z0),P(x1,h1,z1),P(x1,31,z1-5));
                geometry.Quad(P(x0,h0,z0),P(x0,h0+3,150),P(x1,h1+3,150),P(x1,h1,z1));
                geometry.Quad(P(x0,h0+3,150),P(x0,profile.BaseHeight(new Vector2(location.position.x+x0,location.position.z+230)),230),P(x1,profile.BaseHeight(new Vector2(location.position.x+x1,location.position.z+230)),230),P(x1,h1+3,150));
            }
            var mesh=geometry.Mesh("Air Terjun rock escarpment");EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);
            var prefab=PrefabUtility.LoadPrefabContents(folder+"/Essential.prefab");
            try
            {
                if(prefab.transform.Find("Waterfall upper pool")!=null)return;
                var pool=new WorldGenerationGeometry();pool.Quad(P(-70,89.08f,95),P(-70,89.08f,145),P(70,89.08f,145),P(70,89.08f,95));
                var poolMesh=pool.Mesh("Upper waterfall pool");AssetDatabase.CreateAsset(poolMesh,folder+"/UpperPool.asset");
                var go=new GameObject("Waterfall upper pool");go.transform.SetParent(prefab.transform,false);go.AddComponent<MeshFilter>().sharedMesh=poolMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(WorldAssetLayout.MaterialPath("River"));
                // This scenic upper pool is inaccessible; a solid collision enclosure also prevents combat displacements entering it.
                var blocker=go.AddComponent<BoxCollider>();blocker.center=P(0,92,120);blocker.size=new Vector3(140,12,50);
                PrefabUtility.SaveAsPrefabAsset(prefab,folder+"/Essential.prefab");
            }
            finally {PrefabUtility.UnloadPrefabContents(prefab);}
        }
    }
}
