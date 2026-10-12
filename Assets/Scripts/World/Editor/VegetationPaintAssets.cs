using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Nusantara.World.Editor
{
    public static class VegetationPaintAssets
    {
        public const string Root="Assets/NusantaraWorld/VegetationPaint";
        public const string PalettePath=Root+"/Region5Palette.asset";
        public static string Folder(WorldDefinition world,Vector2Int coordinate)=>$"{WorldReferenceBuilder.Root}/Chunks/{coordinate.x:D2}_{coordinate.y:D2}";
        public static VegetationPaintPalette Prepare()
        {
            Region5EnvironmentAssets.EnsureFolder(Root+"/Patches");
            var palette=AssetDatabase.LoadAssetAtPath<VegetationPaintPalette>(PalettePath);
            if(palette!=null)
            {
                var serialized=new SerializedObject(palette);serialized.FindProperty("m_Script").objectReferenceValue=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/World/VegetationPaintPalette.cs");serialized.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(palette);return palette;
            }
            palette=ScriptableObject.CreateInstance<VegetationPaintPalette>();
            palette.treePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Map/Props/Region5/rainforest_tree.prefab");
            if(palette.treePrefab==null)throw new InvalidOperationException("Existing rainforest_tree prefab is required.");
            palette.leafMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/KampungLeafVfx.mat");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/GrassTuft.asset");if(mesh==null){mesh=GrassTuft();AssetDatabase.CreateAsset(mesh,Root+"/GrassTuft.asset");}
            var go=new GameObject("Paintable grass tuft");
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/KampungGrass.mat");
            renderer.shadowCastingMode=ShadowCastingMode.Off;go.AddComponent<KampungGrassQuality>();
            palette.grassPrefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/GrassTuft.prefab");Object.DestroyImmediate(go);
            if(File.Exists(PalettePath))AssetDatabase.DeleteAsset(PalettePath);AssetDatabase.CreateAsset(palette,PalettePath);AssetDatabase.SaveAssets();return palette;
        }
        static Mesh GrassTuft()
        {
            var vertices=new List<Vector3>();var colors=new List<Color>();var uv=new List<Vector2>();var triangles=new List<int>();
            var random=new System.Random(521);
            for(int blade=0;blade<14;blade++)
            {
                float angle=(float)random.NextDouble()*Mathf.PI*2,height=.45f+(float)random.NextDouble()*.55f,width=.016f+(float)random.NextDouble()*.024f;
                Vector3 basePoint=new Vector3(((float)random.NextDouble()-.5f)*.5f,0,((float)random.NextDouble()-.5f)*.5f);
                Vector3 across=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*width,lean=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*height*.16f;
                int n=vertices.Count;
                vertices.AddRange(new[]{basePoint-across,basePoint+across,basePoint+Vector3.up*height*.55f+lean*.35f-across*.55f,basePoint+Vector3.up*height*.55f+lean*.35f+across*.55f,basePoint+Vector3.up*height+lean});
                uv.AddRange(new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,.55f),new Vector2(1,.55f),new Vector2(.5f,1)});
                Color bottom=new Color(.16f,.25f,.065f),tip=Color.Lerp(new Color(.33f,.44f,.12f),new Color(.55f,.52f,.21f),(float)random.NextDouble());
                colors.AddRange(new[]{bottom,bottom,Color.Lerp(bottom,tip,.6f),Color.Lerp(bottom,tip,.6f),tip});
                triangles.AddRange(new[]{n,n+2,n+1,n+1,n+2,n+3,n+2,n+4,n+3});
            }
            var mesh=new Mesh{name="Curved grass blades"};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static void BuildVisuals(GameObject root,VegetationPaintData data,VegetationPaintPalette palette,bool persistMeshes)
        {
            foreach(Transform child in root.transform.Cast<Transform>().ToArray())
            {
                foreach(var filter in child.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null&&!AssetDatabase.Contains(filter.sharedMesh))Object.DestroyImmediate(filter.sharedMesh);
                Object.DestroyImmediate(child.gameObject);
            }
            var world=AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);Vector3 origin=world.ChunkOrigin(data.coordinate);
            var crowns=new List<Vector3>();var radii=new List<float>();
            foreach(var stamp in data.stamps.Where(s=>s.kind==VegetationPaintKind.Tree))
            {
                var tree=(GameObject)PrefabUtility.InstantiatePrefab(palette.treePrefab,root.transform);
                tree.name="Painted tree prop";tree.transform.localPosition=stamp.position-origin;tree.transform.localRotation=Quaternion.Euler(0,stamp.yaw,0);tree.transform.localScale=Vector3.one*stamp.scale;
                foreach(var collider in tree.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
                var bounds=KampungAssets.Bounds(tree);crowns.Add(root.transform.InverseTransformPoint(new Vector3(bounds.center.x,bounds.max.y-bounds.size.y*.15f,bounds.center.z)));radii.Add(Mathf.Max(bounds.size.x,bounds.size.z)*.25f);
            }
            if(crowns.Count>0)Canopy(root,crowns,radii,palette.leafMaterial,data.coordinate.x*137+data.coordinate.y*521);
            var source=palette.grassPrefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            var material=palette.grassPrefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            var grass=data.stamps.Where(s=>s.kind==VegetationPaintKind.Grass).GroupBy(s=>new Vector2Int(Mathf.FloorToInt((s.position.x-origin.x)/32),Mathf.FloorToInt((s.position.z-origin.z)/32)));
            foreach(var cell in grass)
            {
                // Keep batches spatially small for frustum culling and below 16-bit mesh limits.
                var list=cell.ToArray();for(int start=0;start<list.Length;start+=800)
                {
                    Vector3 pivot=new Vector3(cell.Key.x*32,0,cell.Key.y*32);
                    var combine=list.Skip(start).Take(800).Select(s=>new CombineInstance{mesh=source,transform=Matrix4x4.TRS(s.position-origin-pivot,Quaternion.Euler(0,s.yaw,0),Vector3.one*s.scale)}).ToArray();
                    var mesh=new Mesh{name=$"Painted grass {cell.Key.x}_{cell.Key.y}_{start}"};mesh.CombineMeshes(combine,true,true);mesh.RecalculateBounds();
                    if(persistMeshes)
                    {
                        string path=Folder(world,data.coordinate)+$"/PaintGrass_{cell.Key.x}_{cell.Key.y}_{start}.asset";
                        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if(existing==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}
                    }
                    var go=new GameObject(mesh.name);go.transform.SetParent(root.transform,false);go.transform.localPosition=pivot;go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;go.AddComponent<KampungGrassQuality>();
                    var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.018f,new[]{renderer})});lod.RecalculateBounds();
                }
            }
        }
        public static void Canopy(GameObject root,List<Vector3> crowns,List<float> radii,Material material,int seed)
        {
            var go=new GameObject("Painted canopy VFX batch");go.transform.SetParent(root.transform,false);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.startSpeed=0;main.maxParticles=384;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.maxParticleSize=.3f;
            var vfx=go.AddComponent<WorldCanopyVfx>();vfx.crowns=crowns.ToArray();vfx.radii=radii.ToArray();vfx.seed=seed;vfx.minimumCrownRadius=1;vfx.maximumCrownRadius=8;vfx.canopyRadiusMultiplier=1.25f;vfx.visibleDistance=160;vfx.Rebuild();
        }
        public static void Save(VegetationPaintData data,VegetationPaintPalette palette)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Painting requires Edit mode.");
            var world=AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
            var record=world.chunks.First(c=>c.coordinate==data.coordinate&&c.region==5);string folder=Folder(world,data.coordinate),path=folder+"/PaintedVegetation.prefab";
            string backup="Library/Nusantara/VegetationPaintBackup/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            foreach(string file in new[]{path,folder+"/Essential.prefab",folder+"/Navigation.asset",WorldFoundationSetup.DefinitionPath})Backup(file,backup);
            var root=new GameObject("Hand painted vegetation");
            try
            {
                BuildVisuals(root,data,palette,true);PrefabUtility.SaveAsPrefabAsset(root,path);
                var settings=AddressableAssetSettingsDefaultObject.Settings;var entry=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path),settings.FindGroup("Nusantara World Chunks"),false,false);
                entry.address=$"world/{data.coordinate.x:D2}_{data.coordinate.y:D2}/painted-vegetation";
                record.decorationAddresses=record.decorationAddresses.Where(a=>!a.EndsWith("/painted-vegetation")).Concat(new[]{entry.address}).ToArray();
                var essential=PrefabUtility.LoadPrefabContents(folder+"/Essential.prefab");
                try
                {
                    var old=essential.transform.Find("Painted vegetation collision");if(old!=null)Object.DestroyImmediate(old.gameObject);
                    var colliders=new GameObject("Painted vegetation collision");colliders.transform.SetParent(essential.transform,false);
                    Vector3 origin=world.ChunkOrigin(data.coordinate);
                    foreach(var stamp in data.stamps.Where(s=>s.kind==VegetationPaintKind.Tree))
                    {
                        var go=new GameObject("Painted trunk collision");go.transform.SetParent(colliders.transform,false);go.transform.localPosition=stamp.position-origin;
                        var collider=go.AddComponent<CapsuleCollider>();collider.radius=palette.treeCollisionRadius*stamp.scale;collider.height=3;collider.center=Vector3.up*1.5f;
                    }
                    RebuildNavigation(essential,world,backup);PrefabUtility.SaveAsPrefabAsset(essential,folder+"/Essential.prefab");
                }
                finally{PrefabUtility.UnloadPrefabContents(essential);}
                EditorUtility.SetDirty(world);EditorUtility.SetDirty(data);settings.SetDirty(UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.ModificationEvent.BatchModification,entry,true,true);AssetDatabase.SaveAssets();
            }
            finally{Object.DestroyImmediate(root);}
        }
        public static void Backup(string path,string backup)
        {if(!File.Exists(path))return;string destination=backup+"/"+path;Directory.CreateDirectory(Path.GetDirectoryName(destination));if(!File.Exists(destination))File.Copy(path,destination);}
        public static bool IsFloor(Collider collider)=>collider.name=="Terrain"||collider.name.Contains("Connected paths")||collider.name.Contains("Smooth bridges")||collider.name.Contains("Arena floor")||collider.name.StartsWith("Kampung walking");
        public static void RebuildNavigation(GameObject root,WorldDefinition world,string backup)
        {
            var navigation=root.GetComponent<WorldChunkNavigation>();if(navigation==null||navigation.bakedData.Length==0||navigation.bakedData[0]==null)return;
            var sources=new List<NavMeshBuildSource>();var markups=root.GetComponentsInChildren<Collider>().Where(c=>!IsFloor(c)).Select(c=>new NavMeshBuildMarkup{root=c.transform,overrideArea=true,area=1}).ToList();
            NavMeshBuilder.CollectSources(root.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,markups,sources);
            var settings=NavMesh.GetSettingsByIndex(0);settings.agentSlope=world.maxWalkableSlope;settings.agentClimb=world.maxStepHeight;settings.agentRadius=world.agentRadius;settings.agentHeight=world.agentHeight;
            settings.overrideVoxelSize=true;settings.voxelSize=.4f;settings.overrideTileSize=true;settings.tileSize=256;
            Backup(AssetDatabase.GetAssetPath(navigation.bakedData[0]),backup);
            if(!NavMeshBuilder.UpdateNavMeshData(navigation.bakedData[0],settings,sources,new Bounds(new Vector3(world.chunkSize/2,150,world.chunkSize/2),new Vector3(world.chunkSize,400,world.chunkSize))))throw new InvalidOperationException("Navigation update failed.");
            EditorUtility.SetDirty(navigation.bakedData[0]);
        }
    }
}
