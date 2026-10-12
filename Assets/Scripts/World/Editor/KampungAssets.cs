using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace Nusantara.World.Editor
{
    public static class KampungAssets
    {
        public const string Source="Assets/Models/Map/Props/Region5/KampungSources";
        public const string Prepared=Source+"/Prepared";
        public const string Textures="Assets/Textures/KampungSources";
        static Texture2D Texture(string asset,string channel)=>AssetDatabase.LoadAssetAtPath<Texture2D>($"{Textures}/{asset}_{channel}.jpg");
        public static void Create()
        {
            Region5EnvironmentAssets.EnsureFolder(Prepared);
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Textures}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
                bool normal=path.Contains("_nor_gl");importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=!normal&&!path.Contains("_Rough")&&!path.Contains("_Alpha");importer.mipmapEnabled=true;importer.maxTextureSize=1024;
                importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=4;
                foreach(string platform in new[]{"Android","iPhone"})importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name=platform,overridden=true,maxTextureSize=1024,format=TextureImporterFormat.ASTC_6x6,compressionQuality=50});
                importer.SaveAndReimport();
            }
            var ground=Material("KampungGround","Nusantara/Kampung Blended Ground");
            ground.SetTexture("_Forest",Texture("forest_floor","Diffuse"));ground.SetTexture("_ForestNormal",Texture("forest_floor","nor_gl"));
            ground.SetTexture("_Mud",Texture("brown_mud_leaves_01","Diffuse"));ground.SetTexture("_MudNormal",Texture("brown_mud_leaves_01","nor_gl"));
            ground.SetTexture("_Bank",Texture("forrest_sand_01","Diffuse"));ground.SetTexture("_Original",AssetDatabase.LoadAssetAtPath<Texture2D>(WorldAssetLayout.UtilityTexturePath("GroundGrain.asset")));ground.SetFloat("_Tile",3);
            ground.SetTexture("_BankNormal",Texture("forrest_sand_01","nor_gl"));ground.SetTexture("_ForestRough",Texture("forest_floor","Rough"));ground.SetTexture("_MudRough",Texture("brown_mud_leaves_01","Rough"));ground.SetTexture("_BankRough",Texture("forrest_sand_01","Rough"));
            var grass=Material("KampungGrass","Nusantara/Kampung Living Grass");grass.SetFloat("_Wind",.055f);
            var rock=Material("KampungMossRock",WorldAssetLayout.LitShader);rock.mainTexture=Texture("rock_moss_set_01","Diffuse");rock.SetTexture("_BumpMap",Texture("rock_moss_set_01","nor_gl"));rock.EnableKeyword("_NORMALMAP");rock.SetFloat("_Glossiness",.12f);
            var fern=Material("KampungFern","Nusantara/Kampung Foliage");fern.mainTexture=Texture("fern_02","Diffuse");fern.SetTexture("_BumpMap",Texture("fern_02","nor_gl"));fern.SetTexture("_AlphaTex",Texture("fern_02","Alpha"));fern.SetFloat("_Cutoff",.4f);
            var leaves=Material("KampungLeafVfx","Nusantara/Kampung Foliage");leaves.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Region5_canopy_leaf_clump.asset");leaves.SetFloat("_UseVertexColor",1);leaves.SetFloat("_Wind",.025f);
            foreach(string model in new[]{"rock_moss_set_01","fern_02"})PrepareModel(model,model.StartsWith("rock")?rock:fern);
            foreach(var material in new[]{ground,grass,rock,fern,leaves})EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }
        public static Material Material(string name,string shader)
        {
            string path=WorldAssetLayout.Materials+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){var s=Shader.Find(shader);if(s==null)throw new InvalidOperationException("Missing shader "+shader);material=new Material(s){name=name,enableInstancing=true};AssetDatabase.CreateAsset(material,path);}
            return material;
        }
        static void PrepareModel(string name,Material material)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source+"/"+name+".fbx");if(source==null)throw new InvalidOperationException("Import source model: "+name);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                int index=0;
                foreach(var filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    if(filter.sharedMesh==null)continue;string id=(name.StartsWith("rock")?"shore_rock_":"forest_fern_")+index++;
                    string path=Prepared+"/"+id+".prefab";if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)continue;
                    var original=filter.sharedMesh;var mesh=Object.Instantiate(original);var vertices=mesh.vertices;var normals=mesh.normals;var matrix=filter.transform.localToWorldMatrix;
                    for(int i=0;i<vertices.Length;i++){vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);if(i<normals.Length)normals[i]=matrix.MultiplyVector(normals[i]).normalized;}
                    var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var vertex in vertices)bounds.Encapsulate(vertex);
                    Vector3 offset=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);for(int i=0;i<vertices.Length;i++)vertices[i]-=offset;
                    mesh.vertices=vertices;mesh.normals=normals;mesh.name=id;mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Prepared+"/"+id+".asset");
                    var low=WorldGenerationGeometry.Simplify(mesh,name.StartsWith("rock")?12:16);AssetDatabase.CreateAsset(low,Prepared+"/"+id+"_LOD.asset");
                    var root=new GameObject(id);var renderers=new Renderer[2];int n=0;
                    foreach(var m in new[]{mesh,low})
                    {
                        var child=new GameObject(n==0?"Source art":"Distance LOD");child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=m;
                        var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderers[n++]=renderer;
                    }
                    var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.04f,new[]{renderers[0]}),new LOD(.012f,new[]{renderers[1]})});lod.RecalculateBounds();
                    PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);
                }
            }
            finally{Object.DestroyImmediate(instance);}
        }
        public static Bounds Bounds(GameObject root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return new Bounds();var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);return bounds;
        }
    }
}
