using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using TMPro;
using Object=UnityEngine.Object;

namespace Nusantara.World.Editor
{
    public static class WorldRegionThemeAssets
    {
        struct Theme
        {
            public int id,shape; public string boss; public Color ground,leaves,shade;
            public Theme(int id,string boss,int shape,Color ground,Color leaves,Color shade){this.id=id;this.boss=boss;this.shape=shape;this.ground=ground;this.leaves=leaves;this.shade=shade;}
        }
        // IDs retain the existing saved world: Bujang Senang is Region 5, Raja Bersiong is Region 6.
        static readonly Theme[] themes={
            new Theme(1,"Naga Seri Gumum",0,new Color(.25f,.37f,.42f),new Color(.40f,.60f,.62f),new Color(.16f,.32f,.38f)),
            new Theme(2,"Harimau Penjaga Johor",2,new Color(.40f,.23f,.17f),new Color(.67f,.33f,.17f),new Color(.32f,.12f,.12f)),
            new Theme(3,"Sang Kelembai",0,new Color(.22f,.35f,.20f),new Color(.43f,.60f,.28f),new Color(.15f,.32f,.24f)),
            new Theme(4,"Batu Belah Batu Bertangkup",1,new Color(.34f,.35f,.38f),new Color(.57f,.59f,.55f),new Color(.29f,.32f,.35f)),
            new Theme(5,"Bujang Senang",0,new Color(.29f,.39f,.27f),new Color(.53f,.66f,.29f),new Color(.19f,.40f,.33f)),
            new Theme(6,"Raja Bersiong",1,new Color(.32f,.34f,.43f),new Color(.67f,.57f,.29f),new Color(.26f,.34f,.48f)),
            new Theme(7,"Ulek Mayang",2,new Color(.25f,.43f,.43f),new Color(.43f,.68f,.61f),new Color(.17f,.38f,.43f)),
            new Theme(8,"Darkness",2,new Color(.24f,.18f,.32f),new Color(.48f,.29f,.56f),new Color(.19f,.12f,.29f))
        };
        static Material ColourMaterial(string path,Material source,Color light,Color shade)
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat!=null)return mat;
            mat=new Material(source){name=System.IO.Path.GetFileNameWithoutExtension(path)};
            if(mat.HasProperty("_Color"))mat.SetColor("_Color",light);if(mat.HasProperty("_ShadowColor"))mat.SetColor("_ShadowColor",shade);
            AssetDatabase.CreateAsset(mat,path);return mat;
        }
        static VegetationBrushStyle Style(string path,string label,VegetationPaintKind kind,GameObject prefab,Material material)
        {
            var style=AssetDatabase.LoadAssetAtPath<VegetationBrushStyle>(path);if(style!=null)return style;
            style=ScriptableObject.CreateInstance<VegetationBrushStyle>();style.displayName=label;style.kind=kind;style.prefab=prefab;style.grassMaterial=material;
            style.height=kind==VegetationPaintKind.Tree?new Vector2(8,18):new Vector2(.35f,.75f);style.spacing=kind==VegetationPaintKind.Tree?5:.38f;style.trunkRadius=.7f;
            AssetDatabase.CreateAsset(style,path);return style;
        }
        [MenuItem("Tools/Nusantara/Prepare Chapter Palettes and Floating Islands")]
        public static string Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            var world=AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);var profile=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            if(!world.regionalGroundPlanes)throw new InvalidOperationException("Consolidate the mainland ground first.");
            VegetationVariationAssets.Ensure();var palette=VegetationPaintAssets.Prepare();
            var list=world.regions.ToList();
            foreach(var theme in themes.Where(t=>t.id>=7))if(list.All(r=>r.id!=theme.id))list.Add(new WorldRegion{id=theme.id,name=theme.boss,boss=theme.boss,floatingIsland=true,groundElevation=-160,chunkGridSize=new Vector2Int(6,6)});
            world.regions=list.OrderBy(r=>r.id).ToArray();
            var packed=world.regions.Select(r=>new WorldRegion{id=r.id,chunkGridSize=r.chunkGridSize}).ToArray();var size=WorldRegionGridLayout.Pack(packed);
            // Existing region offsets remain authored. Only the two new islands use the separate lower row.
            foreach(var region in world.regions.Where(r=>r.id>=7&&r.groundPrefab==null))region.chunkOffset=packed.Single(r=>r.id==region.id).chunkOffset;
            world.gridSize=new Vector2Int(Mathf.Max(world.gridSize.x,size.x),Mathf.Max(world.gridSize.y,size.y));
            var records=world.chunks.ToList();
            var leafSource=AssetDatabase.LoadAssetAtPath<Material>(VegetationPaintAssets.Root+"/PixelTreeLeaves.mat");var barkSource=AssetDatabase.LoadAssetAtPath<Material>(VegetationPaintAssets.Root+"/PixelTreeBark.mat");
            var grassSource=palette.grassPrefab.GetComponent<MeshRenderer>().sharedMaterial;
            foreach(var theme in themes)
            {
                var region=world.regions.Single(r=>r.id==theme.id);string folder=WorldRegionalGroundAssets.RegionFolder(region.id);Region5EnvironmentAssets.EnsureFolder(folder+"/Vegetation");
                var mat=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Ground.mat");
                if(mat==null)mat=ColourMaterial(folder+"/Ground.mat",AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Nusantara_BrushGround.mat"),theme.ground,theme.shade);
                mat.SetColor("_Color",theme.ground);EditorUtility.SetDirty(mat);
                var leaves=ColourMaterial(folder+"/Vegetation/Leaves.mat",leafSource,theme.leaves,theme.shade);
                var bark=ColourMaterial(folder+"/Vegetation/Bark.mat",barkSource,theme.id==8?new Color(.23f,.18f,.26f):new Color(.35f,.30f,.23f),new Color(.14f,.18f,.18f));
                string treePath=folder+"/Vegetation/Tree.prefab";var treePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(treePath);
                if(treePrefab==null)
                {
                    var source=theme.shape==1?palette.treeBrushes.First(s=>s.displayName=="Tall woodland tree").prefab:theme.shape==2?palette.treeBrushes.First(s=>s.displayName=="Fire ash tree").prefab:palette.treePrefab;
                    var tree=Object.Instantiate(source);tree.name=theme.boss+" tree";
                    try{foreach(var renderer in tree.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=renderer.name.IndexOf("trunk",StringComparison.OrdinalIgnoreCase)>=0?bark:leaves;treePrefab=PrefabUtility.SaveAsPrefabAsset(tree,treePath);}finally{Object.DestroyImmediate(tree);}
                }
                else
                {
                    var tree=PrefabUtility.LoadPrefabContents(treePath);
                    try{foreach(var renderer in tree.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=renderer.name.IndexOf("trunk",StringComparison.OrdinalIgnoreCase)>=0?bark:leaves;PrefabUtility.SaveAsPrefabAsset(tree,treePath);}finally{PrefabUtility.UnloadPrefabContents(tree);}
                }
                string grassPath=folder+"/Vegetation/Grass.mat";var grass=AssetDatabase.LoadAssetAtPath<Material>(grassPath);
                if(grass==null)
                {
                    grass=new Material(grassSource){name=theme.boss+" grass"};grass.SetColor("_RootColor",theme.shade*.7f);grass.SetColor("_TipColor",theme.leaves);grass.SetColor("_PatchColor",Color.Lerp(theme.shade,theme.leaves,.6f));AssetDatabase.CreateAsset(grass,grassPath);
                }
                region.treeBrush=Style(folder+"/Vegetation/TreeBrush.asset",theme.boss+" · tree",VegetationPaintKind.Tree,treePrefab,null);
                region.grassBrush=Style(folder+"/Vegetation/GrassBrush.asset",theme.boss+" · grass",VegetationPaintKind.Grass,palette.grassPrefab,grass);
                palette.treeBrushes=palette.treeBrushes.Append(region.treeBrush).Distinct().ToArray();palette.grassBrushes=palette.grassBrushes.Append(region.grassBrush).Distinct().ToArray();
                if(region.groundPrefab==null)
                {
                    var temporary=new List<Mesh>();Mesh Tile(Vector2Int c,int intervals){var mesh=WorldTerrainMesher.Build(world,c,intervals,(_,__)=>profile.groundLevel+region.groundElevation);temporary.Add(mesh);return mesh;}
                    try
                    {
                        var near=WorldRegionalGroundAssets.Store(WorldRegionalGroundAssets.Join(world,region,profile.terrainIntervals,c=>Tile(c,profile.terrainIntervals)),folder+"/Ground.asset");
                        var far=WorldRegionalGroundAssets.Store(WorldRegionalGroundAssets.Join(world,region,profile.distantIntervals,c=>Tile(c,profile.distantIntervals)),folder+"/GroundLOD.asset");
                        region.groundPrefab=WorldRegionalGroundAssets.GroundPrefab(world,region,near,far,mat,profile.terrainIntervals,profile.distantIntervals);
                    }
                    finally{foreach(var mesh in temporary)Object.DestroyImmediate(mesh);}
                }
                if(region.id>=7)
                {
                    var o=world.ChunkOrigin(region.chunkOffset);float w=region.chunkGridSize.x*world.chunkSize,d=region.chunkGridSize.y*world.chunkSize;
                    region.boundary=new[]{new Vector2(o.x,o.z),new Vector2(o.x+w,o.z),new Vector2(o.x+w,o.z+d),new Vector2(o.x,o.z+d)};
                    for(int z=0;z<region.chunkGridSize.y;z++)for(int x=0;x<region.chunkGridSize.x;x++)if(records.All(c=>c.coordinate!=region.chunkOffset+new Vector2Int(x,z)))
                    {var record=new WorldChunkRecord{region=region.id,coordinate=region.chunkOffset+new Vector2Int(x,z)};record.assetFolder=WorldRegionalGroundAssets.DetailFolder(world,record);records.Add(record);}
                }
            }
            world.chunks=records.ToArray();EditorUtility.SetDirty(world);EditorUtility.SetDirty(palette);AssetDatabase.SaveAssets();WorldRegionalGroundAssets.RefreshScene(world);
            PrepareGates(world,profile);foreach(var tool in Resources.FindObjectsOfTypeAll<VegetationPainterWindow>())tool.RefreshBrushOptions();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());return "Eight chapter ground, tree and grass palettes prepared. Two separate floating islands below mainland, accessed through Region 1 gates.";
        }
        public static void AddIslandUnderside(GameObject root,WorldRegion region,Mesh ground,Material groundMaterial)
        {
            float w=ground.bounds.size.x,d=ground.bounds.size.z,y=ground.vertices[0].y;
            var corners=new[]{new Vector3(0,y,0),new Vector3(w,y,0),new Vector3(w,y,d),new Vector3(0,y,d)};
            var vertices=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color>();
            for(int side=0;side<4;side++)
            {
                var a=corners[side];var b=corners[(side+1)%4];var middle=(a+b)*.5f;
                var ringA=new Vector3(Mathf.Lerp(a.x,w/2,.12f),y-25,Mathf.Lerp(a.z,d/2,.12f));var ringB=new Vector3(Mathf.Lerp(b.x,w/2,.12f),y-25,Mathf.Lerp(b.z,d/2,.12f));
                var tip=new Vector3(w*.5f,y-150,d*.5f);int n=vertices.Count;
                vertices.AddRange(new[]{a,b,ringA,b,ringB,ringA,ringA,ringB,tip});for(int i=0;i<9;i++){triangles.Add(n+i);colors.Add(i<6?new Color(.7f,.7f,.7f):new Color(.35f,.35f,.35f));}
            }
            var mesh=new Mesh{name="Floating island rock underside"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateBounds();
            mesh=WorldRegionalGroundAssets.Store(mesh,WorldRegionalGroundAssets.RegionFolder(region.id)+"/IslandUnderside.asset");
            var material=ColourMaterial(WorldRegionalGroundAssets.RegionFolder(region.id)+"/IslandRock.mat",groundMaterial,region.id==8?new Color(.14f,.10f,.20f):new Color(.22f,.29f,.32f),Color.black);
            var go=new GameObject("Island rock underside");go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
            // The underside is visual only. The single walkable top retains the one ground collider.
        }
        static void PrepareGates(WorldDefinition world,WorldGeographyProfile profile)
        {
            var scene=SceneManager.GetActiveScene();var parent=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Region 1 Island Access");if(parent==null)parent=new GameObject("Region 1 Island Access");
            var mainland=world.regions.Single(r=>r.id==1);var centre=world.ChunkOrigin(mainland.chunkOffset)+new Vector3(mainland.chunkGridSize.x*world.chunkSize/2,0,mainland.chunkGridSize.y*world.chunkSize/2);
            foreach(var region in world.regions.Where(r=>r.floatingIsland))
            {
                var entry=centre+Vector3.right*(region.id==7?-9:9);entry.y=WorldTerrainSculptAssets.SampleGround(profile,entry.x,entry.z);
                var landing=world.ChunkOrigin(region.chunkOffset)+new Vector3(region.chunkGridSize.x*world.chunkSize/2,0,region.chunkGridSize.y*world.chunkSize/2);landing.y=WorldTerrainSculptAssets.SampleGround(profile,landing.x,landing.z);
                var outward=Gate(parent.transform,"Access "+region.boss,entry,world,region.id,region.grassBrush.grassMaterial.GetColor("_TipColor"));
                var inward=Gate(parent.transform,"Return from "+region.boss,landing,world,1,region.grassBrush.grassMaterial.GetColor("_TipColor"));
                outward.destination=landing+Vector3.forward*5+Vector3.up*.15f;inward.destination=entry+Vector3.forward*5+Vector3.up*.15f;
                outward.label="Travel to "+region.boss;inward.label="Return to Region 1";EditorUtility.SetDirty(outward);EditorUtility.SetDirty(inward);
            }
        }
        static WorldRegionPortal Gate(Transform parent,string name,Vector3 position,WorldDefinition world,int destinationRegion,Color color)
        {
            var child=parent.Find(name);if(child!=null){child.position=position;return child.GetComponent<WorldRegionPortal>();}
            var root=new GameObject(name);root.transform.SetParent(parent,false);root.transform.position=position;var portal=root.AddComponent<WorldRegionPortal>();portal.world=world;portal.destinationRegion=destinationRegion;
            var mat=new Material(Shader.Find("Standard")){name="Island gate"};mat.color=color;mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*.35f);
            string matPath=WorldRegionalGroundAssets.RegionFolder(destinationRegion)+"/Gate.mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(existing!=null){Object.DestroyImmediate(mat);mat=existing;}else AssetDatabase.CreateAsset(mat,matPath);
            void Stone(string n,Vector3 p,Vector3 scale){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=n;go.transform.SetParent(root.transform,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<MeshRenderer>().sharedMaterial=mat;}
            Stone("Gate left",new Vector3(-2,2,0),new Vector3(.7f,4,.8f));Stone("Gate right",new Vector3(2,2,0),new Vector3(.7f,4,.8f));Stone("Gate lintel",new Vector3(0,4,0),new Vector3(4.8f,.7f,.8f));
            var canvasGO=new GameObject("Travel Prompt",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));canvasGO.transform.SetParent(root.transform,false);
            portal.promptCanvas=canvasGO.GetComponent<Canvas>();portal.promptCanvas.renderMode=RenderMode.ScreenSpaceOverlay;portal.promptCanvas.sortingOrder=32;
            var scaler=canvasGO.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var buttonGO=new GameObject("Travel",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));buttonGO.transform.SetParent(canvasGO.transform,false);
            var rect=(RectTransform)buttonGO.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.2f);rect.sizeDelta=new Vector2(340,68);rect.anchoredPosition=Vector2.zero;
            var image=buttonGO.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.06f,.08f,.12f,.95f);portal.promptButton=buttonGO.GetComponent<UnityEngine.UI.Button>();portal.promptButton.targetGraphic=image;portal.promptButton.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            var labelGO=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));labelGO.transform.SetParent(buttonGO.transform,false);var lr=(RectTransform)labelGO.transform;lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
            portal.promptText=labelGO.GetComponent<TextMeshProUGUI>();portal.promptText.fontSize=26;portal.promptText.alignment=TextAlignmentOptions.Center;portal.promptText.color=Color.white;portal.promptText.raycastTarget=false;
            canvasGO.SetActive(false);return portal;
        }
    }
}
