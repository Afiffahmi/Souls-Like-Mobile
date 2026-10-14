using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Nusantara.World.Editor
{
    public static class Region5CollisionFinishing
    {
        static readonly HashSet<string> solid=new HashSet<string>{"market_stall","guardian_shrine","moss_rock","ruined_walls","root_formation","fallen_log","rainforest_tree","giant_crocodile_skulls"};
        static WorldDefinition world;
        static WorldChunkRecord[] chunks;
        static int cursor,count;
        static string backup;
        static bool running;
        public static string Status {get;private set;}="Ready.";
        [MenuItem("Tools/Nusantara/Finish Region 5 Prop Collision")]
        public static void Start()
        {
            if(running||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use idle Edit mode.");
            world=AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);chunks=world.chunks.Where(c=>c.region==world.WetlandRegionId).ToArray();cursor=count=0;
            backup="Library/Nusantara/Region5CollisionBackup/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");running=true;EditorApplication.update+=Advance;Status="Building essential prop collision.";
        }
        static void Backup(string path)
        {if(!File.Exists(path))return;string to=backup+"/"+path;Directory.CreateDirectory(Path.GetDirectoryName(to));if(!File.Exists(to))File.Copy(path,to);}
        static void Advance()
        {
            try
            {
                if(cursor>=chunks.Length)
                {Status=$"Saved {count} simple essential colliders in {chunks.Length} Region 5 chunks and updated navigation. Scenic islands and small plants have no colliders. Backup: {backup}.";Stop();Write();return;}
                Apply(chunks[cursor++]);Status=$"Collision and navigation: {cursor}/{chunks.Length} chunks, {count} solid props.";
                if(cursor%32==0){AssetDatabase.SaveAssets();Write();}
            }
            catch(Exception e){Status="Collision finishing stopped: "+e;Stop();Write();Debug.LogException(e);}
        }
        static void Write(){Directory.CreateDirectory("Library/Nusantara");File.WriteAllText("Library/Nusantara/Region5Collision.txt",Status);}
        static void Stop(){running=false;EditorApplication.update-=Advance;AssetDatabase.SaveAssets();}
        static void Apply(WorldChunkRecord chunk)
        {
            string folder=$"{WorldReferenceBuilder.Root}/Chunks/{chunk.coordinate.x:D2}_{chunk.coordinate.y:D2}";
            string path=folder+"/Enhancement.prefab";if(!File.Exists(path))return;
            var props=PrefabUtility.LoadPrefabContents(path);var essential=PrefabUtility.LoadPrefabContents(folder+"/Essential.prefab");
            try
            {
                var old=essential.transform.Find("Region 5 enhancement collision");bool hadOld=old!=null;
                if(old!=null)Object.DestroyImmediate(old.gameObject);
                var root=new GameObject("Region 5 enhancement collision");root.transform.SetParent(essential.transform,false);
                foreach(Transform prop in props.transform)
                {
                    if(prop.name.StartsWith("region5-scenic-island"))continue;
                    string source=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prop.gameObject);
                    string name=Path.GetFileNameWithoutExtension(source);if(!solid.Contains(name))continue;
                    var renderers=prop.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)continue;
                    Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    var blocker=new GameObject(prop.name+" collision");blocker.transform.SetParent(root.transform,false);
                    var box=blocker.AddComponent<BoxCollider>();
                    if(name=="rainforest_tree")
                    {blocker.transform.localPosition=prop.localPosition+Vector3.up*3;box.size=new Vector3(2,6,2)*prop.localScale.x;}
                    else {blocker.transform.localPosition=bounds.center;box.size=bounds.size*.85f;}
                    count++;
                }
                if(root.transform.childCount==0&&!hadOld)return;
                Backup(folder+"/Essential.prefab");
                var navigation=essential.GetComponent<WorldChunkNavigation>();
                if(navigation!=null&&navigation.bakedData.Length>0&&navigation.bakedData[0]!=null)
                {
                    var sources=new List<NavMeshBuildSource>();var markups=new List<NavMeshBuildMarkup>();
                    foreach(var collider in essential.GetComponentsInChildren<Collider>())
                        if(collider.name!="Terrain"&&!collider.name.Contains("Connected paths")&&!collider.name.Contains("Smooth bridges")&&!collider.name.Contains("Arena floor"))
                            markups.Add(new NavMeshBuildMarkup{root=collider.transform,overrideArea=true,area=1});
                    NavMeshBuilder.CollectSources(essential.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,markups,sources);
                    var settings=NavMesh.GetSettingsByIndex(0);settings.agentSlope=world.maxWalkableSlope;settings.agentClimb=world.maxStepHeight;settings.agentRadius=world.agentRadius;settings.agentHeight=world.agentHeight;
                    settings.overrideVoxelSize=true;settings.voxelSize=.4f;settings.overrideTileSize=true;settings.tileSize=256;
                    Backup(AssetDatabase.GetAssetPath(navigation.bakedData[0]));
                    NavMeshBuilder.UpdateNavMeshData(navigation.bakedData[0],settings,sources,new Bounds(new Vector3(world.chunkSize/2,150,world.chunkSize/2),new Vector3(world.chunkSize,400,world.chunkSize)));
                    EditorUtility.SetDirty(navigation.bakedData[0]);
                }
                PrefabUtility.SaveAsPrefabAsset(essential,folder+"/Essential.prefab");
            }
            finally{PrefabUtility.UnloadPrefabContents(props);PrefabUtility.UnloadPrefabContents(essential);}
        }
    }
}
