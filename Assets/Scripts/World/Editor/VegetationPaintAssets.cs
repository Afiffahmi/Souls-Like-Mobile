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
        public static string Folder(WorldDefinition world,Vector2Int coordinate)=>WorldTerrainSculptAssets.Folder(world,coordinate);
        public static string PatchFolder(WorldDefinition world) => world.brushAuthoredTerrain ? world.chunkAssetRoot : Root + "/Patches";
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
        public static Mesh GrassTuft(int bladeCount=14,float widthMultiplier=1,float spreadMultiplier=1,float leanMultiplier=1)
        {
            var vertices=new List<Vector3>();var roots=new List<Vector3>();var colors=new List<Color>();var uv=new List<Vector2>();var triangles=new List<int>();
            var random=new System.Random(521);
            for(int blade=0;blade<bladeCount;blade++)
            {
                float angle=(float)random.NextDouble()*Mathf.PI*2,height=.55f+(float)random.NextDouble()*.45f,width=(.065f+(float)random.NextDouble()*.035f)*widthMultiplier;
                Vector3 basePoint=new Vector3(((float)random.NextDouble()-.5f)*1.1f*spreadMultiplier,0,((float)random.NextDouble()-.5f)*1.1f*spreadMultiplier);
                Vector3 across=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*width,lean=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*height*.28f*leanMultiplier;
                int n=vertices.Count;
                vertices.AddRange(new[]{basePoint-across,basePoint+across,basePoint+Vector3.up*height*.55f+lean*.35f-across*.55f,basePoint+Vector3.up*height*.55f+lean*.35f+across*.55f,basePoint+Vector3.up*height+lean});
                roots.AddRange(Enumerable.Repeat(basePoint,5));
                uv.AddRange(new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,.55f),new Vector2(1,.55f),new Vector2(.5f,1)});
                float bladeId=.02f+(float)random.NextDouble()*.96f;
                Color bottom=new Color(.24f,.40f,.10f,bladeId),tip=new Color(.57f,.72f,.24f,bladeId);
                colors.AddRange(new[]{bottom,bottom,Color.Lerp(bottom,tip,.6f),Color.Lerp(bottom,tip,.6f),tip});
                triangles.AddRange(new[]{n,n+2,n+1,n+1,n+2,n+3,n+2,n+4,n+3});
            }
            var mesh=new Mesh{name="Broad pixel grass blades"};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetUVs(1,roots);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();
            var normals=mesh.normals;for(int i=0;i<normals.Length;i++)normals[i]=(normals[i]*.35f+Vector3.up).normalized;mesh.normals=normals;
            mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(.6f);mesh.bounds=bounds;return mesh;
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
                var treePrefab=stamp.brushStyle!=null&&stamp.brushStyle.prefab!=null?stamp.brushStyle.prefab:palette.treePrefab;
                var tree=(GameObject)PrefabUtility.InstantiatePrefab(treePrefab,root.transform);
                tree.name="Painted tree prop";tree.transform.localPosition=stamp.position-origin;tree.transform.localRotation=Quaternion.Euler(0,stamp.yaw,0);tree.transform.localScale=Vector3.one*stamp.scale;
                foreach(var collider in tree.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
                // Pixel trees include their own layered crown. Custom bare-tree
                // palettes can still use the optional particle canopy.
                if((stamp.brushStyle==null||stamp.brushStyle.addParticleCanopy)&&tree.transform.Find("Pixel leaf crown")==null)
                {
                    var bounds=KampungAssets.Bounds(tree);crowns.Add(root.transform.InverseTransformPoint(new Vector3(bounds.center.x,bounds.max.y-bounds.size.y*.15f,bounds.center.z)));radii.Add(Mathf.Max(bounds.size.x,bounds.size.z)*.25f);
                }
            }
            if(crowns.Count>0)Canopy(root,crowns,radii,palette.leafMaterial,data.coordinate.x*137+data.coordinate.y*521);
            foreach(var styleGroup in data.stamps.Where(s=>s.kind==VegetationPaintKind.Grass).GroupBy(s=>s.brushStyle))
            {
            var style=styleGroup.Key;
            var prefab=style!=null&&style.prefab!=null?style.prefab:palette.grassPrefab;
            var source=prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            var sourceRoots=new List<Vector3>();source.GetUVs(1,sourceRoots);
            bool temporarySource=sourceRoots.Count!=source.vertexCount||source.GetVertexAttributeDimension(VertexAttribute.TexCoord1)!=3;
            if(temporarySource)
            {
                source=Object.Instantiate(source);var positions=source.vertices;float bottom=positions.Min(v=>v.y),height=Mathf.Max(.01f,positions.Max(v=>v.y)-bottom);
                sourceRoots=positions.Select(v=>new Vector3(0,bottom,0)).ToList();source.SetUVs(1,sourceRoots);
                source.SetUVs(0,positions.Select(v=>new Vector2(.5f,(v.y-bottom)/height)).ToList());
                source.SetColors(positions.Select(v=>new Color(1,1,1,.5f)).ToList());
            }
            var material=style!=null&&style.grassMaterial!=null?style.grassMaterial:prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            string styleGuid=style==null?"":AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(style));
            string styleSuffix=style==null?"":"_"+(string.IsNullOrEmpty(styleGuid)?style.name:styleGuid.Substring(0,8));
            var grass=styleGroup.GroupBy(s=>new Vector2Int(Mathf.FloorToInt((s.position.x-origin.x)/32),Mathf.FloorToInt((s.position.z-origin.z)/32)));
            try
            {
            foreach(var cell in grass)
            {
                // Keep batches spatially small for frustum culling and below 16-bit mesh limits.
                int batchSize=Mathf.Max(1,Mathf.Min(800,60000/source.vertexCount));
                var list=cell.ToArray();for(int start=0;start<list.Length;start+=batchSize)
                {
                    Vector3 pivot=new Vector3(cell.Key.x*32,0,cell.Key.y*32);
                    var combine=list.Skip(start).Take(batchSize).Select(s=>new CombineInstance{mesh=source,transform=Matrix4x4.TRS(s.position-origin-pivot,Quaternion.Euler(0,s.yaw,0),Vector3.one*s.scale)}).ToArray();
                    var mesh=new Mesh{name=$"Painted grass {cell.Key.x}_{cell.Key.y}_{start}{styleSuffix}"};if(source.vertexCount>65535)mesh.indexFormat=IndexFormat.UInt32;mesh.CombineMeshes(combine,true,true);mesh.RecalculateBounds();
                    // CombineMeshes transforms vertices, but custom UV coordinates need
                    // the same stamp transform so interaction roots remain in batch space.
                    if(sourceRoots.Count==source.vertexCount)
                    {
                        var roots=new List<Vector3>(mesh.vertexCount);
                        foreach(var instance in combine)foreach(var point in sourceRoots)roots.Add(instance.transform.MultiplyPoint3x4(point));
                        mesh.SetUVs(1,roots);
                    }
                    var bounds=mesh.bounds;bounds.Expand(1.5f);mesh.bounds=bounds;
                    if(persistMeshes)
                    {
                        string path=Folder(world,data.coordinate)+$"/PaintGrass_{cell.Key.x}_{cell.Key.y}_{start}{styleSuffix}.asset";
                        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if(existing==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}
                    }
                    var go=new GameObject(mesh.name);go.transform.SetParent(root.transform,false);go.transform.localPosition=pivot;go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;go.AddComponent<KampungGrassQuality>();
                    var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.018f,new[]{renderer})});lod.RecalculateBounds();
                }
            }
            }
            finally{if(temporarySource)Object.DestroyImmediate(source);}
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
            var record=world.chunks.First(c=>c.coordinate==data.coordinate&&(world.brushAuthoredTerrain||c.region==5));string folder=Folder(world,data.coordinate),path=folder+"/PaintedVegetation.prefab";
            Region5EnvironmentAssets.EnsureFolder(folder);
            string backup="Library/Nusantara/VegetationPaintBackup/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            foreach(string file in new[]{path,folder+"/Essential.prefab",folder+"/Navigation.asset",WorldFoundationSetup.DefinitionPath})Backup(file,backup);
            var root=new GameObject("Hand painted vegetation");
            try
            {
                BuildVisuals(root,data,palette,true);PrefabUtility.SaveAsPrefabAsset(root,path);
                var settings=AddressableAssetSettingsDefaultObject.Settings;var entry=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path),settings.FindGroup("Nusantara World Chunks"),false,false);
                entry.address=$"world/{data.coordinate.x:D2}_{data.coordinate.y:D2}/painted-vegetation";
                record.decorationAddresses=record.decorationAddresses.Where(a=>!a.EndsWith("/painted-vegetation")).Concat(new[]{entry.address}).ToArray();
                string essentialPath=folder+"/Essential.prefab";
                bool existingEssential=AssetDatabase.LoadAssetAtPath<GameObject>(essentialPath)!=null;
                var essential=existingEssential?PrefabUtility.LoadPrefabContents(essentialPath):new GameObject("Essential");
                try
                {
                    var old=essential.transform.Find("Painted vegetation collision");if(old!=null)Object.DestroyImmediate(old.gameObject);
                    var colliders=new GameObject("Painted vegetation collision");colliders.transform.SetParent(essential.transform,false);
                    Vector3 origin=world.ChunkOrigin(data.coordinate);
                    foreach(var stamp in data.stamps.Where(s=>s.kind==VegetationPaintKind.Tree))
                    {
                        var go=new GameObject("Painted trunk collision");go.transform.SetParent(colliders.transform,false);go.transform.localPosition=stamp.position-origin;
                        var collider=go.AddComponent<CapsuleCollider>();collider.radius=(stamp.brushStyle!=null?stamp.brushStyle.trunkRadius:palette.treeCollisionRadius)*stamp.scale;collider.height=3;collider.center=Vector3.up*1.5f;
                    }
                    if(colliders.transform.childCount==0)Object.DestroyImmediate(colliders);
                    bool keep=!world.regionalGroundPlanes||essential.transform.childCount>0||essential.GetComponents<Component>().Any(c=>c is not Transform);
                    if(keep)
                    {
                        RebuildNavigation(essential,world,backup);PrefabUtility.SaveAsPrefabAsset(essential,essentialPath);
                        var collisionEntry=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(essentialPath),settings.FindGroup("Nusantara World Chunks"),false,false);
                        if(string.IsNullOrEmpty(record.essentialAddress))record.essentialAddress=$"world/{data.coordinate.x:D2}_{data.coordinate.y:D2}/essential";
                        collisionEntry.address=record.essentialAddress;
                    }
                    else if(existingEssential)
                    {settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(essentialPath),false);AssetDatabase.DeleteAsset(essentialPath);record.essentialAddress="";}
                }
                finally{if(existingEssential)PrefabUtility.UnloadPrefabContents(essential);else Object.DestroyImmediate(essential);}
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
