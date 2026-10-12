using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nusantara.World.Editor
{
    public static class WorldReferenceBuilder
    {
        public const string Root = "Assets/NusantaraWorld/Generated";
        public const string ProfilePath = "Assets/NusantaraWorld/ReferenceGeography.asset";
        public const string ScenePath = "Assets/NusantaraWorld/Scenes/NusantaraQuest_World.unity";
        static WorldGeographyProfile profile;
        static WorldDefinition world;
        static AddressableAssetSettings settings;
        static AddressableAssetGroup group;
        static readonly Dictionary<string,GameObject> props = new Dictionary<string,GameObject>();
        static readonly Dictionary<string,Material> materials = new Dictionary<string,Material>();
        static readonly List<WorldChunkRecord> records = new List<WorldChunkRecord>();
        static List<Vector2Int> coordinates;
        static Scene staging;
        static int cursor, propCount, waterChunks, bridgePieces;
        static bool running;
        public static string Status { get; private set; } = "Not generating.";

        [MenuItem("Tools/Nusantara/Generate Reference World")]
        public static void Start()
        {
            if (running || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Open the copied NusantaraQuest_World scene first.");
            if (AssetDatabase.IsValidFolder(Root + "/Chunks")) throw new InvalidOperationException("Generated chunks already exist. Edit the existing chunk prefabs; this command never overwrites authored chunks.");
            world = AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
            profile = AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(ProfilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<WorldGeographyProfile>(); profile.world = world; AssetDatabase.CreateAsset(profile, ProfilePath); }
            WorldReferenceLayout.Apply(profile);
            foreach (var folder in new[] {Root, WorldAssetLayout.Props,Root+"/Chunks",Root+"/Navigation"}) EnsureFolder(folder);
            if(!AssetDatabase.IsValidFolder(WorldAssetLayout.Materials)||!AssetDatabase.IsValidFolder(WorldAssetLayout.Textures))throw new InvalidOperationException("Use the existing project Materials and Textures folders.");
            settings = AddressableAssetSettingsDefaultObject.GetSettings(true); group = settings.FindGroup("Nusantara World Chunks");
            if (group == null) throw new InvalidOperationException("Create the world foundation first.");
            CreateMaterials(); CreatePropPrefabs();
            coordinates = new List<Vector2Int>(); records.Clear(); cursor=0;propCount=0;waterChunks=0;bridgePieces=0;
            // Keep a coastal ring for sea views and shoreline collision, without loading the empty corners of the bounding square.
            for(int z=0;z<world.gridSize.y;z++) for(int x=0;x<world.gridSize.x;x++)
            {
                var c=new Vector2Int(x,z);var o=world.ChunkOrigin(c);var p=new Vector2(o.x+world.chunkSize/2,o.z+world.chunkSize/2);
                if(profile.CoastDistance(p)>-world.chunkSize*.8f)coordinates.Add(c);
            }
            staging=EditorSceneManager.NewPreviewScene();
            EditorUtility.SetDirty(profile);EditorUtility.SetDirty(world);AssetDatabase.SaveAssets();
            running=true;EditorApplication.update+=Advance;
            Status=$"Generating {coordinates.Count} reference chunks. Play mode and tests are not being run.";
            SessionState.SetString("Nusantara.Generation",Status);
        }
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        static Material Material(string name,Color color,string shader=null)
        {
            var mat=new Material(Shader.Find(shader??WorldAssetLayout.LitShader)) {name=name,enableInstancing=true};WorldAssetLayout.SetColor(mat,color);
            if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",.15f);
            if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",.15f);
            AssetDatabase.CreateAsset(mat,WorldAssetLayout.MaterialPath(name));materials[name]=mat;return mat;
        }
        static void CreateMaterials()
        {
            materials.Clear();
            Material("Terrain",new Color(.28f,.38f,.20f));Material("Path",new Color(.48f,.39f,.24f));
            Material("Bridge timber",new Color(.31f,.20f,.10f));Material("Stone",new Color(.28f,.31f,.27f));
            materials["Stone"].SetFloat("_Cull",0);
            Material("River",new Color(.035f,.34f,.36f),"Nusantara/River Water");
            Material("Ocean",new Color(.035f,.16f,.22f),"Nusantara/River Water");
            Material("Waterfall",new Color(.30f,.65f,.67f),"Nusantara/River Water");
            // Shared tiling ground texture, stored once for all 1–6 terrain chunks.
            var texture=new Texture2D(128,128,TextureFormat.RGB24,true) {name="Ground grain",wrapMode=TextureWrapMode.Repeat};
            var colors=new Color[128*128];
            for(int z=0;z<128;z++)for(int x=0;x<128;x++)
            {
                float n=Mathf.PerlinNoise(x*.13f,z*.13f)*.24f+Mathf.PerlinNoise(x*.53f,z*.53f)*.10f;
                colors[z*128+x]=new Color(.64f+n,.68f+n,.55f+n);
            }
            texture.SetPixels(colors);texture.Apply();AssetDatabase.CreateAsset(texture,WorldAssetLayout.UtilityTexturePath("GroundGrain.asset"));WorldAssetLayout.SetTexture(materials["Terrain"],texture);
            var plank=new Texture2D(32,128,TextureFormat.RGB24,true) {name="Bridge planks",wrapMode=TextureWrapMode.Repeat};
            var pixels=new Color[32*128];for(int y=0;y<128;y++)for(int x=0;x<32;x++) { float b=y%12<1?.32f:.74f+Mathf.PerlinNoise(x*.3f,y*.04f)*.2f;pixels[y*32+x]=new Color(b,b*.86f,b*.69f); }
            plank.SetPixels(pixels);plank.Apply();AssetDatabase.CreateAsset(plank,WorldAssetLayout.UtilityTexturePath("BridgePlanks.asset"));WorldAssetLayout.SetTexture(materials["Bridge timber"],plank);
        }
        static Bounds RenderBounds(GameObject root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return new Bounds();
            Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;
        }
        static void CreatePropPrefabs()
        {
            props.Clear();
            string source=WorldAssetLayout.Props+"/";
            var sizes=new Dictionary<string,float> { ["rainforest_tree"]=22,["bamboo"]=9,["root_formation"]=7,["fallen_log"]=8,["tree_stump"]=4,["long_house"]=27,["stilt_house"]=15,["dock"]=14,["boat"]=7,["lily_pad"]=1.25f,["barrel"]=1.25f,["crates"]=2,["stone_arch"]=14,["ruined_walls"]=12,["crocodile_statue"]=7,["giant_crocodile_skulls"]=36,["wooden_bridge"]=16,["wooden_walkway"]=12 };
            foreach(var pair in sizes)
            {
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(source+pair.Key+".fbx");if(model==null)continue;
                string texturePath=WorldAssetLayout.TexturePath(pair.Key+".jpg");
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)??AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath+".jpg");
                if(texture!=null && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) is TextureImporter importer)
                {
                    importer.mipmapEnabled=true;importer.maxTextureSize=1024;
                    foreach(string platform in new[]{"Android","iPhone"})importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {name=platform,overridden=true,maxTextureSize=1024,format=TextureImporterFormat.ASTC_6x6,compressionQuality=50});
                    importer.SaveAndReimport();
                }
                var mat=Material(pair.Key,new Color(.94f,.94f,.94f));WorldAssetLayout.SetTexture(mat,texture);
                var root=new GameObject(pair.Key);var high=(GameObject)PrefabUtility.InstantiatePrefab(model);high.transform.SetParent(root.transform,false);high.name="Source model";
                Bounds bounds=RenderBounds(high);bool tall=pair.Key=="rainforest_tree"||pair.Key=="bamboo"||pair.Key=="root_formation";
                float unit=tall?bounds.size.y:Mathf.Max(bounds.size.x,bounds.size.z);float scale=pair.Value/Mathf.Max(.001f,unit);
                high.transform.localScale=Vector3.one*scale;bounds=RenderBounds(high);high.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                foreach(var r in high.GetComponentsInChildren<Renderer>()) { r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();r.shadowCastingMode=pair.Key=="lily_pad"?ShadowCastingMode.Off:ShadowCastingMode.On;r.receiveShadows=true; }
                var low=Object.Instantiate(high,root.transform);low.name="Simplified distance LOD";
                int meshIndex=0;foreach(var filter in low.GetComponentsInChildren<MeshFilter>())
                {
                    if(filter.sharedMesh==null)continue;var mesh=WorldGenerationGeometry.Simplify(filter.sharedMesh);AssetDatabase.CreateAsset(mesh,WorldAssetLayout.Props+"/"+pair.Key+"_LOD_"+(meshIndex++)+".asset");filter.sharedMesh=mesh;
                }
                foreach(var r in low.GetComponentsInChildren<Renderer>())r.shadowCastingMode=ShadowCastingMode.Off;
                var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.12f,high.GetComponentsInChildren<Renderer>()),new LOD(.018f,low.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
                // Visual wrappers are shared and editable. Required walking collision is authored separately in each essential chunk.
                props[pair.Key]=PrefabUtility.SaveAsPrefabAsset(root,WorldAssetLayout.Props+"/"+pair.Key+".prefab");Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
        }
        static void Advance()
        {
            if(!running)return;
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode) { Fail("Generation paused because Play mode was requested. Finish authoring before Play.");return; }
                if(cursor>=coordinates.Count) { Finish();return; }
                GenerateChunk(coordinates[cursor]);cursor++;
                Status=$"Generated {cursor}/{coordinates.Count} chunks; {propCount:N0} props, {waterChunks} water chunks.";
                SessionState.SetString("Nusantara.Generation",Status);
                if(cursor%64==0) { AssetDatabase.SaveAssets(); WriteProgress(); }
            }
            catch(Exception error) { Fail(error.ToString());Debug.LogException(error); }
        }
        static void Fail(string message)
        {
            running=false;EditorApplication.update-=Advance;Status="Generation stopped: "+message;SessionState.SetString("Nusantara.Generation",Status);WriteProgress();
            if(staging.IsValid())EditorSceneManager.ClosePreviewScene(staging);AssetDatabase.SaveAssets();
        }
        static void WriteProgress()
        {
            Directory.CreateDirectory("Library/Nusantara");File.WriteAllText("Library/Nusantara/Generation.txt",Status);
        }
        static GameObject New(string name,Transform parent=null)
        {
            var go=new GameObject(name);if(parent!=null)go.transform.SetParent(parent,false);else SceneManager.MoveGameObjectToScene(go,staging);return go;
        }
        static GameObject MeshObject(string name,Mesh mesh,Material mat,Transform parent,bool collision,string path)
        {
            AssetDatabase.CreateAsset(mesh,path);var go=New(name,parent);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            if(mat!=null)go.AddComponent<MeshRenderer>().sharedMaterial=mat;
            if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;
        }
        static void AddSource(List<NavMeshBuildSource> sources,Mesh mesh,int area=0)
        {
            sources.Add(new NavMeshBuildSource {shape=NavMeshBuildSourceShape.Mesh,sourceObject=mesh,transform=Matrix4x4.identity,area=area});
        }
        static int ChunkRegion(Vector2Int c)
        {
            var o=world.ChunkOrigin(c);
            for(int z=0;z<=2;z++)for(int x=0;x<=2;x++)if(profile.Region(new Vector2(o.x+x*world.chunkSize/2,o.z+z*world.chunkSize/2))==5)return 5;
            return profile.Region(new Vector2(o.x+world.chunkSize/2,o.z+world.chunkSize/2));
        }
        static void GenerateChunk(Vector2Int c)
        {
            string id=$"{c.x:D2}_{c.y:D2}";string folder=Root+"/Chunks/"+id;EnsureFolder(folder);
            int region=ChunkRegion(c);Vector3 origin=world.ChunkOrigin(c);float size=world.chunkSize;
            var essential=New("Terrain and collision "+id);var sources=new List<NavMeshBuildSource>();
            var ground=WorldTerrainMesher.Build(world,c,profile.terrainIntervals,profile.Height);
            MeshObject("Terrain",ground,materials["Terrain"],essential.transform,true,folder+"/Terrain.asset");AddSource(sources,ground);
            var road=new WorldGenerationGeometry();var timber=new WorldGenerationGeometry();var rails=new WorldGenerationGeometry();
            BuildRoutes(origin,road,timber,rails);
            if(!road.Empty) { var mesh=road.Mesh("Walking paths");MeshObject("Connected paths",mesh,materials["Path"],essential.transform,true,folder+"/Paths.asset");AddSource(sources,mesh); }
            if(!timber.Empty) { var mesh=timber.Mesh("Smooth bridge decks");MeshObject("Smooth bridges and boardwalks",mesh,materials["Bridge timber"],essential.transform,true,folder+"/Bridges.asset");AddSource(sources,mesh); }
            if(!rails.Empty) { var mesh=rails.Mesh("Bridge safety rails");MeshObject("Bridge rail collision",mesh,materials["Bridge timber"],essential.transform,true,folder+"/Rails.asset");AddSource(sources,mesh,1); }
            var river=new WorldGenerationGeometry();var sea=new WorldGenerationGeometry();var barriers=new WorldGenerationGeometry();
            BuildWater(origin,river,sea,barriers);
            if(!river.Empty) { MeshObject("River water - no swimming",river.Mesh("River surface"),materials["River"],essential.transform,false,folder+"/River.asset");waterChunks++; }
            if(!sea.Empty)MeshObject("Coastal water - no swimming",sea.Mesh("Sea surface"),materials["Ocean"],essential.transform,false,folder+"/Sea.asset");
            if(!barriers.Empty) { var mesh=barriers.Mesh("No swimming volume");MeshObject("Water entry blockers",mesh,null,essential.transform,true,folder+"/WaterBarrier.asset");AddSource(sources,mesh,1); }
            GameObject decoration=null,gameplay=null;
            if(region==5)
            {
                decoration=New("Region 5 editable props "+id);gameplay=New("Region 5 encounter areas "+id);
                PlaceEnvironment(c,origin,decoration.transform,essential.transform,sources);
                AddLandmarks(c,origin,decoration.transform,essential.transform,gameplay.transform,sources,folder);
            }
            // Bake only this chunk's collision. Neighbor connections are owned by WorldChunkNavigation.
            var navSettings=NavMesh.GetSettingsByIndex(0);navSettings.agentSlope=world.maxWalkableSlope;navSettings.agentClimb=world.maxStepHeight;navSettings.agentRadius=world.agentRadius;navSettings.agentHeight=world.agentHeight;
            navSettings.overrideVoxelSize=true;navSettings.voxelSize=.4f;navSettings.overrideTileSize=true;navSettings.tileSize=256;
            var nav=NavMeshBuilder.BuildNavMeshData(navSettings,sources,new Bounds(new Vector3(size/2,150,size/2),new Vector3(size,400,size)),Vector3.zero,Quaternion.identity);
            if(nav!=null)
            {
                AssetDatabase.CreateAsset(nav,Root+"/Navigation/"+id+".asset");var navigation=essential.AddComponent<WorldChunkNavigation>();navigation.bakedData=new[]{nav};navigation.chunkSize=size;navigation.connectEdges=true;navigation.agentTypeId=navSettings.agentTypeID;
                var starts=new List<Vector3>();var ends=new List<Vector3>();
                for(float d=16;d<size;d+=16)for(int edge=0;edge<2;edge++)
                {
                    Vector2 p=new Vector2(origin.x+(edge==0?size:d),origin.z+(edge==0?d:size));if(profile.IsForbiddenWater(p))continue;
                    float h=profile.RouteDistance(p)<0?profile.BaseHeight(p):profile.Height(p.x,p.y);
                    Vector3 local=new Vector3(p.x-origin.x,h+.2f,p.y-origin.z);Vector3 offset=edge==0?Vector3.right*2:Vector3.forward*2;starts.Add(local-offset);ends.Add(local+offset);
                }
                navigation.edgeStarts=starts.ToArray();navigation.edgeEnds=ends.ToArray();
            }
            var distant=New("Distant terrain "+id);var far=WorldTerrainMesher.Build(world,c,profile.distantIntervals,profile.Height);WorldGenerationGeometry.AddSkirt(far,profile.distantIntervals,12);
            MeshObject("Terrain LOD",far,materials["Terrain"],distant.transform,false,folder+"/DistantTerrain.asset");
            if(!river.Empty) { var water=New("Distant river",distant.transform);water.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/River.asset");water.AddComponent<MeshRenderer>().sharedMaterial=materials["River"]; }
            if(!sea.Empty) { var water=New("Distant sea",distant.transform);water.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/Sea.asset");water.AddComponent<MeshRenderer>().sharedMaterial=materials["Ocean"]; }
            var record=new WorldChunkRecord {coordinate=c,region=region,essentialAddress=SaveAddressable(essential,folder+"/Essential.prefab","world/"+id+"/essential"),distantAddress=SaveAddressable(distant,folder+"/Distant.prefab","world/"+id+"/distant")};
            if(decoration!=null && decoration.transform.childCount>0)record.decorationAddresses=new[]{SaveAddressable(decoration,folder+"/Props.prefab","world/"+id+"/props")};
            if(gameplay!=null && gameplay.transform.childCount>0)record.gameplayAddresses=new[]{SaveAddressable(gameplay,folder+"/Gameplay.prefab","world/"+id+"/gameplay")};
            records.Add(record);Object.DestroyImmediate(essential);Object.DestroyImmediate(distant);if(decoration!=null)Object.DestroyImmediate(decoration);if(gameplay!=null)Object.DestroyImmediate(gameplay);
        }
        static string SaveAddressable(GameObject go,string path,string address)
        {
            PrefabUtility.SaveAsPrefabAsset(go,path);var entry=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path),group,false,false);entry.address=address;return address;
        }
        static bool Clip(Vector2 a,Vector2 b,Vector2 min,Vector2 max,out float t0,out float t1)
        {
            t0=0;t1=1;Vector2 d=b-a;
            for(int axis=0;axis<2;axis++)
            {
                if(Mathf.Abs(d[axis])<.0001f) { if(a[axis]<min[axis]||a[axis]>max[axis])return false;continue; }
                float near=(min[axis]-a[axis])/d[axis],far=(max[axis]-a[axis])/d[axis];if(near>far)(near,far)=(far,near);
                t0=Mathf.Max(t0,near);t1=Mathf.Min(t1,far);if(t0>t1)return false;
            }
            return true;
        }
        static void BuildRoutes(Vector3 origin,WorldGenerationGeometry road,WorldGenerationGeometry timber,WorldGenerationGeometry rails)
        {
            Vector2 min=new Vector2(origin.x,origin.z);Vector2 max=min+Vector2.one*world.chunkSize;
            foreach(var route in profile.routes)for(int i=1;i<route.points.Length;i++)
            {
                Vector2 a=route.points[i-1],b=route.points[i];
                if(!Clip(a,b,min-Vector2.one*route.halfWidth,max+Vector2.one*route.halfWidth,out float t0,out float t1))continue;
                Vector2 start=Vector2.Lerp(a,b,t0),end=Vector2.Lerp(a,b,t1);Vector2 dir=(b-a).normalized;Vector2 normal=new Vector2(-dir.y,dir.x)*route.halfWidth;
                int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(start,end)/8));
                for(int j=0;j<steps;j++)
                {
                    var p=Vector2.Lerp(start,end,j/(float)steps);var q=Vector2.Lerp(start,end,(j+1)/(float)steps);var mid=(p+q)/2;
                    Vector3 V(Vector2 v)=>new Vector3(v.x-origin.x,profile.BaseHeight(v)+.075f,v.y-origin.z);
                    bool bridge=profile.WaterDistance(mid,out _)<65;
                    (bridge?timber:road).Quad(V(p+normal),V(q+normal),V(q-normal),V(p-normal));
                    if(!bridge)continue;
                    bridgePieces++;
                    // Low solid rails keep the capsule on the dry bridge instead of stepping off into the channel.
                    foreach(float side in new[]{-1f,1f})
                    {
                        Vector3 center=V(mid+normal*(side*.94f))+Vector3.up*.65f;
                        float yaw=Mathf.Atan2(dir.x,dir.y)*Mathf.Rad2Deg;
                        rails.Box(center,new Vector3(.35f,1.3f,Vector2.Distance(p,q)+.15f),Quaternion.Euler(0,yaw,0));
                    }
                }
            }
        }
        static void BuildWater(Vector3 origin,WorldGenerationGeometry river,WorldGenerationGeometry sea,WorldGenerationGeometry barriers)
        {
            int count=Mathf.CeilToInt(world.chunkSize/8);float step=world.chunkSize/count;
            for(int z=0;z<count;z++)
            {
                int run=-1;float runTop=0;
                for(int x=0;x<=count;x++)
                {
                    bool blocked=false;float top=0;
                    if(x<count)
                    {
                        Vector2 p=new Vector2(origin.x+(x+.5f)*step,origin.z+(z+.5f)*step);
                        float cd=profile.CoastDistance(p);float wd=profile.WaterDistance(p,out float surface);float route=profile.RouteDistance(p);
                        float x0=x*step,z0=z*step;
                        if(wd<32 && cd>0)river.Quad(new Vector3(x0,surface,z0),new Vector3(x0,surface,z0+step),new Vector3(x0+step,surface,z0+step),new Vector3(x0+step,surface,z0));
                        if(cd<30)sea.Quad(new Vector3(x0,0,z0),new Vector3(x0,0,z0+step),new Vector3(x0+step,0,z0+step),new Vector3(x0+step,0,z0));
                        blocked=cd<78 || (wd<40 && route>5);top=cd<78?60:surface+24;
                    }
                    if(blocked && run<0) {run=x;runTop=top;}
                    if(run>=0 && (!blocked || Mathf.Abs(top-runTop)>1 || x==count))
                    {
                        barriers.Box(new Vector3((run+x)*step/2,(runTop-20)/2,(z+.5f)*step),new Vector3((x-run)*step,runTop+20,step+.1f),Quaternion.identity);run=blocked?x:-1;runTop=top;
                    }
                }
            }
        }
        static float Random(System.Random random,float min,float max)=>(float)(min+random.NextDouble()*(max-min));
        static bool DryPlacement(Vector2 p,float clearance=8)
        {
            if(!profile.InsideRegion(p,5,30)||profile.CoastDistance(p)<110||profile.WaterDistance(p,out _)<65||profile.RouteDistance(p)<clearance)return false;
            foreach(var location in world.locations)if(Vector2.Distance(p,new Vector2(location.position.x,location.position.z))<location.clearRadius+12)return false;
            return true;
        }
        static GameObject Place(string name,Vector2 p,float y,float yaw,float scale,Vector3 origin,Transform parent)
        {
            if(!props.TryGetValue(name,out var prefab))return null;
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);instance.name=name+" "+parent.childCount.ToString("D3");
            instance.transform.localPosition=new Vector3(p.x-origin.x,y,p.y-origin.z);instance.transform.localRotation=Quaternion.Euler(0,yaw,0);instance.transform.localScale=Vector3.one*scale;propCount++;return instance;
        }
        static void Collision(string name,Vector2 p,float y,float radius,float height,Vector3 origin,Transform parent,List<NavMeshBuildSource> sources)
        {
            var go=New(name+" collision",parent);go.transform.localPosition=new Vector3(p.x-origin.x,y+height/2,p.y-origin.z);
            var box=go.AddComponent<BoxCollider>();box.size=new Vector3(radius*2,height,radius*2);
            sources.Add(new NavMeshBuildSource {shape=NavMeshBuildSourceShape.Box,size=box.size,transform=Matrix4x4.TRS(go.transform.localPosition,Quaternion.identity,Vector3.one),area=1});
        }
        static void PlaceEnvironment(Vector2Int c,Vector3 origin,Transform decoration,Transform essential,List<NavMeshBuildSource> sources)
        {
            var random=new System.Random(unchecked(world.seed+c.x*73856093+c.y*19349663));
            for(int i=0;i<profile.treesPerChunk;i++)
            {
                Vector2 p=new Vector2(origin.x+Random(random,8,world.chunkSize-8),origin.z+Random(random,8,world.chunkSize-8));
                if(!DryPlacement(p,14))continue;
                string name=i%9==0?"bamboo":"rainforest_tree";float scale=Random(random,.7f,1.5f);float y=profile.Height(p.x,p.y);
                Place(name,p,y,Random(random,0,360),scale,origin,decoration);Collision(name,p,y,.65f*scale,3,origin,essential,sources);
                if(i%5==0)
                {
                    Vector2 near=p+new Vector2(5,3);string extra=i%10==0?"root_formation":"fallen_log";
                    if(DryPlacement(near,12))Place(extra,near,profile.Height(near.x,near.y),Random(random,0,360),Random(random,.5f,.9f),origin,decoration);
                }
                if(i%7==0) { var near=p+new Vector2(-4,4);if(DryPlacement(near,12))Place("tree_stump",near,profile.Height(near.x,near.y),Random(random,0,360),.7f,origin,decoration); }
            }
            for(int i=0;i<48;i++)
            {
                Vector2 p=new Vector2(origin.x+Random(random,4,world.chunkSize-4),origin.z+Random(random,4,world.chunkSize-4));
                if(!profile.InsideRegion(p,5,12)||profile.WaterDistance(p,out float surface)>-4||profile.RouteDistance(p)<16)continue;
                string name=i==0||i==24?"boat":"lily_pad";Place(name,p,surface+(name=="boat"?-.45f:.015f),Random(random,0,360),name=="boat"?1:Random(random,.8f,1.6f),origin,decoration);
            }
        }
        static bool Owns(Vector2 p,Vector2Int c)=>world.WorldToChunk(new Vector3(p.x,0,p.y))==c;
        static void AddLandmarks(Vector2Int c,Vector3 origin,Transform decoration,Transform essential,Transform gameplay,List<NavMeshBuildSource> sources,string folder)
        {
            for(int index=0;index<world.locations.Length;index++)
            {
                var location=world.locations[index];Vector2 center=new Vector2(location.position.x,location.position.z);
                if(Owns(center,c))
                {
                    var marker=New(location.name,gameplay);marker.transform.localPosition=location.position-origin;
                    var area=marker.AddComponent<WorldEncounterArea>();area.encounterId=location.id;area.radius=location.clearRadius;area.element=WorldEncounterArea.Element.Neutral;
                    if(index==8) foreach(var element in new[]{WorldEncounterArea.Element.Water,WorldEncounterArea.Element.Nature})
                    {
                        var zone=New(element+" mechanic zone",marker.transform);zone.transform.localPosition=new Vector3(element==WorldEncounterArea.Element.Water?-35:35,0,0);
                        var config=zone.AddComponent<WorldEncounterArea>();config.encounterId=location.id+"-"+element;config.element=element;config.radius=24;
                    }
                }
                void LandmarkProp(string asset,Vector2 offset,float yaw=0,float scale=1,bool solid=false)
                {
                    Vector2 p=center+offset;if(!Owns(p,c)||!profile.InsideRegion(p,5,25)||profile.IsForbiddenWater(p))return;
                    float y=profile.Height(p.x,p.y);Place(asset,p,y,yaw,scale,origin,decoration);
                    if(solid)Collision(asset,p,y,asset=="long_house"?11:asset=="stilt_house"?6:2.5f,asset.Contains("house")?12:6,origin,essential,sources);
                }
                if(index==0)
                {
                    for(int row=-2;row<=2;row++)for(int side=-1;side<=1;side+=2)
                    {
                        Vector2 offset=new Vector2(side*65,row*48);if(profile.RouteDistance(center+offset)<22)continue;
                        LandmarkProp(row%2==0?"long_house":"stilt_house",offset,side>0?270:90,1,true);
                        LandmarkProp("barrel",offset+new Vector2(side*15,9),25);LandmarkProp("crates",offset+new Vector2(side*15,13),0,.9f);
                    }
                    // Small gathering court and central storage, with the main path kept clear.
                    LandmarkProp("crocodile_statue",new Vector2(0,95),180,.6f,true);
                }
                if(index==5)
                {
                    for(int i=0;i<10;i++) {float a=i*Mathf.PI*2/10;Vector2 offset=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*80;if(profile.RouteDistance(center+offset)>16)LandmarkProp(i%3==0?"stone_arch":"ruined_walls",offset,-a*Mathf.Rad2Deg,1,true);}
                    LandmarkProp("crocodile_statue",new Vector2(35,25),210,1.2f,true);
                }
                if(index==7)
                {
                    // Giant boss landmark is reserved exclusively for Sarang Bujang Senang.
                    LandmarkProp("crocodile_statue",new Vector2(40,30),30,.5f,true);
                    LandmarkProp("fallen_log",new Vector2(-40,-25),110,1.5f);
                    LandmarkProp("root_formation",new Vector2(35,-45),70,1.4f);
                }
                if(index==8)
                {
                    LandmarkProp("giant_crocodile_skulls",new Vector2(0,-120),0,1.5f,true);
                    foreach(float side in new[]{-1f,1f}) {LandmarkProp("crocodile_statue",new Vector2(side*105,0),side*90,1.8f,true);LandmarkProp("root_formation",new Vector2(side*80,-75),side*30,2);}
                    for(int i=0;i<6;i++){float a=i*Mathf.PI/3;LandmarkProp("ruined_walls",new Vector2(Mathf.Cos(a),Mathf.Sin(a))*140,-a*Mathf.Rad2Deg,.9f,true);}
                }
                if(index==6 && Owns(center,c))AddCave(center,origin,essential,sources,folder);
                if(index==4 && Owns(center,c))AddWaterfall(center,origin,essential,folder);
            }
            // Docks and tied boats at riverbanks, using only supplied assets.
            for(int i=0;i<profile.waterways.Length;i++)
            {
                var water=profile.waterways[i];if(!water.name.Contains("Bujang")&&!water.name.Contains("Delta"))continue;
                for(int k=1;k<water.points.Length;k++)
                {
                    Vector2 mid=(water.points[k-1]+water.points[k])/2;Vector2 direction=(water.points[k]-water.points[k-1]).normalized;Vector2 side=new Vector2(-direction.y,direction.x);
                    Vector2 boat=mid+side*(water.halfWidth-8);if(Owns(boat,c)&&profile.InsideRegion(boat,5,15)&&profile.RouteDistance(boat)>25)Place("boat",boat,water.surface-.45f,Mathf.Atan2(direction.x,direction.y)*Mathf.Rad2Deg+90,1,origin,decoration);
                    Vector2 dock=mid+side*(water.halfWidth+25);if(Owns(dock,c)&&profile.InsideRegion(dock,5,20)&&profile.RouteDistance(dock)>20)Place("dock",dock,water.surface-3,Mathf.Atan2(side.x,side.y)*Mathf.Rad2Deg,1,origin,decoration);
                }
            }
        }
        static void AddCave(Vector2 center,Vector3 origin,Transform essential,List<NavMeshBuildSource> sources,string folder)
        {
            var geometry=new WorldGenerationGeometry();float floor=profile.BaseHeight(center);
            // A hollow rock vault with two walk-in ends. The center stays at path height.
            Vector3 Point(float angle,float radius,float z)=>new Vector3(center.x-origin.x+Mathf.Cos(angle)*radius,floor+Mathf.Sin(angle)*radius,center.y-origin.z+z);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;
                geometry.Quad(Point(a,15,-52),Point(b,15,-52),Point(b,15,52),Point(a,15,52));
                geometry.Quad(Point(b,24,-52),Point(a,24,-52),Point(a,24,52),Point(b,24,52));
                geometry.Quad(Point(a,24,-52),Point(b,24,-52),Point(b,15,-52),Point(a,15,-52));
                geometry.Quad(Point(b,24,52),Point(a,24,52),Point(a,15,52),Point(b,15,52));
            }
            var mesh=geometry.Mesh("Gua Sunyi hollow rock vault");MeshObject("Gua Sunyi - walk-in cave",mesh,materials["Stone"],essential,true,folder+"/Cave.asset");AddSource(sources,mesh,1);
        }
        static void AddWaterfall(Vector2 center,Vector3 origin,Transform essential,string folder)
        {
            var cliff=new WorldGenerationGeometry();var fall=new WorldGenerationGeometry();float y=35;
            for(int i=-1;i<=1;i++)
            {
                Vector3 p=new Vector3(center.x-origin.x+i*40,y,center.y-origin.z+105);
                cliff.Box(p+new Vector3(0,20,8),new Vector3(42,70,35),Quaternion.identity);
                fall.Quad(p+new Vector3(-15,0,-11),p+new Vector3(-15,54,-11),p+new Vector3(15,54,-11),p+new Vector3(15,0,-11));
            }
            MeshObject("Sacred waterfall cliff",cliff.Mesh("Waterfall cliff"),materials["Stone"],essential,true,folder+"/WaterfallCliff.asset");
            MeshObject("Air Terjun Suci falls",fall.Mesh("Waterfall curtains"),materials["Waterfall"],essential,false,folder+"/Waterfall.asset");
        }
        static void Finish()
        {
            running=false;EditorApplication.update-=Advance;
            world.chunks=records.ToArray();world.layoutApproved=true;EditorUtility.SetDirty(world);EditorUtility.SetDirty(profile);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification,group,true,true);AssetDatabase.SaveAssets();
            if(staging.IsValid())EditorSceneManager.ClosePreviewScene(staging);
            WorldEnvironmentFinishing.Apply();
            IntegrateScene();
            Status=$"Complete: {records.Count} chunks, {propCount:N0} editable Region 5 props, {waterChunks} river chunks. World scene copied from main_scene_test. Tests and Play mode were not run.";
            SessionState.SetString("Nusantara.Generation",Status);WriteProgress();Debug.Log(Status);
        }
        public static void IntegrateScene()
        {
            var scene=SceneManager.GetActiveScene();if(scene.path!=ScenePath)throw new InvalidOperationException("The copied world scene must be active.");
            world=AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);profile=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(ProfilePath);
            var player=scene.GetRootGameObjects().First(g=>g.name=="Player");var oldPosition=player.transform.position;
            var controller=player.GetComponent<CharacterController>();float foot=controller.center.y-controller.height*.5f;
            Vector3 target=world.playerStart-Vector3.up*foot;Vector3 delta=target-oldPosition;
            var preserved=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Original test content (preserved)")??new GameObject("Original test content (preserved)");
            foreach(string name in new[]{"Plane","enemy_1_onehand","Enemy Navigation"})
            {
                var obj=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==name);if(obj!=null)obj.transform.SetParent(preserved.transform,true);
            }
            preserved.SetActive(false);
            player.transform.position=target;
            foreach(string name in new[]{"Camera","Isometric Camera","Rideable Horse"})
            {
                var obj=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==name);if(obj!=null)obj.transform.position+=delta;
            }
            var root=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="World")??new GameObject("World");root.SetActive(false);
            var streamer=root.GetComponent<WorldChunkStreamer>()??root.AddComponent<WorldChunkStreamer>();streamer.definition=world;streamer.player=player.transform;
            var gate=root.GetComponent<WorldStreamingPlayerGate>()??root.AddComponent<WorldStreamingPlayerGate>();gate.streamer=streamer;gate.controller=controller;gate.movementScripts=new Behaviour[]{player.GetComponent<PlayerStateManager>()};
            var water=root.GetComponent<WorldWaterSafety>()??root.AddComponent<WorldWaterSafety>();water.geography=profile;water.player=player.transform;water.controller=controller;
            var horse=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Rideable Horse");
            if(horse!=null)
            {
                var horseController=horse.GetComponent<CharacterController>();var horseGate=horse.GetComponent<WorldStreamingPlayerGate>()??horse.AddComponent<WorldStreamingPlayerGate>();horseGate.streamer=streamer;horseGate.controller=horseController;
                horseGate.movementScripts=horse.GetComponents<Behaviour>().Where(b=>b.GetType().Name=="HorseController").ToArray();
                var horseWater=horse.GetComponent<WorldWaterSafety>()??horse.AddComponent<WorldWaterSafety>();horseWater.geography=profile;horseWater.player=horse.transform;horseWater.controller=horseController;
            }
            root.SetActive(true);
            WorldSceneBookmarks.LoadPreview(target,false);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
