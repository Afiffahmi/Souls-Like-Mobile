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
    public static class KampungVillageBuilder
    {
        public const string PlanPath="Assets/NusantaraWorld/KampungSungaiPlan.asset";
        static WorldGeographyProfile geography,previous;
        static WorldDefinition world,previousWorld;
        static KampungVillagePlan plan;
        static WorldChunkRecord[] chunks;
        static readonly Dictionary<string,GameObject> prefabs=new Dictionary<string,GameObject>();
        static readonly Dictionary<string,float> floorOffsets=new Dictionary<string,float>();
        static readonly Dictionary<string,Bounds> bounds=new Dictionary<string,Bounds>();
        static int cursor,placed;
        static string backup;
        static Scene staging;
        static bool running;
        public static string Status {get;private set;}="Ready.";
        static Vector2 C=>plan.center;
        static string Folder(Vector2Int coordinate)=>VegetationPaintAssets.Folder(world,coordinate);
        static Vector2[] Polygon(params Vector2[] offsets)=>offsets.Select(p=>C+p).ToArray();
        static Vector2 V(float x,float z)=>new Vector2(x,z);

        [MenuItem("Tools/Nusantara/Kampung Sungai/Build Village Layout")]
        public static void Start()
        {
            if(running||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use idle Edit mode.");
            if(SceneManager.GetActiveScene().path!=WorldReferenceBuilder.ScenePath)throw new InvalidOperationException("Open the existing world scene.");
            geography=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);world=geography.world;
            previous=Object.Instantiate(geography);previousWorld=Object.Instantiate(world);previous.world=previousWorld;
            backup="Library/Nusantara/KampungBackup/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            VegetationPaintAssets.Backup(WorldReferenceBuilder.ProfilePath,backup);VegetationPaintAssets.Backup(WorldFoundationSetup.DefinitionPath,backup);VegetationPaintAssets.Backup(PlanPath,backup);
            LoadAssets();CreatePlan();ApplyPlan();
            chunks=world.chunks.Where(c=>c.region==world.WetlandRegionId&&Intersects(c.coordinate)).ToArray();
            foreach(var record in chunks)
                foreach(string path in Directory.GetFiles(Folder(record.coordinate)).Where(p=>!p.EndsWith(".meta")))VegetationPaintAssets.Backup(path,backup);
            File.WriteAllText("Library/Nusantara/KampungAffected.txt",string.Join("\n",chunks.Select(c=>Folder(c.coordinate))));
            staging=EditorSceneManager.NewPreviewScene();cursor=placed=0;running=true;EditorApplication.update+=Advance;
            Status="Building Kampung Sungai in "+chunks.Length+" existing chunks.";Write();
        }
        static void LoadAssets()
        {
            prefabs.Clear();bounds.Clear();floorOffsets.Clear();
            foreach(string name in new[]{"long_house","stilt_house","wooden_walkway","wooden_bridge","dock","boat","barrel","crates","lily_pad","crocodile_statue"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Map/Props/Region5/"+name+".prefab");
                if(prefab==null)throw new InvalidOperationException("Missing source art "+name);prefabs[name]=prefab;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try{bounds[name]=KampungAssets.Bounds(go);floorOffsets[name]=FloorHeight(go,bounds[name]);}finally{Object.DestroyImmediate(go);}
            }
            for(int i=0;i<6;i++)prefabs["shore_rock_"+i]=AssetDatabase.LoadAssetAtPath<GameObject>(KampungAssets.Prepared+"/shore_rock_"+i+".prefab");
            for(int i=0;i<4;i++)prefabs["forest_fern_"+i]=AssetDatabase.LoadAssetAtPath<GameObject>(KampungAssets.Prepared+"/forest_fern_"+i+".prefab");
        }
        static float FloorHeight(GameObject go,Bounds b)
        {
            var levels=new Dictionary<int,float>();
            foreach(var f in go.GetComponentsInChildren<MeshFilter>())
            {
                var v=f.sharedMesh.vertices;var t=f.sharedMesh.triangles;
                for(int i=0;i<t.Length;i+=3)
                {
                    var a=f.transform.TransformPoint(v[t[i]]);var c=f.transform.TransformPoint(v[t[i+1]]);var d=f.transform.TransformPoint(v[t[i+2]]);
                    var n=Vector3.Cross(c-a,d-a);float y=(a.y+c.y+d.y)/3;
                    if(Mathf.Abs(n.normalized.y)<.97f||y>b.min.y+b.size.y*.7f)continue;
                    int key=Mathf.RoundToInt(y*10);levels.TryGetValue(key,out float area);levels[key]=area+n.magnitude*.5f;
                }
            }
            return levels.Count==0?b.min.y:levels.OrderByDescending(k=>k.Value).First().Key/10f;
        }
        static void CreatePlan()
        {
            plan=AssetDatabase.LoadAssetAtPath<KampungVillagePlan>(PlanPath);if(plan!=null)return;
            plan=ScriptableObject.CreateInstance<KampungVillagePlan>();plan.center=new Vector2(world.locations[0].position.x,world.locations[0].position.z);
            plan.footprint=Polygon(V(-132,8),V(-112,-68),V(-62,-116),V(38,-125),V(124,-74),V(159,-12),V(143,59),V(75,123),V(-25,114),V(-105,74));
            var sites=new[]{V(0,31),V(-14,9),V(15,11),V(-36,31),V(30,30),V(-45,9),V(-48,-13),V(-15,-34),V(16,-34),V(39,-18),V(54,9),V(43,52),V(-21,59)};
            plan.buildings=sites.Select((p,i)=>new KampungBuilding{id=i==0?"longhouse":"house-"+i.ToString("D2"),asset=i==0?"long_house":"stilt_house",offset=p,height=i==0?8:i<3?5.1f:5.4f+(i%3)*.3f,yaw=i==0?0:Mathf.Atan2(p.x,p.y)*Mathf.Rad2Deg,entrance=p-p.normalized*(i==0?8:6)}).ToArray();
            var paths=new List<WorldWalkingRoute>();float deck=geography.BaseHeight(C)+.075f;
            foreach(var building in plan.buildings)
            {
                var end=building.entrance;var mid=end*.55f+new Vector2(-end.y,end.x).normalized*2;
                paths.Add(new WorldWalkingRoute{name="Kampung / "+building.id,halfWidth=building.id=="longhouse"?2.5f:1.8f,points=Polygon(Vector2.zero,mid,end)});
            }
            paths.Add(new WorldWalkingRoute{name="Kampung / boat dock ramp",halfWidth=2,points=Polygon(V(84,0),V(96,-6),V(110,-14),V(126,-24)),surfaceHeights=new[]{geography.BaseHeight(C+V(84,0))+.075f,41f,38.5f,36.4f}});
            plan.paths=paths.ToArray();
            var platforms=new List<WorldWalkingPlatform>{new WorldWalkingPlatform{id="kampung-market",surface=deck,boundary=Polygon(V(-7,-6),V(-8,3),V(-4,7),V(6,7),V(9,2),V(7,-6))}};
            foreach(var building in plan.buildings)
            {
                Vector2 forward=(building.offset-building.entrance).normalized,side=new Vector2(-forward.y,forward.x);Vector2 e=building.entrance;
                platforms.Add(new WorldWalkingPlatform{id="kampung-porch-"+building.id,surface=geography.BaseHeight(C+e)+.075f,boundary=Polygon(e-side*3-forward*2,e-side*3+forward*2,e+side*3+forward*2,e+side*3-forward*2)});
            }
            platforms.Add(new WorldWalkingPlatform{id="kampung-dock",surface=36.4f,boundary=Polygon(V(122,-30),V(122,-19),V(130,-19),V(130,-30))});
            plan.platforms=platforms.ToArray();AssetDatabase.CreateAsset(plan,PlanPath);
        }
        static void ApplyPlan()
        {
            if(plan.footprint.Any(p=>!geography.InsideRegion(p,geography.world.WetlandRegionId,30)))throw new InvalidOperationException("Village footprint must stay inside Map 5.");
            world.locations[0].footprint=plan.footprint;
            geography.routes=geography.routes.Where(r=>!r.name.StartsWith("Kampung / ")).Concat(plan.paths).ToArray();
            geography.walkingPlatforms=geography.walkingPlatforms.Where(p=>!p.id.StartsWith("kampung-")).Concat(plan.platforms).ToArray();
            geography.buildingFoundations=geography.buildingFoundations.Where(p=>!p.id.StartsWith("kampung-")).Concat(plan.buildings.Select(b=>
            {
                float scale=b.height/bounds[b.asset].size.y;
                return new WorldBuildingFoundation{id="kampung-"+b.id,center=C+b.offset,radius=Mathf.Max(bounds[b.asset].size.x,bounds[b.asset].size.z)*scale*.55f+2,blend=24,height=geography.BaseHeight(C+b.offset)+.075f-floorOffsets[b.asset]*scale};
            })).ToArray();
            EditorUtility.SetDirty(world);EditorUtility.SetDirty(geography);EditorUtility.SetDirty(plan);
        }
        public static void RefineVillagePaths()
        {
            var profile=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);var authored=AssetDatabase.LoadAssetAtPath<KampungVillagePlan>(PlanPath);
            var originals=profile.routes.Where(r=>!r.name.StartsWith("Kampung / ")).ToArray();
            var small=authored.buildings[1];small.offset=V(-18,12);small.yaw=Mathf.Atan2(small.offset.x,small.offset.y)*Mathf.Rad2Deg;small.entrance=small.offset-small.offset.normalized*6;
            foreach(var building in authored.buildings)
            {
                var route=authored.paths.First(r=>r.name=="Kampung / "+building.id);Vector2 end=authored.center+building.entrance;
                if(building.id=="longhouse"){route.points=new[]{authored.center,authored.center+V(0,10),end};continue;}
                float nearest=float.MaxValue;Vector2 start=authored.center;
                foreach(var road in originals)for(int i=1;i<road.points.Length;i++)
                {
                    Vector2 a=road.points[i-1],d=road.points[i]-a;float t=Mathf.Clamp01(Vector2.Dot(end-a,d)/d.sqrMagnitude);Vector2 p=a+d*t;
                    if((p-end).sqrMagnitude<nearest){nearest=(p-end).sqrMagnitude;start=p;}
                }
                Vector2 direction=(end-start).normalized;route.points=new[]{start,(start+end)*.5f+new Vector2(-direction.y,direction.x)*.6f,end};
            }
            authored.platforms=authored.platforms.Where(p=>!p.id.StartsWith("kampung-porch-")).Concat(authored.buildings.Select(b=>
            {
                Vector2 e=authored.center+b.entrance,forward=(b.offset-b.entrance).normalized,side=new Vector2(-forward.y,forward.x);
                return new WorldWalkingPlatform{id="kampung-porch-"+b.id,surface=profile.BaseHeight(e)+.075f,boundary=new[]{e-side*3-forward*2,e-side*3+forward*2,e+side*3+forward*2,e+side*3-forward*2}};
            })).ToArray();
            EditorUtility.SetDirty(authored);AssetDatabase.SaveAssets();
        }
        static bool Intersects(Vector2Int coordinate)
        {
            Vector3 origin=world.ChunkOrigin(coordinate);
            float minX=plan.footprint.Min(p=>p.x)-65,maxX=plan.footprint.Max(p=>p.x)+65,minZ=plan.footprint.Min(p=>p.y)-65,maxZ=plan.footprint.Max(p=>p.y)+65;
            return origin.x<=maxX&&origin.x+world.chunkSize>=minX&&origin.z<=maxZ&&origin.z+world.chunkSize>=minZ;
        }
        static void Advance()
        {
            try
            {
                if(cursor>=chunks.Length){Finish();return;}
                Build(chunks[cursor++]);Status=$"Built {cursor}/{chunks.Length} Kampung chunks; {placed} source art props. Tree and grass painting is left to you.";Write();
            }
            catch(Exception exception){Status="Village build stopped: "+exception;Write();Stop();Debug.LogException(exception);}
        }
        static void Write(){Directory.CreateDirectory("Library/Nusantara");File.WriteAllText("Library/Nusantara/KampungBuild.txt",Status);}
        static void Stop()
        {
            running=false;EditorApplication.update-=Advance;if(staging.IsValid())EditorSceneManager.ClosePreviewScene(staging);
            if(previous!=null)Object.DestroyImmediate(previous);if(previousWorld!=null)Object.DestroyImmediate(previousWorld);AssetDatabase.SaveAssets();
        }
        static bool InVillage(Vector2 p)=>WorldGeographyProfile.Inside(p,plan.footprint);
        static void Build(WorldChunkRecord record)
        {
            string folder=Folder(record.coordinate);Vector3 origin=world.ChunkOrigin(record.coordinate);
            foreach(string file in new[]{"Terrain.asset","DistantTerrain.asset"})
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/"+file);var vertices=mesh.vertices;var normals=mesh.normals;var colors=new Color[vertices.Length];var tangents=new Vector4[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector2 p=new Vector2(origin.x+vertices[i].x,origin.z+vertices[i].z);
                    vertices[i].y+=geography.Height(p.x,p.y)-previous.Height(p.x,p.y);
                    float Delta(Vector2 v)=>geography.Height(v.x,v.y)-previous.Height(v.x,v.y);
                    if(normals[i].y>.01f)
                    {
                        float dx=Delta(p+Vector2.right*.5f)-Delta(p-Vector2.right*.5f),dz=Delta(p+Vector2.up*.5f)-Delta(p-Vector2.up*.5f);
                        normals[i]=new Vector3(normals[i].x/normals[i].y-dx,1,normals[i].z/normals[i].y-dz).normalized;
                    }
                    float wet=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((geography.WaterDistance(p,out _)-5)/75));
                    float worn=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((geography.RouteDistance(p)+1)/7));
                    colors[i]=new Color(wet,worn,.15f,geography.VillageInfluence(p));tangents[i]=new Vector4(1,0,0,-1);
                }
                mesh.vertices=vertices;mesh.colors=colors;mesh.normals=normals;mesh.tangents=tangents;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            }
            CleanDecorations(folder,origin);
            var essential=PrefabUtility.LoadPrefabContents(folder+"/Essential.prefab");
            var decoration=new GameObject("Kampung Sungai authored village");SceneManager.MoveGameObjectToScene(decoration,staging);
            try
            {
                foreach(var child in essential.transform.Cast<Transform>().ToArray())
                {
                    Vector2 p=new Vector2(origin.x+child.localPosition.x,origin.z+child.localPosition.z);
                    if(child.name.StartsWith("Kampung ")||(child.name.EndsWith(" collision")&&InVillage(p)))Object.DestroyImmediate(child.gameObject);
                }
                var oldCollision=essential.transform.Find("Region 5 enhancement collision");
                if(oldCollision!=null)foreach(var collider in oldCollision.GetComponentsInChildren<Collider>().ToArray())
                {
                    var position=essential.transform.InverseTransformPoint(collider.bounds.center);
                    if(InVillage(new Vector2(origin.x+position.x,origin.z+position.z)))Object.DestroyImmediate(collider.gameObject);
                }
                var terrainRenderer=essential.transform.Find("Terrain").GetComponent<MeshRenderer>();terrainRenderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/KampungGround.mat");
                BuildPaths(essential,decoration.transform,record.coordinate,origin,folder);
                BuildWater(essential,origin,folder);
                BuildPlatforms(essential,origin,folder);
                AddBuildings(decoration.transform,essential.transform,record.coordinate,origin);
                AddDetails(decoration.transform,essential.transform,record.coordinate,origin);
                VegetationPaintAssets.RebuildNavigation(essential,world,backup);
                PrefabUtility.SaveAsPrefabAsset(essential,folder+"/Essential.prefab");
                string path=folder+"/Kampung.prefab";PrefabUtility.SaveAsPrefabAsset(decoration,path);
                var settings=AddressableAssetSettingsDefaultObject.Settings;var entry=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path),settings.FindGroup("Nusantara World Chunks"),false,false);
                entry.address=$"world/{record.coordinate.x:D2}_{record.coordinate.y:D2}/kampung-sungai";
                record.decorationAddresses=record.decorationAddresses.Where(a=>!a.EndsWith("/kampung-sungai")).Concat(new[]{entry.address}).ToArray();
                var distant=PrefabUtility.LoadPrefabContents(folder+"/Distant.prefab");
                try{var renderer=distant.GetComponentInChildren<MeshRenderer>();if(renderer!=null)renderer.sharedMaterial=terrainRenderer.sharedMaterial;PrefabUtility.SaveAsPrefabAsset(distant,folder+"/Distant.prefab");}finally{PrefabUtility.UnloadPrefabContents(distant);}
            }
            finally{PrefabUtility.UnloadPrefabContents(essential);Object.DestroyImmediate(decoration);}
        }
        static void CleanDecorations(string folder,Vector3 origin)
        {
            foreach(string file in new[]{"Props.prefab","Enhancement.prefab"})
            {
                string path=folder+"/"+file;if(!File.Exists(path))continue;var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var child in root.transform.Cast<Transform>().ToArray())
                    {
                        var vfx=child.GetComponent<WorldCanopyVfx>();
                        if(vfx!=null)
                        {
                            var keep=Enumerable.Range(0,vfx.crowns.Length).Where(i=>!InVillage(new Vector2(origin.x+vfx.crowns[i].x,origin.z+vfx.crowns[i].z))).ToArray();
                            vfx.crowns=keep.Select(i=>vfx.crowns[i]).ToArray();vfx.radii=keep.Select(i=>vfx.radii[i]).ToArray();vfx.Rebuild();continue;
                        }
                        Vector2 p=new Vector2(origin.x+child.localPosition.x,origin.z+child.localPosition.z);
                        if(InVillage(p)&&child.GetComponent<WorldLandmarkIdentity>()==null)Object.DestroyImmediate(child.gameObject);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
        }
        static bool Owns(Vector2 p,Vector2Int coordinate)=>world.WorldToChunk(new Vector3(p.x,0,p.y))==coordinate;
        static GameObject Place(string asset,Vector2 p,float y,float yaw,float scale,Transform parent,Vector3 origin,string name)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[asset],parent);go.name=name;go.transform.localPosition=new Vector3(p.x-origin.x,y,p.y-origin.z);go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
            foreach(var collider in go.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);placed++;return go;
        }
        static Mesh SaveMesh(string path,WorldGenerationGeometry geometry)
        {
            var built=geometry.Mesh(Path.GetFileNameWithoutExtension(path));var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=built;AssetDatabase.CreateAsset(mesh,path);}else{EditorUtility.CopySerialized(built,mesh);Object.DestroyImmediate(built);EditorUtility.SetDirty(mesh);}return mesh;
        }
        static GameObject MeshObject(GameObject root,string name,Mesh mesh,Material material,bool collision)
        {
            var old=root.transform.Find(name);var go=old!=null?old.gameObject:new GameObject(name);go.transform.SetParent(root.transform,false);
            var filter=go.GetComponent<MeshFilter>();if(filter==null)filter=go.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();
            if(material!=null){if(renderer==null)renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.enabled=true;}else if(renderer!=null)renderer.enabled=false;
            if(collision){var collider=go.GetComponent<MeshCollider>();if(collider==null)collider=go.AddComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=mesh;}return go;
        }
        public static string RestoreInterrupted(string directory)
        {
            if(running)throw new InvalidOperationException("Stop the build before restoring.");
            const string temporary="Assets/NusantaraWorld/KampungRestoreTemp";Region5EnvironmentAssets.EnsureFolder(temporary);
            int restored=0;
            try
            {
                foreach(string source in Directory.GetFiles(directory,"*",SearchOption.AllDirectories).Where(p=>p.EndsWith(".asset")||p.EndsWith(".prefab")))
                {
                    string target=source.Substring(directory.Length+1),copy=temporary+"/"+Path.GetFileName(source);
                    File.Copy(source,copy,true);AssetDatabase.ImportAsset(copy,ImportAssetOptions.ForceSynchronousImport);
                    if(target.EndsWith(".prefab"))
                    {var root=PrefabUtility.LoadPrefabContents(copy);try{PrefabUtility.SaveAsPrefabAsset(root,target);}finally{PrefabUtility.UnloadPrefabContents(root);}}
                    else
                    {var original=AssetDatabase.LoadMainAssetAtPath(target);var saved=AssetDatabase.LoadMainAssetAtPath(copy);if(original!=null&&saved!=null){EditorUtility.CopySerialized(saved,original);EditorUtility.SetDirty(original);}}
                    AssetDatabase.DeleteAsset(copy);restored++;
                }
                AssetDatabase.SaveAssets();return "Restored "+restored+" assets through Unity.";
            }
            finally{AssetDatabase.DeleteAsset(temporary);}
        }
        static void BuildPaths(GameObject essential,Transform decoration,Vector2Int coordinate,Vector3 origin,string folder)
        {
            var road=new WorldGenerationGeometry();var timber=new WorldGenerationGeometry();var rails=new WorldGenerationGeometry();
            foreach(var route in geography.routes)for(int segment=1;segment<route.points.Length;segment++)
            {
                Vector2 a=route.points[segment-1],b=route.points[segment],direction=(b-a).normalized,normal=new Vector2(-direction.y,direction.x);
                float length=Vector2.Distance(a,b);int count=Mathf.CeilToInt(length/4);
                for(int i=0;i<count;i++)
                {
                    Vector2 p=Vector2.Lerp(a,b,i/(float)count),q=Vector2.Lerp(a,b,(i+1)/(float)count),mid=(p+q)*.5f;
                    if(mid.x<origin.x-12||mid.x>origin.x+world.chunkSize+12||mid.y<origin.z-12||mid.y>origin.z+world.chunkSize+12)continue;
                    float w0=geography.RouteHalfWidth(route,p),w1=geography.RouteHalfWidth(route,q);Vector3 Point(Vector2 v)=>new Vector3(v.x-origin.x,geography.RouteHeight(route,v),v.y-origin.z);
                    bool bridge=geography.VillageInfluence(mid)>.5f
                        ?geography.WaterDistance(mid,out _)<20
                        :geography.WaterDistance(mid,out _)<65;
                    (bridge?timber:road).Quad(Point(p+normal*w0),Point(q+normal*w1),Point(q-normal*w1),Point(p-normal*w0));
                    if(!bridge)continue;
                    foreach(float sign in new[]{-1f,1f})
                    {
                        Vector2 edge=mid+normal*sign*((w0+w1)*.5f-.22f);
                        bool junction=geography.routes.Any(other=>other!=route&&WorldGeographyProfile.LineDistance(edge,other.points)<geography.RouteHalfWidth(other,edge)+.5f)||geography.walkingPlatforms.Any(platform=>WorldGeographyProfile.Inside(edge,platform.boundary));
                        if(junction)continue;
                        rails.Box(Point(edge)+Vector3.up*.65f,new Vector3(.3f,1.3f,Vector2.Distance(p,q)+.06f),Quaternion.Euler(0,Mathf.Atan2(direction.x,direction.y)*Mathf.Rad2Deg,0));
                    }
                }
                // Existing walkway artwork dresses the most visible local boardwalks.
                if(!route.name.StartsWith("Kampung / ")||route.name.Contains("ramp"))continue;
                float scale=route.halfWidth*2/bounds["wooden_walkway"].size.x,tile=bounds["wooden_walkway"].size.z*scale;
                for(float d=tile*.5f;d<length;d+=tile)
                {
                    Vector2 p=Vector2.Lerp(a,b,d/length);if(!Owns(p,coordinate)||Vector2.Distance(p,C)<12||geography.WaterDistance(p,out _)>45)continue;
                    float deck=geography.RouteHeight(route,p);
                    Place("wooden_walkway",p,deck-floorOffsets["wooden_walkway"]*scale,Mathf.Atan2(direction.x,direction.y)*Mathf.Rad2Deg,scale,decoration,origin,"Kampung timber boardwalk");
                }
            }
            MeshObject(essential,"Connected paths",SaveMesh(folder+"/Paths.asset",road),VillagePathMaterial(),true);
            MeshObject(essential,"Smooth bridges and boardwalks",SaveMesh(folder+"/Bridges.asset",timber),AssetDatabase.LoadAssetAtPath<Material>(WorldAssetLayout.MaterialPath("Bridge timber")),true);
            // Physical edge guards remain in the essential chunk, independent of decoration streaming.
            MeshObject(essential,"Bridge rail collision",SaveMesh(folder+"/Rails.asset",rails),null,true);
        }
        static Material VillagePathMaterial()
        {
            var material=KampungAssets.Material("KampungPath","Nusantara/Kampung Worn Path");
            material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(KampungAssets.Textures+"/forrest_sand_01_Diffuse.jpg"));
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(KampungAssets.Textures+"/forrest_sand_01_nor_gl.jpg"));
            material.SetTexture("_Rough",AssetDatabase.LoadAssetAtPath<Texture2D>(KampungAssets.Textures+"/forrest_sand_01_Rough.jpg"));EditorUtility.SetDirty(material);return material;
        }
        static void BuildWater(GameObject essential,Vector3 origin,string folder)
        {
            var barriers=new WorldGenerationGeometry();const float step=2;int count=Mathf.RoundToInt(world.chunkSize/step);
            for(int z=0;z<count;z++)
            {
                int run=-1;float runTop=0;
                for(int x=0;x<=count;x++)
                {
                    bool blocked=false;float top=0;
                    if(x<count)
                    {
                        Vector2 p=new Vector2(origin.x+(x+.5f)*step,origin.z+(z+.5f)*step);
                        blocked=geography.IsForbiddenWater(p)&&geography.RouteDistance(p)>2&&!geography.walkingPlatforms.Any(platform=>WorldGeographyProfile.Inside(p,platform.boundary)||WorldGeographyProfile.LineDistance(p,platform.boundary,true)<2);
                        blocked&=!geography.buildingFoundations.Any(f=>Vector2.Distance(f.center,p)<f.radius+2);
                        geography.WaterDistance(p,out float surface);top=geography.CoastDistance(p)<65?60:surface+24;
                    }
                    if(blocked&&run<0){run=x;runTop=top;}
                    if(run>=0&&(!blocked||Mathf.Abs(top-runTop)>1||x==count))
                    {
                        barriers.Box(new Vector3((run+x)*step*.5f,(runTop-20)*.5f,(z+.5f)*step),new Vector3((x-run)*step,runTop+20,step+.01f),Quaternion.identity);run=blocked?x:-1;runTop=top;
                    }
                }
            }
            MeshObject(essential,"Water entry blockers",SaveMesh(folder+"/WaterBarrier.asset",barriers),null,true);
        }
        static List<Vector2> Clip(List<Vector2> input,int axis,float edge,bool greater)
        {
            var result=new List<Vector2>();if(input.Count==0)return result;Vector2 previous=input[input.Count-1];
            float Value(Vector2 p)=>axis==0?p.x:p.y;bool Inside(Vector2 p)=>greater?Value(p)>=edge:Value(p)<=edge;
            foreach(var current in input)
            {
                bool a=Inside(previous),b=Inside(current);if(a!=b)result.Add(Vector2.Lerp(previous,current,(edge-Value(previous))/(Value(current)-Value(previous))));
                if(b)result.Add(current);previous=current;
            }
            return result;
        }
        static void BuildPlatforms(GameObject essential,Vector3 origin,string folder)
        {
            var geometry=new WorldGenerationGeometry();var court=new WorldGenerationGeometry();
            foreach(var platform in plan.platforms)
            {
                var polygon=platform.boundary.ToList();polygon=Clip(polygon,0,origin.x,true);polygon=Clip(polygon,0,origin.x+world.chunkSize,false);polygon=Clip(polygon,1,origin.z,true);polygon=Clip(polygon,1,origin.z+world.chunkSize,false);
                float area=0;for(int i=0;i<polygon.Count;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Count];area+=a.x*b.y-b.x*a.y;}if(area>0)polygon.Reverse();
                Vector3 Point(Vector2 p)=>new Vector3(p.x-origin.x,platform.surface,p.y-origin.z);
                var target=platform.id=="kampung-market"?court:geometry;
                for(int i=1;i<polygon.Count-1;i++)target.Quad(Point(polygon[0]),Point(polygon[i]),Point(polygon[i+1]),Point(polygon[i+1]));
            }
            MeshObject(essential,"Kampung walking platforms",SaveMesh(folder+"/KampungPlatforms.asset",geometry),AssetDatabase.LoadAssetAtPath<Material>(WorldAssetLayout.MaterialPath("Bridge timber")),true);
            MeshObject(essential,"Kampung walking market court",SaveMesh(folder+"/KampungCourt.asset",court),VillagePathMaterial(),true);
        }
        static void AddBuildings(Transform decoration,Transform essential,Vector2Int coordinate,Vector3 origin)
        {
            foreach(var building in plan.buildings)
            {
                Vector2 p=C+building.offset;if(!Owns(p,coordinate))continue;
                float scale=building.height/bounds[building.asset].size.y,deck=geography.BaseHeight(p)+.075f,y=deck-floorOffsets[building.asset]*scale;
                Place(building.asset,p,y,building.yaw,scale,decoration,origin,"Kampung "+building.id+" - "+building.asset);
                var box=new GameObject("Kampung "+building.id+" wall collision");box.transform.SetParent(essential,false);box.transform.localPosition=new Vector3(p.x-origin.x,y+building.height*.5f,p.y-origin.z);box.transform.localRotation=Quaternion.Euler(0,building.yaw,0);
                var collider=box.AddComponent<BoxCollider>();collider.size=new Vector3(bounds[building.asset].size.x*scale*.82f,building.height,bounds[building.asset].size.z*scale*.72f);
                Vector2 e=C+building.entrance;
                if(Owns(e,coordinate))
                {
                    float porchScale=4/bounds["wooden_walkway"].size.x;
                    Place("wooden_walkway",e,deck-floorOffsets["wooden_walkway"]*porchScale,building.yaw,porchScale,decoration,origin,"Kampung "+building.id+" porch");
                    Vector2 side=new Vector2(Mathf.Cos(building.yaw*Mathf.Deg2Rad),-Mathf.Sin(building.yaw*Mathf.Deg2Rad));
                    Place("barrel",e+side*2.5f,deck,building.yaw,.65f,decoration,origin,"Kampung household barrel");
                    Place("crates",e-side*2.6f,deck,building.yaw,.55f,decoration,origin,"Kampung household stores");
                }
            }
            if(Owns(C,coordinate))
            {
                var marker=new GameObject("Kampung Sungai main village");marker.transform.SetParent(decoration,false);marker.transform.localPosition=new Vector3(C.x-origin.x,geography.BaseHeight(C),C.y-origin.z);marker.AddComponent<WorldLandmarkIdentity>().stableId="kampung-sungai-authored-layout";
                foreach(float sign in new[]{-1f,1f})for(int i=0;i<3;i++)
                {Vector2 p=C+V(sign*9,16+i*3);Place(i%2==0?"crates":"barrel",p,geography.BaseHeight(p)+.1f,sign*90,i%2==0?.7f:.65f,decoration,origin,"Kampung market wares");}
            }
        }
        static void AddDetails(Transform decoration,Transform essential,Vector2Int coordinate,Vector3 origin)
        {
            Vector2 dock=C+V(126,-24);
            if(Owns(dock,coordinate))
            {
                float scale=.8f;Place("dock",dock,36.4f-floorOffsets["dock"]*scale,0,scale,decoration,origin,"Kampung boat dock");
                Place("boat",dock+V(7,-6),34.6f,20,.75f,decoration,origin,"Kampung moored sampan");
                Place("boat",dock+V(-5,-10),34.6f,-18,.62f,decoration,origin,"Kampung fishing boat");
                Place("barrel",dock+V(-2,2),36.45f,0,.65f,decoration,origin,"Kampung dock supplies");
            }
            // Curated banks and outskirts use actual source meshes. Trees and grass are deliberately unpainted.
            var random=new System.Random(plan.seed+coordinate.x*137+coordinate.y*521);var centers=new[]{V(-75,42),V(-70,-37),V(-30,-70),V(35,-73),V(88,-39),V(88,62),V(12,89),V(-61,81)};
            foreach(var offset in centers)
            {
                Vector2 center=C+offset;
                for(int i=0;i<9;i++)
                {
                    float angle=(float)random.NextDouble()*Mathf.PI*2,distance=(float)random.NextDouble()*8;
                    Vector2 p=center+V(Mathf.Cos(angle),Mathf.Sin(angle))*distance;
                    if(!Owns(p,coordinate)||!InVillage(p)||geography.IsForbiddenWater(p)||geography.RouteDistance(p)<4)continue;
                    if(plan.buildings.Any(b=>Vector2.Distance(C+b.offset,p)<12))continue;
                    string asset=i<3?"shore_rock_"+(i%6):"forest_fern_"+(i%4);
                    float y=geography.Height(p.x,p.y);float scale=i<3?.65f+(float)random.NextDouble()*.7f:1+(float)random.NextDouble()*.5f;
                    Place(asset,p,y-.03f,(float)random.NextDouble()*360,scale,decoration,origin,"Kampung bank "+asset);
                }
            }
        }
        static void Finish()
        {
            EditorUtility.SetDirty(world);EditorUtility.SetDirty(geography);AssetDatabase.SaveAssets();
            Status=$"Kampung Sungai layout saved: {chunks.Length} chunks, one longhouse, {plan.buildings.Length-1} small houses, connected porches, market court, dock ramp, {placed} source art props. No new trees or grass painted. Backup: {backup}.";Write();Stop();
            var player=GameObject.Find("Player");if(player!=null)WorldSceneBookmarks.LoadPreview(player.transform.position,false);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Debug.Log(Status);
        }
    }
}
