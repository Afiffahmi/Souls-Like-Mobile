using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ElementalGems.Editor
{
    public static class GemShieldBuilder
    {
        public const string Root="Assets/ElementalGems/ShieldVFX";
        [MenuItem("Tools/Elemental Gems/Set Up Shield Bonds")]
        public static void SetupMenu()=>Setup();
        public static string Setup()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before configuring the shield.");
            var shield=UnityEngine.Object.FindAnyObjectByType<PlayerShieldAnimation>();
            if(shield==null)throw new InvalidOperationException("No PlayerShieldAnimation in the loaded scene.");
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            var ribbon=Texture("Bond",false);var glow=Texture("Glow",true);
            var ribbonMat=Material("Bond",ribbon,1.8f);var glowMat=Material("Glow",glow,1.2f);
            var fx=shield.GetComponent<GemShieldEffects>();if(fx==null)fx=Undo.AddComponent<GemShieldEffects>(shield.gameObject);
            Undo.RecordObject(fx,"Configure elemental shield bonds");
            fx.manager=shield.GetComponentInParent<GemManager>();fx.deployment=shield;fx.ribbonMaterial=ribbonMat;fx.glowMaterial=glowMat;
            fx.particleMaterials=new Material[8];
            string[] textures={"Ember","Flame","Bubble","Leaf","Dust","Ember","Streak","Shadow"};
            for(int i=0;i<8;i++)
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ElementalGems/FantasyVFX/Textures/"+textures[i]+".png");
                fx.particleMaterials[i]=Material(((ElementType)i)+" Motes",texture!=null?texture:glow,i==1?1.5f:1.1f);
            }
            var mesh=shield.GetComponent<SkinnedMeshRenderer>().sharedMesh;
            var vertices=mesh.vertices;fx.shieldHeight=mesh.bounds.size.z;
            var deltas=new Vector3[mesh.blendShapeCount][];
            for(int j=0;j<deltas.Length;j++){deltas[j]=new Vector3[mesh.vertexCount];mesh.GetBlendShapeFrameVertices(j,mesh.GetBlendShapeFrameCount(j)-1,deltas[j],null,null);}
            var anchors=new System.Collections.Generic.List<GemShieldEffects.Anchor>();
            var core=fx.core;
            // The imported model has six Miniaturize shapes, each isolating one physical plate.
            for(int j=0;j<deltas.Length;j++)
            {
                if(!mesh.GetBlendShapeName(j).EndsWith("Miniaturize"))continue;
                var indices=Enumerable.Range(0,vertices.Length).Where(i=>deltas[j][i].sqrMagnitude>1e-12f).ToArray();
                var center=indices.Aggregate(Vector3.zero,(v,i)=>v+vertices[i])/indices.Length;
                var direction=center-core;direction.y=0;direction.Normalize();
                var tangent=Vector3.Cross(direction,Vector3.up);
                for(int branch=0;branch<2;branch++)
                {
                    var target=core+direction*fx.shieldHeight*.08f+tangent*fx.shieldHeight*(branch==0?-.105f:.105f);
                    target.y=mesh.bounds.max.y;
                    int vertex=indices.OrderBy(i=>(vertices[i]-target).sqrMagnitude).First();
                    var anchor=new GemShieldEffects.Anchor{position=vertices[vertex]+Vector3.up*fx.shieldHeight*.003f,blendDeltas=new Vector3[deltas.Length]};
                    for(int k=0;k<deltas.Length;k++)anchor.blendDeltas[k]=deltas[k][vertex];
                    anchors.Add(anchor);
                }
            }
            fx.anchors=anchors.ToArray();
            EditorUtility.SetDirty(fx);PrefabUtility.RecordPrefabInstancePropertyModifications(fx);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(shield.gameObject.scene);EditorSceneManager.SaveScene(shield.gameObject.scene);
            Selection.activeGameObject=shield.gameObject;
            return "Saved "+anchors.Count+" animated anchors on "+shield.name+"; all eight gems supported.";
        }
        static Texture2D Texture(string name,bool radial)
        {
            string path=Root+"/"+name+".png";
            if(!File.Exists(path))
            {
                const int n=64;var tex=new Texture2D(n,n,TextureFormat.RGBA32,false);var colors=new Color[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    float u=(x+.5f)/n*2-1,v=(y+.5f)/n*2-1;
                    float a=radial?Mathf.Pow(Mathf.Clamp01(1-Mathf.Sqrt(u*u+v*v)),2):Mathf.Exp(-v*v*7)*Mathf.Clamp01((1-Mathf.Abs(v))*8);
                    colors[y*n+x]=new Color(1,1,1,a);
                }
                tex.SetPixels(colors);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(path);
            }
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material Material(string name,Texture2D texture,float glow)
        {
            string path=Root+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("ElementalGems/Fantasy Particles"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetTexture("_MainTex",texture);mat.SetColor("_Tint",Color.white);mat.SetFloat("_Glow",glow);mat.SetFloat("_DstBlend",1);EditorUtility.SetDirty(mat);return mat;
        }
    }
}
