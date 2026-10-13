using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace Nusantara.World.Editor
{
    public static class VegetationVariationAssets
    {
        public const string Root=VegetationPaintAssets.Root+"/BrushStyles";
        [MenuItem("Tools/Nusantara/Vegetation/Add Default Brush Variations")]
        public static void Ensure()
        {
            Region5EnvironmentAssets.EnsureFolder(Root);
            var palette=VegetationPaintAssets.Prepare();
            var green=palette.grassPrefab.GetComponent<MeshRenderer>().sharedMaterial;
            var leaves=AssetDatabase.LoadAssetAtPath<Material>(VegetationPaintAssets.Root+"/PixelTreeLeaves.mat");
            var bark=AssetDatabase.LoadAssetAtPath<Material>(VegetationPaintAssets.Root+"/PixelTreeBark.mat");
            if(leaves==null||bark==null)throw new InvalidOperationException("Apply Reference Pixel Vegetation first.");
            var pineLeaves=LeafMaterial("WoodlandLeaves",leaves,new Color(.34f,.46f,.32f),new Color(.20f,.32f,.29f));
            var fireLeaves=LeafMaterial("FireLeaves",leaves,new Color(.50f,.24f,.18f),new Color(.25f,.13f,.12f));
            var fireBark=LeafMaterial("CharredBark",bark,new Color(.26f,.23f,.21f),new Color(.13f,.15f,.16f));
            var woodland=Tree("WoodlandTree",1,pineLeaves,bark);
            var fireTree=Tree("FireAshTree",2,fireLeaves,fireBark);
            var tall=Grass("TallWildGrass",green);
            var red=GrassMaterial("FireRedGrass",green,new Color(.23f,.10f,.08f),new Color(.52f,.25f,.18f),new Color(.40f,.15f,.12f));
            var gold=GrassMaterial("DryGoldenGrass",green,new Color(.30f,.26f,.17f),new Color(.52f,.45f,.30f),new Color(.40f,.34f,.21f));
            var trees=new[]{
                Style(Root+"/Broadleaf.asset","Broadleaf tree",VegetationPaintKind.Tree,palette.treePrefab,null,new Vector2(8,18),5,.8f),
                Style(Root+"/Woodland.asset","Tall woodland tree",VegetationPaintKind.Tree,woodland,null,new Vector2(10,22),4,.55f),
                Style(Root+"/FireTree.asset","Fire ash tree",VegetationPaintKind.Tree,fireTree,null,new Vector2(7,16),5,.75f)
            };
            var grasses=new[]{
                Style(Root+"/Meadow.asset","Meadow grass · green",VegetationPaintKind.Grass,palette.grassPrefab,green,palette.grassHeight,palette.grassSpacing,.8f),
                Style(Root+"/Wild.asset","Tall wild grass · green",VegetationPaintKind.Grass,tall,green,new Vector2(.65f,1.05f),.45f,.8f),
                Style(Root+"/FireGrass.asset","Fire grass · red",VegetationPaintKind.Grass,tall,red,new Vector2(.45f,.85f),.38f,.8f),
                Style(Root+"/DryGrass.asset","Dry grass · golden",VegetationPaintKind.Grass,palette.grassPrefab,gold,new Vector2(.30f,.65f),.35f,.8f)
            };
            Undo.RecordObject(palette,"Add vegetation brush styles");
            palette.treeBrushes=(palette.treeBrushes??Array.Empty<VegetationBrushStyle>()).Where(s=>s!=null).Concat(trees).Distinct().ToArray();
            palette.grassBrushes=(palette.grassBrushes??Array.Empty<VegetationBrushStyle>()).Where(s=>s!=null).Concat(grasses).Distinct().ToArray();
            EditorUtility.SetDirty(palette);AssetDatabase.SaveAssets();
            foreach(var window in Resources.FindObjectsOfTypeAll<VegetationPainterWindow>())window.RefreshBrushOptions();
        }
        public static VegetationBrushStyle AddPrefab(VegetationPaintPalette palette,VegetationPaintKind kind,GameObject prefab,string label,Material material)
        {
            if(prefab==null||!PrefabUtility.IsPartOfPrefabAsset(prefab))throw new InvalidOperationException("Choose a prefab asset for the brush.");
            if(kind==VegetationPaintKind.Grass&&(prefab.GetComponentInChildren<MeshFilter>()==null||prefab.GetComponentInChildren<MeshRenderer>()==null))throw new InvalidOperationException("Grass prefab needs a MeshFilter and MeshRenderer.");
            Region5EnvironmentAssets.EnsureFolder(Root);
            string path=AssetDatabase.GenerateUniqueAssetPath(Root+"/CustomBrush.asset");
            var style=Style(path,string.IsNullOrWhiteSpace(label)?prefab.name:label,kind,prefab,kind==VegetationPaintKind.Grass?material:null,kind==VegetationPaintKind.Tree?palette.treeHeight:palette.grassHeight,kind==VegetationPaintKind.Tree?palette.treeSpacing:palette.grassSpacing,palette.treeCollisionRadius);
            AddToPalette(palette,style);return style;
        }
        public static VegetationBrushStyle AddGrassColour(VegetationPaintPalette palette,VegetationBrushStyle source,string label,Color roots,Color tips,Color patches)
        {
            Region5EnvironmentAssets.EnsureFolder(Root);
            var prefab=source!=null?source.prefab:palette.grassPrefab;
            var original=source!=null&&source.grassMaterial!=null?source.grassMaterial:prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            var material=new Material(original){name=string.IsNullOrWhiteSpace(label)?"Custom grass colour":label};material.shader=Shader.Find("Nusantara/Kampung Living Grass");
            material.SetColor("_RootColor",roots);material.SetColor("_TipColor",tips);material.SetColor("_PatchColor",patches);
            AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(Root+"/CustomGrass.mat"));
            var style=Style(AssetDatabase.GenerateUniqueAssetPath(Root+"/CustomGrass.asset"),material.name,VegetationPaintKind.Grass,prefab,material,source!=null?source.height:palette.grassHeight,source!=null?source.spacing:palette.grassSpacing,.8f);
            AddToPalette(palette,style);return style;
        }
        static void AddToPalette(VegetationPaintPalette palette,VegetationBrushStyle style)
        {
            Undo.RecordObject(palette,"Add brush style");
            if(style.kind==VegetationPaintKind.Tree)palette.treeBrushes=(palette.treeBrushes??Array.Empty<VegetationBrushStyle>()).Append(style).ToArray();
            else palette.grassBrushes=(palette.grassBrushes??Array.Empty<VegetationBrushStyle>()).Append(style).ToArray();
            EditorUtility.SetDirty(palette);AssetDatabase.SaveAssets();
        }
        static VegetationBrushStyle Style(string path,string label,VegetationPaintKind kind,GameObject prefab,Material material,Vector2 height,float spacing,float radius)
        {
            var style=AssetDatabase.LoadAssetAtPath<VegetationBrushStyle>(path);if(style!=null)return style;
            style=ScriptableObject.CreateInstance<VegetationBrushStyle>();style.displayName=label;style.kind=kind;style.prefab=prefab;style.grassMaterial=material;style.height=height;style.spacing=spacing;style.trunkRadius=radius;
            AssetDatabase.CreateAsset(style,path);return style;
        }
        static Material GrassMaterial(string name,Material source,Color roots,Color tips,Color patches)
        {
            string path=Root+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            material=new Material(source){name=name};material.SetColor("_RootColor",roots);material.SetColor("_TipColor",tips);material.SetColor("_PatchColor",patches);AssetDatabase.CreateAsset(material,path);return material;
        }
        static Material LeafMaterial(string name,Material source,Color light,Color shadow)
        {
            string path=Root+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            material=new Material(source){name=name};material.SetColor("_Color",light);material.SetColor("_ShadowColor",shadow);AssetDatabase.CreateAsset(material,path);return material;
        }
        static Mesh Store(Mesh mesh,string path)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null){Object.DestroyImmediate(mesh);return existing;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static GameObject Grass(string name,Material material)
        {
            string path=Root+"/"+name+".prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var mesh=Store(VegetationPaintAssets.GrassTuft(20,.7f,1.2f,1.6f),Root+"/"+name+".asset");var go=new GameObject(name);
            try{go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;go.AddComponent<KampungGrassQuality>();return PrefabUtility.SaveAsPrefabAsset(go,path);}
            finally{Object.DestroyImmediate(go);}
        }
        static GameObject Tree(string name,int shape,Material leaves,Material bark)
        {
            string path=Root+"/"+name+".prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var trunk=PixelVegetationAssets.Trunk();var vertices=trunk.vertices;
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i];if(shape==1)p=new Vector3(p.x*.55f,p.y*1.1f,p.z*.55f);else p.x+=Mathf.Sin(p.y*.3f)*p.y*.075f;vertices[i]=p;
            }
            trunk.vertices=vertices;trunk.RecalculateNormals();trunk.RecalculateBounds();trunk=Store(trunk,Root+"/"+name+"Trunk.asset");
            var crown=Store(PixelVegetationAssets.Crown(72,shape),Root+"/"+name+"Crown.asset");var low=Store(PixelVegetationAssets.Crown(28,shape),Root+"/"+name+"CrownLOD.asset");
            var go=new GameObject(name);
            try
            {
                MeshRenderer Child(string child,Mesh mesh,Material material)
                {var obj=new GameObject(child);obj.transform.SetParent(go.transform,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.TwoSided;return renderer;}
                var t=Child("Trunk",trunk,bark);var near=Child("Pixel leaf crown",crown,leaves);var far=Child("Pixel leaf crown LOD",low,leaves);
                var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.1f,new Renderer[]{t,near}),new LOD(.015f,new Renderer[]{t,far})});lod.RecalculateBounds();return PrefabUtility.SaveAsPrefabAsset(go,path);
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
