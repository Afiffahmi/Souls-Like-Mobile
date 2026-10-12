using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nusantara.World.Editor
{
    public static class Region5Enhancement
    {
        const string ProfilePath="Assets/NusantaraWorld/Region5Biomes.asset";
        const string BatchName="Region 5 reference enhancement";
        static WorldGeographyProfile geography;
        static WorldDefinition world;
        static Region5BiomeProfile biomeProfile;
        static WorldChunkRecord[] chunks;
        static readonly Dictionary<string,GameObject> prefabs=new Dictionary<string,GameObject>();
        static readonly List<Vector2> placements=new List<Vector2>();
        static readonly List<Vector2> buildings=new List<Vector2>();
        static readonly List<string> findings=new List<string>();
        static readonly HashSet<string> landmarkIds=new HashSet<string>();
        static Scene staging;
        static Vector3 origin;
        static Vector2Int coordinate;
        static Mesh terrain;
        static Vector3[] terrainVertices;
        static Vector3[] oldTerrainVertices;
        static float oldRelief;
        static int cursor,added,removedSkulls,pruned,uniqueCount;
        static string backup;
        static bool running;
        public static string Status {get;private set;}="Ready.";

        [MenuItem("Tools/Nusantara/Enhance Existing Region 5")]
        public static void Start()
        {
            if(running)throw new InvalidOperationException("An enhancement pass is already running.");
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode before editing chunks.");
            if(SceneManager.GetActiveScene().path!=WorldReferenceBuilder.ScenePath)throw new InvalidOperationException("Open the existing NusantaraQuest_World scene.");
            geography=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            world=geography.world;
            if(world==null||world.chunks.Length==0)throw new InvalidOperationException("Existing generated chunks are required.");
            chunks=world.chunks.Where(c=>c.region==5).OrderBy(c=>c.coordinate.y).ThenBy(c=>c.coordinate.x).ToArray();
            backup="Library/Nusantara/Region5Backup/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);
            Backup(WorldFoundationSetup.DefinitionPath);Backup(WorldReferenceBuilder.ProfilePath);
            Region5EnvironmentAssets.Create();LoadPrefabs();CreateProfile();
            foreach(var rule in biomeProfile.biomes)
                if(rule.props.Contains("giant_crocodile_skulls")||rule.props.Contains("scenic_island"))
                    throw new InvalidOperationException("Unique boss landmarks and registered islands must be authored explicitly, never added to biome prop lists.");
            PlanIslands();
            oldRelief=geography.region5ReliefStrength;geography.region5ReliefStrength=24;EditorUtility.SetDirty(geography);
            cursor=added=removedSkulls=pruned=uniqueCount=0;landmarkIds.Clear();findings.Clear();
            staging=EditorSceneManager.NewPreviewScene();running=true;EditorApplication.update+=Advance;
            Status="Enhancing existing Region 5 chunks: 0/"+chunks.Length;WriteStatus();
        }
        static void Backup(string path)
        {
            if(!File.Exists(path))return;string destination=backup+"/"+path;Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if(!File.Exists(destination))File.Copy(path,destination);
        }
        static void LoadPrefabs()
        {
            prefabs.Clear();
            foreach(string folder in new[]{"Assets/Models/Map/Props/Region5",Region5EnvironmentAssets.Folder})
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{folder}))
            {var path=AssetDatabase.GUIDToAssetPath(guid);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);prefabs[Path.GetFileNameWithoutExtension(path)]=prefab;}
        }
        static void CreateProfile()
        {
            biomeProfile=AssetDatabase.LoadAssetAtPath<Region5BiomeProfile>(ProfilePath);
            if(biomeProfile!=null)return;
            biomeProfile=ScriptableObject.CreateInstance<Region5BiomeProfile>();
            biomeProfile.biomes=new[]{
                new Region5BiomeRule{name="Rainforest",props=new[]{"fern_cluster","broadleaf_bush","moss_rock","forest_mushrooms","fallen_log"},clustersPerChunk=8,seed=17},
                new Region5BiomeRule{name="Riverbank",props=new[]{"river_reeds","moss_rock","fern_cluster","fallen_log"},clustersPerChunk=10,riverDistance=new Vector2(42,100),seed=29},
                new Region5BiomeRule{name="Swamp",props=new[]{"root_formation","river_reeds","broadleaf_bush","moss_rock"},clustersPerChunk=9,riverDistance=new Vector2(42,220),seed=43},
                new Region5BiomeRule{name="Village",props=new[]{"barrel","crates","fern_cluster"},clustersPerChunk=4,seed=53},
                new Region5BiomeRule{name="Ruins",props=new[]{"moss_rock","fern_cluster","broadleaf_bush","ruined_walls"},clustersPerChunk=6,seed=61},
                new Region5BiomeRule{name="Boss Arena",props=new[]{"root_formation","moss_rock","river_reeds"},clustersPerChunk=4,seed=71}
            };
            AssetDatabase.CreateAsset(biomeProfile,ProfilePath);
        }
        static void PlanIslands()
        {
            var list=new List<WorldScenicIsland>();var random=new System.Random(biomeProfile.seed);
            // Only existing Map 5 channels and basins: the future-map arrows/outer scenery are excluded.
            foreach(var water in geography.waterways.Where(w=>w.name.Contains("Delta")||w.name.Contains("Bujang")||w.name.Contains("Swamp")||w.name.Contains("Sarang")))
            {
                int segments=Mathf.Max(1,water.points.Length-1);
                for(int k=0;k<segments;k++)for(int sample=0;sample<5;sample++)
                {
                    if(list.Count>=biomeProfile.maximumIslands)break;
                    float t=(sample+.5f)/5;
                    Vector2 p=water.points.Length==1?water.points[0]+new Vector2(Mathf.Cos(sample*1.7f),Mathf.Sin(sample*1.7f))*water.halfWidth*.55f:Vector2.Lerp(water.points[k],water.points[k+1],t);
                    float radius=water.halfWidth>100?24+Random(random,0,10):Mathf.Min(18,water.halfWidth*.38f);
                    p+=new Vector2(Random(random,-8,8),Random(random,-8,8));
                    if(!geography.InsideRegion(p,5,radius+biomeProfile.boundaryInset)||geography.WaterDistance(p,out float y)>-radius-6||geography.RouteDistance(p)<radius+32)continue;
                    if(!chunks.Any(c=>c.coordinate==world.WorldToChunk(new Vector3(p.x,0,p.y))))continue;
                    if(list.Any(i=>Vector2.Distance(i.center,p)<i.radius+radius+30))continue;
                    if(world.locations.Any(l=>Vector2.Distance(new Vector2(l.position.x,l.position.z),p)<l.clearRadius+radius+20))continue;
                    list.Add(new WorldScenicIsland{id="region5-scenic-island-"+list.Count.ToString("D2"),center=p,radius=radius,waterLevel=y});
                }
            }
            geography.scenicIslands=list.ToArray();EditorUtility.SetDirty(geography);
        }
        static float Random(System.Random r,float min,float max)=>(float)(min+r.NextDouble()*(max-min));
        static void Advance()
        {
            if(!running)return;
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Enhancement interrupted by Play mode.");
                if(cursor>=chunks.Length){Finish();return;}
                EnhanceChunk(chunks[cursor]);cursor++;
                Status=$"Enhanced {cursor}/{chunks.Length} existing Region 5 chunks; {added} new details; {geography.scenicIslands.Length} scenic islands.";
                if(cursor%16==0) {AssetDatabase.SaveAssets();WriteStatus();}
            }
            catch(Exception e){Stop();Status="Enhancement stopped: "+e;WriteStatus();Debug.LogException(e);}
        }
        static void Stop()
        {running=false;EditorApplication.update-=Advance;if(staging.IsValid())EditorSceneManager.ClosePreviewScene(staging);AssetDatabase.SaveAssets();}
        static void WriteStatus()
        {Directory.CreateDirectory("Library/Nusantara");File.WriteAllText("Library/Nusantara/Region5Enhancement.txt",Status);}
        static string Folder(Vector2Int c)=>$"{WorldReferenceBuilder.Root}/Chunks/{c.x:D2}_{c.y:D2}";
        static void EnhanceChunk(WorldChunkRecord record)
        {
            coordinate=record.coordinate;origin=world.ChunkOrigin(coordinate);string folder=Folder(coordinate);
            terrain=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/Terrain.asset");terrainVertices=terrain!=null?terrain.vertices:Array.Empty<Vector3>();
            oldTerrainVertices=(Vector3[])terrainVertices.Clone();
            bool terrainChanged=ApplyRelief(terrain,folder+"/Terrain.asset");terrainVertices=terrain!=null?terrain.vertices:Array.Empty<Vector3>();
            ApplyRelief(AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/DistantTerrain.asset"),folder+"/DistantTerrain.asset");
            buildings.Clear();placements.Clear();
            CleanExistingProps(folder+"/Props.prefab");
            UpdateEssential(folder,terrainChanged);
            string path=folder+"/Enhancement.prefab";Backup(path);
            var root=new GameObject(BatchName);SceneManager.MoveGameObjectToScene(root,staging);
            try
            {
                PopulateBiomes(root.transform);AddIslands(root.transform);AddLandmarkDetails(root.transform);
                AddCanopy(root.transform,folder+"/Props.prefab");
                if(root.transform.childCount==0)return;
                PrefabUtility.SaveAsPrefabAsset(root,path);
                var settings=AddressableAssetSettingsDefaultObject.Settings;var group=settings.FindGroup("Nusantara World Chunks");
                var entry=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path),group,false,false);
                entry.address=$"world/{coordinate.x:D2}_{coordinate.y:D2}/region5-enhancement";
                record.decorationAddresses=record.decorationAddresses.Where(a=>!a.EndsWith("/region5-enhancement")).Concat(new[]{entry.address}).ToArray();
            }
            finally{Object.DestroyImmediate(root);}
        }
        static void CleanExistingProps(string path)
        {
            if(!File.Exists(path))return;Backup(path);var root=PrefabUtility.LoadPrefabContents(path);bool changed=false;
            try
            {
                foreach(var child in root.transform.Cast<Transform>().ToArray())
                {
                    Vector2 p=new Vector2(origin.x+child.localPosition.x,origin.z+child.localPosition.z);
                    if(child.name.StartsWith("giant_crocodile_skulls")){Object.DestroyImmediate(child.gameObject);removedSkulls++;changed=true;continue;}
                    if(!geography.InsideRegion(p,5)){Object.DestroyImmediate(child.gameObject);pruned++;changed=true;continue;}
                    if(!child.name.StartsWith("boat")&&!child.name.StartsWith("lily_pad")&&!child.name.StartsWith("dock")&&child.GetComponent<ParticleSystem>()==null)
                    {float delta=Ground(p)-GroundFrom(p,oldTerrainVertices);if(Mathf.Abs(delta)>.001f){child.localPosition+=Vector3.up*delta;changed=true;}}
                    if(child.name.Contains("house")||child.name.StartsWith("dock"))buildings.Add(p);
                }
                if(changed)PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static float Ground(Vector2 p)
            => GroundFrom(p,terrainVertices);
        static float GroundFrom(Vector2 p,Vector3[] vertices)
        {
            if(vertices.Length==0)return geography.Height(p.x,p.y);
            int side=Mathf.RoundToInt(Mathf.Sqrt(vertices.Length));int intervals=side-1;
            float x=Mathf.Clamp((p.x-origin.x)/world.chunkSize*intervals,0,intervals-.0001f);
            float z=Mathf.Clamp((p.y-origin.z)/world.chunkSize*intervals,0,intervals-.0001f);
            int ix=(int)x,iz=(int)z;float u=x-ix,v=z-iz;int n=iz*side+ix;
            return u+v<=1?vertices[n].y+(vertices[n+1].y-vertices[n].y)*u+(vertices[n+side].y-vertices[n].y)*v:
                vertices[n+side+1].y+(vertices[n+side].y-vertices[n+side+1].y)*(1-u)+(vertices[n+1].y-vertices[n+side+1].y)*(1-v);
        }
        static bool ApplyRelief(Mesh mesh,string path)
        {
            if(mesh==null||Mathf.Approximately(oldRelief,geography.region5ReliefStrength))return false;
            var vertices=mesh.vertices;var normals=mesh.normals;bool changed=false;
            float Delta(Vector2 p)=>geography.Region5Relief(p,geography.region5ReliefStrength)-geography.Region5Relief(p,oldRelief);
            for(int i=0;i<vertices.Length;i++)
            {
                var p=new Vector2(origin.x+vertices[i].x,origin.z+vertices[i].z);float d=Delta(p);
                if(Mathf.Abs(d)<.0001f)continue;vertices[i].y+=d;changed=true;
                if(i<normals.Length&&normals[i].y>.01f)
                {
                    float dx=Delta(p+Vector2.right*.5f)-Delta(p-Vector2.right*.5f);
                    float dz=Delta(p+Vector2.up*.5f)-Delta(p-Vector2.up*.5f);
                    normals[i]=new Vector3(normals[i].x/normals[i].y-dx,1,normals[i].z/normals[i].y-dz).normalized;
                }
            }
            if(!changed)return false;Backup(path);mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return true;
        }
        static void UpdateEssential(string folder,bool terrainChanged)
        {
            string path=folder+"/Essential.prefab";var root=PrefabUtility.LoadPrefabContents(path);bool changed=terrainChanged;
            try
            {
                Vector2 boss=new Vector2(world.locations[8].position.x,world.locations[8].position.z)+new Vector2(0,-120);
                foreach(var child in root.transform.Cast<Transform>().ToArray())
                {
                    Vector2 p=new Vector2(origin.x+child.localPosition.x,origin.z+child.localPosition.z);
                    if(child.name.StartsWith("giant_crocodile_skulls")&&Vector2.Distance(p,boss)>2)
                    {Object.DestroyImmediate(child.gameObject);changed=true;continue;}
                    if(child.name.EndsWith(" collision")&&child.GetComponent<BoxCollider>()!=null)
                    {float delta=Ground(p)-GroundFrom(p,oldTerrainVertices);if(Mathf.Abs(delta)>.001f){child.localPosition+=Vector3.up*delta;changed=true;}}
                }
                if(!changed)return;Backup(path);
                var navigation=root.GetComponent<WorldChunkNavigation>();
                if(navigation!=null&&navigation.bakedData.Length>0&&navigation.bakedData[0]!=null)
                {
                    var sources=new List<NavMeshBuildSource>();var markups=new List<NavMeshBuildMarkup>();
                    foreach(var collider in root.GetComponentsInChildren<Collider>())
                        if(collider.name!="Terrain"&&!collider.name.Contains("Connected paths")&&!collider.name.Contains("Smooth bridges")&&!collider.name.Contains("Arena floor"))
                            markups.Add(new NavMeshBuildMarkup{root=collider.transform,overrideArea=true,area=1});
                    NavMeshBuilder.CollectSources(root.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,markups,sources);
                    var settings=NavMesh.GetSettingsByIndex(0);settings.agentSlope=world.maxWalkableSlope;settings.agentClimb=world.maxStepHeight;settings.agentRadius=world.agentRadius;settings.agentHeight=world.agentHeight;
                    settings.overrideVoxelSize=true;settings.voxelSize=.4f;settings.overrideTileSize=true;settings.tileSize=256;
                    Backup(AssetDatabase.GetAssetPath(navigation.bakedData[0]));
                    NavMeshBuilder.UpdateNavMeshData(navigation.bakedData[0],settings,sources,new Bounds(new Vector3(world.chunkSize/2,150,world.chunkSize/2),new Vector3(world.chunkSize,400,world.chunkSize)));
                    EditorUtility.SetDirty(navigation.bakedData[0]);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static bool Owns(Vector2 p)=>world.WorldToChunk(new Vector3(p.x,0,p.y))==coordinate;
        static bool Allowed(Vector2 p,float radius=4,float pathClearance=18,bool clearings=true)
        {
            if(!Owns(p)||!geography.InsideRegion(p,5,biomeProfile.boundaryInset+radius)||geography.CoastDistance(p)<radius+80||geography.WaterDistance(p,out _)<42+radius||geography.RouteDistance(p)<pathClearance+radius)return false;
            if(clearings&&world.locations.Any(l=>Vector2.Distance(p,new Vector2(l.position.x,l.position.z))<l.clearRadius+15))return false;
            if(buildings.Any(b=>Vector2.Distance(b,p)<20+radius))return false;
            return !placements.Any(b=>Vector2.Distance(b,p)<radius+3);
        }
        static GameObject Place(string asset,Vector2 p,float y,float yaw,float scale,Transform parent,string label=null)
        {
            if(!prefabs.TryGetValue(asset,out var prefab))throw new InvalidOperationException("Missing reusable prop "+asset);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.name=label??asset+" "+parent.childCount.ToString("D3");
            go.transform.localPosition=new Vector3(p.x-origin.x,y,p.y-origin.z);go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
            // Enhancements are decoration; essential collision and existing navigation stay resident.
            foreach(var collider in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            added++;return go;
        }
        static Region5BiomeRule Biome(Vector2 p)
        {
            float D(int i)=>Vector2.Distance(p,new Vector2(world.locations[i].position.x,world.locations[i].position.z));
            string name=D(0)<350?"Village":D(3)<550?"Swamp":D(5)<450?"Ruins":D(8)<300?"Boss Arena":geography.WaterDistance(p,out _)<100?"Riverbank":"Rainforest";
            return biomeProfile.biomes.First(b=>b.name==name);
        }
        static void PopulateBiomes(Transform parent)
        {
            var random=new System.Random(unchecked(biomeProfile.seed+coordinate.x*73856093+coordinate.y*19349663));
            for(int attempt=0;attempt<32;attempt++)
            {
                Vector2 center=new Vector2(origin.x+Random(random,12,world.chunkSize-12),origin.z+Random(random,12,world.chunkSize-12));var rule=Biome(center);
                if(attempt>=rule.clustersPerChunk*3||!Allowed(center,rule.minimumSpacing,rule.pathClearance))continue;
                var clusterRandom=new System.Random(random.Next()^rule.seed);
                for(int k=0;k<4;k++)
                {
                    Vector2 p=center+new Vector2(Random(clusterRandom,-10,10),Random(clusterRandom,-10,10));
                    float water=geography.WaterDistance(p,out _),h=Ground(p);
                    if(!Allowed(p,rule.minimumSpacing,rule.pathClearance)||water<rule.riverDistance.x||water>rule.riverDistance.y||h<rule.heightRange.x||h>rule.heightRange.y)continue;
                    if(buildings.Any(b=>Vector2.Distance(b,p)<rule.buildingClearance))continue;
                    float dx=geography.Height(p.x+1,p.y)-geography.Height(p.x-1,p.y),dz=geography.Height(p.x,p.y+1)-geography.Height(p.x,p.y-1);
                    if(Mathf.Atan(new Vector2(dx,dz).magnitude/2)*Mathf.Rad2Deg>rule.maxSlope)continue;
                    string asset=rule.props[(attempt+k)%rule.props.Length];float scale=Random(clusterRandom,rule.scaleRange.x,rule.scaleRange.y);
                    Place(asset,p,h,Random(clusterRandom,0,360),scale,parent);placements.Add(p);
                }
            }
        }
        static void AddIslands(Transform parent)
        {
            foreach(var island in geography.scenicIslands)
            {
                if(!Owns(island.center))continue;
                var go=Place("scenic_island",island.center,island.waterLevel,0,island.radius,parent,island.id+" - unreachable scenery");
                go.AddComponent<WorldLandmarkIdentity>().stableId=island.id;
                Register(island.id);var random=new System.Random(biomeProfile.seed+Array.IndexOf(geography.scenicIslands,island)*137);
                for(int i=0;i<5;i++)
                {
                    float a=i*2.4f;float d=island.radius*(i==0?.08f:.38f);Vector2 p=island.center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*d;
                    float h=island.waterLevel+island.radius*.25f;
                    string asset=i<2?"rainforest_tree":i==2?"moss_rock":"root_formation";
                    Place(asset,p,h,Random(random,0,360),i<2?Random(random,.5f,.8f):Random(random,1.5f,2.5f),parent,island.id+" "+asset);
                }
            }
        }
        static void Register(string id)
        {if(!landmarkIds.Add(id))throw new InvalidOperationException("Duplicate unique landmark: "+id);uniqueCount++;}
        static GameObject LandmarkRoot(string id,Transform parent)
        {
            Register(id);var root=new GameObject(id);root.transform.SetParent(parent,false);root.AddComponent<WorldLandmarkIdentity>().stableId=id;return root;
        }
        static void AddLandmarkDetails(Transform parent)
        {
            for(int index=0;index<world.locations.Length;index++)
            {
                var location=world.locations[index];Vector2 center=new Vector2(location.position.x,location.position.z);
                if(Owns(center))LandmarkRoot(location.id+"-environment",parent);
                void Detail(string asset,Vector2 offset,float yaw=0,float scale=1,float radius=3)
                {
                    Vector2 p=center+offset;if(!Allowed(p,radius,10,false))return;
                    Place(asset,p,Ground(p),yaw,scale,parent,location.name+" - "+asset);placements.Add(p);
                }
                if(index==0)
                {
                    for(int row=-3;row<=3;row++)for(int side=-1;side<=1;side+=2)
                    {Detail("market_stall",new Vector2(side*38,row*32),side*90);Detail("fishing_net",new Vector2(side*115,row*34),side*90);Detail(row%2==0?"barrel":"crates",new Vector2(side*43,row*32+8),row*15);}
                    Detail("guardian_shrine",new Vector2(0,118),0,.8f);
                }
                if(index==1)
                {
                    // One ancient grove, with the user's existing bare tree and VFX canopy.
                    Vector2 grove=center+new Vector2(-65,55);
                    if(Owns(grove)&&Allowed(grove,20,20,false))
                    {
                        var tree=Place("rainforest_tree",grove,Ground(grove),25,2.1f,parent,"Ancient Tree Grove - unique banyan");
                        tree.AddComponent<WorldLandmarkIdentity>().stableId="region5-ancient-tree-grove";Register("region5-ancient-tree-grove");
                    }
                    for(int i=0;i<12;i++) {float a=i*2.4f;Detail(i%3==0?"root_formation":"fern_cluster",new Vector2(-65,55)+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*28,i*30,i%3==0?1.6f:2);}
                }
                if(index==2)
                    for(int i=0;i<20;i++){float a=i*2.4f;Detail(i%3==0?"fallen_log":"river_reeds",new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(85+i*4),i*20,.8f);}
                if(index==3)
                    for(int i=0;i<24;i++){float a=i*2.4f;Detail(i%4==0?"fallen_log":i%3==0?"river_reeds":"root_formation",new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(90+i*5),i*23,i%3==0?2:1.5f);}
                if(index==4)
                {
                    for(int i=-3;i<=3;i++){Detail("moss_rock",new Vector2(i*44,135),i*17,7,24);Detail("fern_cluster",new Vector2(i*40,85),i*20,2);}
                    Detail("guardian_shrine",new Vector2(-160,-10),90,1.5f,8);
                    Detail("guardian_shrine",new Vector2(155,-10),270,1,8);
                }
                if(index==5)
                    for(int i=0;i<16;i++){float a=i*Mathf.PI*2/16;Vector2 p=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*105;
                        Detail(i%4==0?"guardian_shrine":i%2==0?"ruined_walls":"moss_rock",p,-a*Mathf.Rad2Deg,1);
                        Detail("hanging_vines",p+new Vector2(3,4),i*30,1.8f);Detail("fern_cluster",p+new Vector2(-5,4),i*35,1.3f);}
                if(index==6&&Owns(center))
                {
                    // Cave furniture stays against the walls, away from the central walking corridor.
                    for(int side=-1;side<=1;side+=2)for(int i=-2;i<=2;i++)
                    {
                        Vector2 p=center+new Vector2(side*10,i*17);if(!geography.InsideRegion(p,5,10))continue;
                        Place("cave_crystals",p,geography.BaseHeight(center)+.1f,i*27,.8f,parent,"Gua Sunyi - glowing crystals");
                    }
                }
                if(index==7)
                {
                    for(int i=0;i<9;i++){float a=i*2.4f;Detail("crocodile_nest",new Vector2(Mathf.Cos(a),Mathf.Sin(a))*60,i*30,1.3f,5);}
                    Detail("small_rib_bones",new Vector2(50,30),35,1.8f,10);Detail("fallen_log",new Vector2(-50,-20),120,1.7f,7);
                }
                if(index==8)
                {
                    Vector2 p=center+new Vector2(0,-120);
                    if(Owns(p)&&geography.InsideRegion(p,5,45)&&!geography.IsForbiddenWater(p))
                    {
                        var skull=Place("giant_crocodile_skulls",p,Ground(p),0,1.5f,parent,"Bujang Senang - sole giant crocodile landmark");
                        skull.AddComponent<WorldLandmarkIdentity>().stableId="region5-main-crocodile";Register("region5-main-crocodile");
                    }
                    for(int i=0;i<18;i++){float a=i*Mathf.PI*2/18;Detail(i%3==0?"guardian_shrine":"root_formation",new Vector2(Mathf.Cos(a),Mathf.Sin(a))*165,-a*Mathf.Rad2Deg,i%3==0?1.4f:2,10);}
                }
            }
        }
        static void AddCanopy(Transform parent,string existingPath)
        {
            var crowns=new List<Vector3>();var radii=new List<float>();
            void Collect(Transform root)
            {
                foreach(var tree in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("rainforest_tree ")||t.name.Contains("unique banyan")||t.name.Contains(" rainforest_tree")))
                {
                    var source=tree.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>!r.name.Contains("Distance"));if(source==null)continue;
                    var bounds=source.bounds;var crown=new Vector3(bounds.center.x,bounds.max.y-2*tree.lossyScale.y,bounds.center.z);
                    crowns.Add(root.InverseTransformPoint(crown));radii.Add(Mathf.Clamp(Mathf.Max(bounds.size.x,bounds.size.z)*.3f,3,10));
                }
            }
            Collect(parent);
            if(File.Exists(existingPath))
            {var root=PrefabUtility.LoadPrefabContents(existingPath);try{Collect(root.transform);}finally{PrefabUtility.UnloadPrefabContents(root);}}
            if(crowns.Count==0)return;
            string matPath=WorldAssetLayout.Materials+"/Region5_canopy_vfx.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(material==null)
            {
                material=Region5EnvironmentAssets.RefreshCanopyMaterial();
            }
            var go=new GameObject("Canopy leaves VFX - shared chunk batch");go.transform.SetParent(parent,false);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.maxParticles=384;main.startSpeed=0;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.maxParticleSize=.12f;
            var vfx=go.AddComponent<WorldCanopyVfx>();vfx.crowns=crowns.ToArray();vfx.radii=radii.ToArray();vfx.seed=unchecked(world.seed+coordinate.x*137+coordinate.y*521);vfx.Rebuild();
        }
        static void Finish()
        {
            EditorUtility.SetDirty(world);AssetDatabase.SaveAssets();
            Status=$"Enhanced {chunks.Length} existing Region 5 chunks; {added} reusable details; {geography.scenicIslands.Length} unreachable scenic islands; removed {removedSkulls} old giant skull placements and {pruned} outside-region props. Unique IDs: {uniqueCount}. Backup: {backup}.";
            WriteStatus();Stop();
            Debug.Log(Status);
        }
        [MenuItem("Tools/Nusantara/Repair Region 5 Bridge Junctions")]
        public static string RepairBridgeJunctions()
        {
            if(running||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use idle Edit mode.");
            geography=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);world=geography.world;
            backup="Library/Nusantara/Region5BridgeBackup/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            int pieces=0,affected=0;
            foreach(var chunk in world.chunks.Where(c=>c.region==5))
            {
                coordinate=chunk.coordinate;origin=world.ChunkOrigin(coordinate);string folder=Folder(coordinate);
                string path=folder+"/Rails.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null)continue;
                var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
                if(vertices.Length%24!=0)throw new InvalidOperationException("Preserving unfamiliar authored rail mesh: "+path);
                var keptVertices=new List<Vector3>();var keptNormals=new List<Vector3>();var keptUv=new List<Vector2>();var triangles=new List<int>();
                int removed=0;
                for(int start=0;start<vertices.Length;start+=24)
                {
                    Vector3 center=Vector3.zero;for(int i=0;i<24;i++)center+=vertices[start+i]/24;
                    // A rail is 0.6 m inside its OWN route edge. A deeper point is inside a
                    // connecting corridor, where retaining that rail seals the junction.
                    Vector2 p=new Vector2(center.x+origin.x,center.z+origin.z);
                    if(geography.RouteDistance(p)<-2){removed++;continue;}
                    int first=keptVertices.Count;
                    for(int i=0;i<24;i++){keptVertices.Add(vertices[start+i]);keptNormals.Add(normals[start+i]);keptUv.Add(uv[start+i]);}
                    for(int face=0;face<6;face++){int a=first+face*4;triangles.AddRange(new[]{a,a+1,a+2,a,a+2,a+3});}
                }
                if(removed==0)continue;Backup(path);mesh.Clear();mesh.SetVertices(keptVertices);mesh.SetNormals(keptNormals);mesh.SetUVs(0,keptUv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                terrain=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/Terrain.asset");terrainVertices=terrain.vertices;oldTerrainVertices=(Vector3[])terrainVertices.Clone();
                UpdateEssential(folder,true);pieces+=removed;affected++;
            }
            AssetDatabase.SaveAssets();return $"Removed {pieces} rail pieces obstructing connecting paths in {affected} Region 5 chunks; updated their navigation. Backup: {backup}.";
        }
        [MenuItem("Tools/Nusantara/Polish Sacred Waterfall")]
        public static string PolishWaterfall()
        {
            if(running||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use idle Edit mode.");
            geography=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);world=geography.world;LoadPrefabs();
            var location=world.locations[4];coordinate=world.WorldToChunk(location.position);origin=world.ChunkOrigin(coordinate);
            string path=Folder(coordinate)+"/Enhancement.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            backup="Library/Nusantara/Region5WaterfallBackup/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Backup(path);
            try
            {
                var old=root.transform.Find("Sacred waterfall cliff dressing");if(old!=null)Object.DestroyImmediate(old.gameObject);
                var dressing=new GameObject("Sacred waterfall cliff dressing");dressing.transform.SetParent(root.transform,false);
                Vector2 center=new Vector2(location.position.x,location.position.z);
                for(int i=-5;i<=5;i++)
                {
                    Vector2 p=center+new Vector2(i*27,110+(i%3)*2);if(!geography.InsideRegion(p,5,45))continue;
                    var rock=Place("moss_rock",p,35,i*19,1,dressing.transform,"Waterfall mossy rock face "+i);
                    rock.transform.localScale=new Vector3(8.5f,22+Mathf.Abs(i)*1.3f,6);
                    if(i%2==0)Place("hanging_vines",p+new Vector2(-7,-16),70,i*15,5,dressing.transform,"Waterfall hanging roots");
                    Place("fern_cluster",p,89+Mathf.Abs(i)*3,i*31,3,dressing.transform,"Cliff crest ferns");
                }
                var fog=new GameObject("Sacred waterfall spray - 48 particle cap");fog.transform.SetParent(dressing.transform,false);fog.transform.localPosition=new Vector3(center.x-origin.x,38,center.y-origin.z+70);
                var ps=fog.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.loop=true;main.prewarm=true;main.duration=8;main.startLifetime=8;main.startSpeed=.6f;main.startSize=new ParticleSystem.MinMaxCurve(8,15);main.startColor=new Color(.65f,.86f,.84f,.20f);main.maxParticles=48;main.simulationSpace=ParticleSystemSimulationSpace.Local;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(140,2,15);
                var emission=ps.emission;emission.rateOverTime=4;var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(WorldAssetLayout.MaterialPath("SwampMist"));renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            return "Saved irregular mossy waterfall faces, hanging roots, crest ferns and localized spray within Region 5.";
        }
        [MenuItem("Tools/Nusantara/Finish Circular Boss Arena")]
        public static string FinishArena()
        {
            if(running||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use idle Edit mode.");
            geography=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);world=geography.world;
            backup="Library/Nusantara/Region5ArenaBackup/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Vector2 center=new Vector2(world.locations[8].position.x,world.locations[8].position.z);float floor=geography.BaseHeight(center)+.06f;
            string materialPath="Assets/Materials/Region5_arena_stone.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null)
            {material=new Material(Shader.Find(WorldAssetLayout.LitShader)){name="Sarang arena stone",enableInstancing=true};WorldAssetLayout.SetColor(material,new Color(.72f,.74f,.66f));WorldAssetLayout.SetTexture(material,AssetDatabase.LoadAssetAtPath<Texture>(WorldAssetLayout.UtilityTexturePath("GroundGrain.asset")));AssetDatabase.CreateAsset(material,materialPath);}
            int affected=0;
            foreach(var chunk in world.chunks.Where(c=>c.region==5))
            {
                coordinate=chunk.coordinate;origin=world.ChunkOrigin(coordinate);
                if(center.x+82<origin.x||center.x-82>origin.x+world.chunkSize||center.y+82<origin.z||center.y-82>origin.z+world.chunkSize)continue;
                var geometry=new WorldGenerationGeometry();
                // Clip each circular wedge to its owning chunk; all pieces share the same floor.
                List<Vector2> ClipPolygon(List<Vector2> input,int axis,float edge,bool greater)
                {
                    var output=new List<Vector2>();if(input.Count==0)return output;Vector2 previous=input[input.Count-1];
                    float Value(Vector2 p)=>axis==0?p.x:p.y;
                    bool Inside(Vector2 p)=>greater?Value(p)>=edge:Value(p)<=edge;
                    foreach(var current in input)
                    {
                        bool a=Inside(previous),b=Inside(current);
                        if(a!=b){float t=(edge-Value(previous))/(Value(current)-Value(previous));output.Add(Vector2.Lerp(previous,current,t));}
                        if(b)output.Add(current);previous=current;
                    }
                    return output;
                }
                const int slices=64;
                for(int i=0;i<slices;i++)
                {
                    float a=i*Mathf.PI*2/slices,b=(i+1)*Mathf.PI*2/slices;
                    var polygon=new List<Vector2>{center,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*80,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*80};
                    polygon=ClipPolygon(polygon,0,origin.x,true);polygon=ClipPolygon(polygon,0,origin.x+world.chunkSize,false);
                    polygon=ClipPolygon(polygon,1,origin.z,true);polygon=ClipPolygon(polygon,1,origin.z+world.chunkSize,false);
                    Vector3 P(Vector2 p)=>new Vector3(p.x-origin.x,floor,p.y-origin.z);
                    for(int j=1;j<polygon.Count-1;j++)geometry.Quad(P(polygon[0]),P(polygon[j]),P(polygon[j+1]),P(polygon[j+1]));
                }
                if(geometry.Empty)continue;string folder=Folder(coordinate),meshPath=folder+"/BossArenaFloor.asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);var built=geometry.Mesh("Sarang circular arena floor");
                if(mesh==null){mesh=built;AssetDatabase.CreateAsset(mesh,meshPath);}else{Backup(meshPath);EditorUtility.CopySerialized(built,mesh);Object.DestroyImmediate(built);EditorUtility.SetDirty(mesh);}
                var essential=PrefabUtility.LoadPrefabContents(folder+"/Essential.prefab");
                try
                {
                    var old=essential.transform.Find("Sarang Arena floor - seamless walking");var go=old!=null?old.gameObject:new GameObject("Sarang Arena floor - seamless walking");go.transform.SetParent(essential.transform,false);
                    var filter=go.GetComponent<MeshFilter>();if(filter==null)filter=go.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
                    var renderer=go.GetComponent<MeshRenderer>();if(renderer==null)renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                    var collider=go.GetComponent<MeshCollider>();if(collider==null)collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
                    Backup(folder+"/Essential.prefab");PrefabUtility.SaveAsPrefabAsset(essential,folder+"/Essential.prefab");
                }
                finally{PrefabUtility.UnloadPrefabContents(essential);}
                terrain=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/Terrain.asset");terrainVertices=terrain.vertices;oldTerrainVertices=(Vector3[])terrainVertices.Clone();UpdateEssential(folder,true);affected++;
            }
            AssetDatabase.SaveAssets();return $"Saved one 160 m circular combat floor across {affected} existing chunks, with seamless floor collision and updated navigation.";
        }
    }
}
