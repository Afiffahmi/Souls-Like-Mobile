using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Nusantara.World.Editor
{
    public static class WorldRegionalGroundAssets
    {
        public const string Root="Assets/NusantaraWorld/Models";
        public const string SceneRoot="Regional Ground";
        sealed class Samples {public Mesh mesh;public Vector3[] vertices;public int width,depth;public float spacing;}
        static readonly Dictionary<int,Samples> cache=new Dictionary<int,Samples>();
        public static void InvalidateCache()=>cache.Clear();
        public static string RegionFolder(int region)=>Root+"/Region"+region;
        public static string DetailFolder(WorldDefinition world,WorldChunkRecord record)
        {
            var region=world.regions.First(r=>r.id==record.region);var local=record.coordinate-region.chunkOffset;
            return RegionFolder(region.id)+$"/Details/{local.x:D2}_{local.y:D2}";
        }
        public static Mesh Mesh(WorldRegion region,bool distant=false)=>region.groundPrefab!=null?
            (distant?region.groundPrefab.GetComponent<WorldRegionGround>().distantGround:region.groundPrefab.GetComponent<WorldRegionGround>().ground):null;
        static WorldRegion Owner(WorldDefinition world,Vector2Int coordinate)=>world.regions.FirstOrDefault(r=>coordinate.x>=r.chunkOffset.x&&coordinate.y>=r.chunkOffset.y&&coordinate.x<r.chunkOffset.x+r.chunkGridSize.x&&coordinate.y<r.chunkOffset.y+r.chunkGridSize.y);
        public static bool TryCoordinate(WorldDefinition world,Mesh mesh,out Vector2Int coordinate)
        {
            var region=world.regions.FirstOrDefault(r=>Mesh(r)==mesh);coordinate=region!=null?region.chunkOffset:default;return region!=null;
        }
        public static float Sample(WorldGeographyProfile profile,float x,float z)
        {
            var world=profile.world;var region=Owner(world,world.WorldToChunk(new Vector3(x,0,z)));if(region==null)return profile.Height(x,z);
            if(!cache.TryGetValue(region.id,out var s))
            {
                var marker=region.groundPrefab.GetComponent<WorldRegionGround>();var mesh=marker.ground;
                s=new Samples{mesh=mesh,vertices=mesh.vertices,width=marker.cells.x*marker.nearIntervals+1,depth=marker.cells.y*marker.nearIntervals+1,spacing=world.chunkSize/marker.nearIntervals};cache[region.id]=s;
            }
            var origin=world.ChunkOrigin(region.chunkOffset);float gx=Mathf.Clamp((x-origin.x)/s.spacing,0,s.width-1),gz=Mathf.Clamp((z-origin.z)/s.spacing,0,s.depth-1);
            int ix=Mathf.Min(Mathf.FloorToInt(gx),s.width-2),iz=Mathf.Min(Mathf.FloorToInt(gz),s.depth-2);float u=gx-ix,v=gz-iz;int n=iz*s.width+ix;
            return u+v<=1?s.vertices[n].y+(s.vertices[n+1].y-s.vertices[n].y)*u+(s.vertices[n+s.width].y-s.vertices[n].y)*v:
                s.vertices[n+s.width+1].y+(s.vertices[n+s.width].y-s.vertices[n+s.width+1].y)*(1-u)+(s.vertices[n+1].y-s.vertices[n+s.width+1].y)*(1-v);
        }
        public static Mesh Tile(WorldDefinition world,WorldRegion region,Vector2Int local,bool distant=false)
        {
            var marker=region.groundPrefab.GetComponent<WorldRegionGround>();var source=distant?marker.distantGround:marker.ground;
            int intervals=distant?marker.distantIntervals:marker.nearIntervals,side=intervals+1,width=marker.cells.x*intervals+1;
            var profile=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            var mesh=WorldTerrainMesher.Build(world,region.chunkOffset+local,intervals,(_,__)=>profile.groundLevel+region.groundElevation);
            if(local.x>=marker.cells.x||local.y>=marker.cells.y)return mesh;
            var original=source.vertices;var originalNormals=source.normals;var originalUV=source.uv;var originalColors=source.colors;
            var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;var colors=originalColors.Length>0?new Color[vertices.Length]:null;
            for(int z=0;z<side;z++)for(int x=0;x<side;x++)
            {
                int index=z*side+x,n=(local.y*intervals+z)*width+local.x*intervals+x;
                vertices[index].y=original[n].y;if(originalNormals.Length==original.Length)normals[index]=originalNormals[n];if(originalUV.Length==original.Length)uv[index]=originalUV[n];if(colors!=null)colors[index]=originalColors[n];
            }
            mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;if(colors!=null)mesh.colors=colors;mesh.RecalculateBounds();return mesh;
        }
        public static void ApplyDelta(WorldGeographyProfile profile,Rect bounds,Func<float,float,float> delta,ISet<Mesh> recorded,ISet<Vector2Int> changed)
        {
            var world=profile.world;
            foreach(var region in world.regions)
            {
                var origin=world.ChunkOrigin(region.chunkOffset);var rect=new Rect(origin.x,origin.z,region.chunkGridSize.x*world.chunkSize,region.chunkGridSize.y*world.chunkSize);
                var expanded=bounds;expanded.xMin-=.51f;expanded.xMax+=.51f;expanded.yMin-=.51f;expanded.yMax+=.51f;if(!rect.Overlaps(expanded,true))continue;
                bool modified=false;
                foreach(var mesh in new[]{Mesh(region),Mesh(region,true)})
                    if(WorldTerrainSculptAssets.ApplyMeshDelta(mesh,origin,delta,()=>{if(recorded.Add(mesh))Undo.RegisterCompleteObjectUndo(mesh,"Sculpt regional ground");})){EditorUtility.SetDirty(mesh);modified=true;}
                if(!modified)continue;
                foreach(var record in world.chunks.Where(c=>c.region==region.id))
                {
                    var o=world.ChunkOrigin(record.coordinate);if(!new Rect(o.x,o.z,world.chunkSize,world.chunkSize).Overlaps(expanded,true))continue;
                    changed.Add(record.coordinate);if(!profile.sculptData.pendingChunks.Contains(record.coordinate))profile.sculptData.pendingChunks.Add(record.coordinate);
                }
            }
            EditorUtility.SetDirty(profile.sculptData);InvalidateCache();WorldTerrainSculptAssets.RefreshColliders();
        }
        public static string Save(WorldGeographyProfile profile)
        {
            var ids=profile.sculptData.pendingChunks.Select(c=>Owner(profile.world,c)?.id).Where(id=>id.HasValue).Select(id=>id.Value).Distinct().ToArray();
            foreach(var region in profile.world.regions.Where(r=>ids.Contains(r.id)))foreach(var mesh in new[]{Mesh(region),Mesh(region,true)})AssetDatabase.SaveAssetIfDirty(mesh);
            profile.sculptData.pendingChunks.Clear();EditorUtility.SetDirty(profile.sculptData);AssetDatabase.SaveAssetIfDirty(profile.sculptData);InvalidateCache();WorldTerrainSculptAssets.RefreshColliders();
            return $"Saved {ids.Length} continuous regional ground planes. Use Build AI Walking Ground after sculpting. Undo remains available.";
        }
        public static Mesh Join(WorldDefinition world,WorldRegion region,int intervals,Func<Vector2Int,Mesh> tile)
        {
            int width=region.chunkGridSize.x*intervals+1,depth=region.chunkGridSize.y*intervals+1,count=width*depth;
            var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];var colors=new Color[count];var filled=new bool[count];bool anyColors=false;
            for(int z=0;z<region.chunkGridSize.y;z++)for(int x=0;x<region.chunkGridSize.x;x++)
            {
                var source=tile(region.chunkOffset+new Vector2Int(x,z));if(source==null)throw new InvalidOperationException("Missing source ground in Region "+region.id);
                var sv=source.vertices;var sn=source.normals;var su=source.uv;var sc=source.colors;int side=intervals+1;
                if(sv.Length!=side*side)throw new InvalidOperationException("Unexpected ground resolution in Region "+region.id);
                anyColors|=sc.Length==sv.Length;
                for(int iz=0;iz<side;iz++)for(int ix=0;ix<side;ix++)
                {
                    int src=iz*side+ix,n=(z*intervals+iz)*width+x*intervals+ix;
                    var p=sv[src]+new Vector3(x*world.chunkSize,0,z*world.chunkSize);
                    if(filled[n]){if(Mathf.Abs(vertices[n].y-p.y)>world.seamTolerance)throw new InvalidOperationException("Source terrain has a height discontinuity at a cell seam in Region "+region.id);continue;}
                    vertices[n]=p;normals[n]=sn.Length==sv.Length?sn[src]:Vector3.up;uv[n]=su.Length==sv.Length?su[src]:new Vector2(p.x,p.z)/32;colors[n]=sc.Length==sv.Length?sc[src]:Color.white;filled[n]=true;
                }
            }
            var triangles=new int[(width-1)*(depth-1)*6];
            for(int z=0;z<depth-1;z++)for(int x=0;x<width-1;x++){int n=z*width+x,t=(z*(width-1)+x)*6;triangles[t]=n;triangles[t+1]=n+width;triangles[t+2]=n+1;triangles[t+3]=n+1;triangles[t+4]=n+width;triangles[t+5]=n+width+1;}
            var result=new Mesh{name=$"Region {region.id} continuous ground",indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};result.vertices=vertices;result.normals=normals;result.uv=uv;if(anyColors)result.colors=colors;result.triangles=triangles;result.RecalculateBounds();return result;
        }
        public static Mesh Store(Mesh mesh,string path)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
        }
        public static GameObject GroundPrefab(WorldDefinition world,WorldRegion region,Mesh near,Mesh distant,Material material,int nearIntervals,int distantIntervals)
        {
            var root=new GameObject($"Region {region.id} Ground");
            try
            {
                MeshRenderer Surface(string name,Mesh mesh,bool collision)
                {var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;return renderer;}
                var n=Surface("Terrain",near,true);var d=Surface("Distant Ground",distant,false);var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.18f,new[]{n}),new LOD(.001f,new[]{d})});lod.RecalculateBounds();
                var marker=root.AddComponent<WorldRegionGround>();marker.region=region.id;marker.cells=region.chunkGridSize;marker.nearIntervals=nearIntervals;marker.distantIntervals=distantIntervals;marker.ground=near;marker.distantGround=distant;
                if(region.floatingIsland)WorldRegionThemeAssets.AddIslandUnderside(root,region,near,material);
                return PrefabUtility.SaveAsPrefabAsset(root,RegionFolder(region.id)+"/Ground.prefab");
            }
            finally{Object.DestroyImmediate(root);}
        }
        public static void Rebuild(WorldDefinition world,WorldRegion region)
        {
            var marker=region.groundPrefab.GetComponent<WorldRegionGround>();
            var temporary=new List<Mesh>();
            Mesh TileAt(Vector2Int c,bool distant){var tile=Tile(world,region,c-region.chunkOffset,distant);temporary.Add(tile);return tile;}
            try
            {
                var near=Join(world,region,marker.nearIntervals,c=>TileAt(c,false));var far=Join(world,region,marker.distantIntervals,c=>TileAt(c,true));
                var material=region.groundPrefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                near=Store(near,RegionFolder(region.id)+"/Ground.asset");far=Store(far,RegionFolder(region.id)+"/GroundLOD.asset");
                region.groundPrefab=GroundPrefab(world,region,near,far,material,marker.nearIntervals,marker.distantIntervals);
            }
            finally{foreach(var mesh in temporary)Object.DestroyImmediate(mesh);}
        }
        public static void RefreshScene(WorldDefinition world)
        {
            var scene=SceneManager.GetActiveScene();var parent=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==SceneRoot);if(parent==null)parent=new GameObject(SceneRoot);
            foreach(Transform child in parent.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
            foreach(var region in world.regions){var instance=(GameObject)PrefabUtility.InstantiatePrefab(region.groundPrefab,parent.transform);instance.transform.position=world.ChunkOrigin(region.chunkOffset);}
            EditorSceneManager.MarkSceneDirty(scene);InvalidateCache();Physics.SyncTransforms();
        }
        [MenuItem("Tools/Nusantara/Consolidate Ground into Region Planes")]
        public static string Consolidate()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetActiveScene();var world=AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);var profile=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            if(scene.path!=WorldReferenceBuilder.ScenePath||world==null||!world.brushAuthoredTerrain)throw new InvalidOperationException("Open the brush world scene first.");
            if(world.regionalGroundPlanes)return "The world already has one ground plane per region.";
            var settings=AddressableAssetSettingsDefaultObject.Settings;
            string backup="Library/Nusantara/RegionalGroundBackup/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");Directory.CreateDirectory(backup);
            foreach(string path in AssetDatabase.FindAssets("",new[]{world.chunkAssetRoot}).Select(AssetDatabase.GUIDToAssetPath).Where(File.Exists)) {VegetationPaintAssets.Backup(path,backup);VegetationPaintAssets.Backup(path+".meta",backup);}
            foreach(string path in new[]{scene.path,WorldFoundationSetup.DefinitionPath,WorldReferenceBuilder.ProfilePath,AssetDatabase.GetAssetPath(settings)})VegetationPaintAssets.Backup(path,backup);
            foreach(var group in settings.groups.Where(g=>g!=null))VegetationPaintAssets.Backup(AssetDatabase.GetAssetPath(group),backup);
            foreach(var tool in Resources.FindObjectsOfTypeAll<WorldTerrainBrushWindow>())tool.DisablePainting();foreach(var tool in Resources.FindObjectsOfTypeAll<VegetationPainterWindow>())tool.Close();
            WorldTerrainSculptAssets.Save(profile);WorldSceneBookmarks.SaveLoadedBrushChunks();
            var oldFolders=world.chunks.ToDictionary(c=>c.coordinate,c=>WorldTerrainSculptAssets.Folder(world,c.coordinate));
            var nearMeshes=new Dictionary<int,Mesh>();var farMeshes=new Dictionary<int,Mesh>();
            try
            {
                // Join and validate all terrain BEFORE removing any source asset.
                foreach(var region in world.regions)
                {
                    nearMeshes[region.id]=Join(world,region,profile.terrainIntervals,c=>AssetDatabase.LoadAssetAtPath<Mesh>(oldFolders[c]+"/Terrain.asset"));
                    farMeshes[region.id]=Join(world,region,profile.distantIntervals,c=>AssetDatabase.LoadAssetAtPath<Mesh>(oldFolders[c]+"/DistantTerrain.asset"));
                }
                var preview=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="World preview (Editor only)");if(preview!=null)Object.DestroyImmediate(preview);
                foreach(var region in world.regions)
                {
                    string folder=RegionFolder(region.id);Region5EnvironmentAssets.EnsureFolder(folder);
                    var sample=world.chunks.First(c=>c.region==region.id);var source=AssetDatabase.LoadAssetAtPath<GameObject>(oldFolders[sample.coordinate]+"/Essential.prefab").GetComponentsInChildren<MeshRenderer>().First(r=>r.name=="Terrain").sharedMaterial;
                    var material=new Material(source){name=$"Region{region.id}Ground"};AssetDatabase.CreateAsset(material,folder+"/Ground.mat");
                    var near=Store(nearMeshes[region.id],folder+"/Ground.asset");nearMeshes.Remove(region.id);var far=Store(farMeshes[region.id],folder+"/GroundLOD.asset");farMeshes.Remove(region.id);
                    region.groundPrefab=GroundPrefab(world,region,near,far,material,profile.terrainIntervals,profile.distantIntervals);
                }
                foreach(var record in world.chunks)
                {
                    string folder=oldFolders[record.coordinate],essentialPath=folder+"/Essential.prefab";
                    var contents=PrefabUtility.LoadPrefabContents(essentialPath);bool keep;
                    try
                    {
                        var terrain=contents.transform.Find("Terrain");if(terrain!=null)Object.DestroyImmediate(terrain.gameObject);
                        var empty=contents.transform.Find("Painted vegetation collision");if(empty!=null&&empty.childCount==0)Object.DestroyImmediate(empty.gameObject);
                        keep=contents.transform.childCount>0||contents.GetComponents<Component>().Any(c=>c is not Transform);
                        if(keep)PrefabUtility.SaveAsPrefabAsset(contents,essentialPath);
                    }
                    finally{PrefabUtility.UnloadPrefabContents(contents);}
                    foreach(string file in new[]{"Distant.prefab","DistantTerrain.asset","Terrain.asset"})
                    {string path=folder+"/"+file;settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(path),false);if(File.Exists(path))AssetDatabase.DeleteAsset(path);}
                    record.distantAddress="";
                    if(!keep){settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(essentialPath),false);AssetDatabase.DeleteAsset(essentialPath);record.essentialAddress="";}
                    string destination=DetailFolder(world,record);record.assetFolder=destination;
                    bool hasFiles=AssetDatabase.FindAssets("",new[]{folder}).Select(AssetDatabase.GUIDToAssetPath).Any(File.Exists);
                    if(hasFiles)
                    {
                        Region5EnvironmentAssets.EnsureFolder(Path.GetDirectoryName(destination).Replace('\\','/'));string error=AssetDatabase.MoveAsset(folder,destination);if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
                    }
                    else AssetDatabase.DeleteAsset(folder);
                }
                string oldRoot=world.chunkAssetRoot;string sculptPath=AssetDatabase.GetAssetPath(profile.sculptData);
                string sculptError=AssetDatabase.MoveAsset(sculptPath,Root+"/SculptData.asset");if(!string.IsNullOrEmpty(sculptError))throw new InvalidOperationException(sculptError);
                world.chunkAssetRoot=Root;world.regionalGroundPlanes=true;
                foreach(string folder in new[]{oldRoot+"/Chunks",oldRoot+"/RegionChunks",oldRoot+"/VegetationPatches"})if(AssetDatabase.IsValidFolder(folder))AssetDatabase.DeleteAsset(folder);
                if(AssetDatabase.IsValidFolder(oldRoot)&&!AssetDatabase.FindAssets("",new[]{oldRoot}).Select(AssetDatabase.GUIDToAssetPath).Any(File.Exists))AssetDatabase.DeleteAsset(oldRoot);
                EditorUtility.SetDirty(world);EditorUtility.SetDirty(profile);EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
                RefreshScene(world);WorldTerrainSculptAssets.InvalidateCache();WorldSceneBookmarks.LoadPreview(world.playerStart,false);EditorSceneManager.SaveScene(scene);
                return $"Created six continuous region planes, each retaining its {world.regions[0].chunkGridSize.x}x{world.regions[0].chunkGridSize.y} authored area. Ground colours, sculpting and decoration GUIDs preserved. Backup: {backup}";
            }
            finally{foreach(var mesh in nearMeshes.Values.Concat(farMeshes.Values))if(mesh!=null&&!AssetDatabase.Contains(mesh))Object.DestroyImmediate(mesh);EditorUtility.ClearProgressBar();}
        }
    }
}
