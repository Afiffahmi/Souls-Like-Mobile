using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nusantara.World.Editor
{
    public static class Region5EnhancementValidation
    {
        sealed class RouteSample
        {public string route;public int index;public Vector2 position;public Vector3 floor;public bool safe;}
        static WorldGeographyProfile geography;
        static WorldDefinition world;
        static WorldChunkRecord[] chunks;
        static readonly Dictionary<Vector2Int,List<RouteSample>> samples=new Dictionary<Vector2Int,List<RouteSample>>();
        static readonly List<RouteSample> allSamples=new List<RouteSample>();
        static readonly Dictionary<string,int> ids=new Dictionary<string,int>();
        static readonly List<string> issues=new List<string>();
        static Scene scene;
        static int cursor,giants,details,canopies,routeFailures,seamFailures;
        static bool running;
        public static string Status {get;private set;}="Ready.";
        [MenuItem("Tools/Nusantara/Validate Region 5 Enhancement")]
        public static void Start()
        {
            if(running||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Validation requires idle Edit mode.");
            geography=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);world=geography.world;
            chunks=world.chunks.Where(c=>c.region==world.WetlandRegionId).ToArray();samples.Clear();allSamples.Clear();ids.Clear();issues.Clear();cursor=giants=details=canopies=routeFailures=seamFailures=0;
            foreach(var route in geography.routes)
            {
                int index=0;
                for(int k=0;k<route.points.Length-1;k++)
                {
                    Vector2 a=route.points[k],b=route.points[k+1];int count=Mathf.CeilToInt(Vector2.Distance(a,b)/2);
                    for(int j=0;j<=count;j++)
                    {
                        Vector2 p=Vector2.Lerp(a,b,j/(float)count);var c=world.WorldToChunk(new Vector3(p.x,0,p.y));
                        if(!geography.InsideRegion(p,geography.world.WetlandRegionId)||!chunks.Any(record=>record.coordinate==c)){index++;continue;}
                        var sample=new RouteSample{route=route.name,index=index++,position=p};allSamples.Add(sample);
                        if(!samples.TryGetValue(c,out var list)){list=new List<RouteSample>();samples[c]=list;}list.Add(sample);
                    }
                }
            }
            scene=EditorSceneManager.NewPreviewScene();running=true;EditorApplication.update+=Advance;
            Status=$"Validating {chunks.Length} chunks and {allSamples.Count} route samples.";Write();
        }
        static void Issue(string text){if(issues.Count<100)issues.Add(text);}
        static void Write(){Directory.CreateDirectory("Library/Nusantara");File.WriteAllText("Library/Nusantara/Region5Validation.txt",Status+"\n"+string.Join("\n",issues));}
        static void Advance()
        {
            try
            {
                if(cursor>=chunks.Length){Finish();return;}
                ValidateChunk(chunks[cursor++]);
                Status=$"Validated {cursor}/{chunks.Length} Region 5 chunks; route failures {routeFailures}; seam failures {seamFailures}.";
                if(cursor%32==0)Write();
            }
            catch(Exception e){Status="Validation stopped: "+e;Write();Stop();Debug.LogException(e);}
        }
        static void Stop(){running=false;EditorApplication.update-=Advance;if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);}
        static string Folder(Vector2Int c)=>$"{WorldReferenceBuilder.Root}/Chunks/{c.x:D2}_{c.y:D2}";
        static void ValidateChunk(WorldChunkRecord chunk)
        {
            string folder=Folder(chunk.coordinate);Vector3 origin=world.ChunkOrigin(chunk.coordinate);
            foreach(string file in new[]{"Props.prefab","Enhancement.prefab","Kampung.prefab","PaintedVegetation.prefab"})
            {
                string path=folder+"/"+file;if(!File.Exists(path))continue;
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var identity in root.GetComponentsInChildren<WorldLandmarkIdentity>(true))
                    {ids.TryGetValue(identity.stableId,out int n);ids[identity.stableId]=n+1;}
                    foreach(var child in root.transform.Cast<Transform>())
                    {
                        if(child.name.Contains("sole giant crocodile")||child.name.StartsWith("giant_crocodile_skulls"))giants++;
                        var p=new Vector2(origin.x+child.localPosition.x,origin.z+child.localPosition.z);
                        if(child.GetComponent<WorldCanopyVfx>()!=null){canopies++;continue;}
                        if(child.childCount>0&&!geography.InsideRegion(p,geography.world.WetlandRegionId)&&child.GetComponent<WorldLandmarkIdentity>()==null)Issue("Outside region prop: "+path+" / "+child.name);
                        if(file=="Enhancement.prefab")
                        {
                            details++;
                            if(child.GetComponentsInChildren<Collider>(true).Length>0)Issue("Unexpected decoration collision: "+child.name);
                        }
                    }
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/Terrain.asset");
            if(mesh==null){Issue("Missing terrain: "+folder);return;}
            var vertices=mesh.vertices;int side=Mathf.RoundToInt(Mathf.Sqrt(vertices.Length));
            foreach(var direction in new[]{Vector2Int.right,Vector2Int.up})
            {
                var neighbor=AssetDatabase.LoadAssetAtPath<Mesh>(Folder(chunk.coordinate+direction)+"/Terrain.asset");if(neighbor==null)continue;
                var other=neighbor.vertices;if(other.Length!=vertices.Length){Issue("Terrain lattice mismatch: "+folder);continue;}
                for(int i=0;i<side;i++)
                {
                    float a=direction==Vector2Int.right?vertices[i*side+side-1].y:vertices[(side-1)*side+i].y;
                    float b=direction==Vector2Int.right?other[i*side].y:other[i].y;
                    if(Mathf.Abs(a-b)>world.seamTolerance){seamFailures++;Issue("Terrain seam: "+chunk.coordinate+" -> "+(chunk.coordinate+direction));}
                }
            }
            if(!samples.TryGetValue(chunk.coordinate,out var points))return;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/Essential.prefab");
            var essential=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);essential.transform.position=origin;
            try
            {
                foreach(var collider in essential.GetComponentsInChildren<Collider>())
                    collider.gameObject.layer=VegetationPaintAssets.IsFloor(collider)?26:27;
                Physics.SyncTransforms();var physics=scene.GetPhysicsScene();var overlaps=new Collider[16];
                foreach(var sample in points)
                {
                    var top=new Vector3(sample.position.x,500,sample.position.y);
                    bool hit=physics.Raycast(top,Vector3.down,out var floor,600,1<<26,QueryTriggerInteraction.Ignore);
                    sample.floor=hit?floor.point:top;
                    sample.safe=hit&&Vector3.Angle(floor.normal,Vector3.up)<=world.maxWalkableSlope+.1f;
                    if(sample.safe)
                    {
                        var low=floor.point+Vector3.up*(world.agentRadius+.05f);var high=floor.point+Vector3.up*(world.agentHeight-world.agentRadius+.05f);
                        sample.safe=physics.OverlapCapsule(low,high,world.agentRadius*.9f,overlaps,1<<27,QueryTriggerInteraction.Ignore)==0;
                    }
                    if(!sample.safe){routeFailures++;Issue("Route floor/slope/clearance: "+sample.route+" at "+sample.position);}
                }
            }
            finally{Object.DestroyImmediate(essential);}
        }
        static void Finish()
        {
            foreach(var id in ids.Where(p=>p.Value!=1))Issue("Duplicate stable ID: "+id.Key+" x"+id.Value);
            if(giants!=1||!ids.TryGetValue("region5-main-crocodile",out int main)||main!=1)Issue("Expected exactly one giant crocodile landmark; found "+giants);
            foreach(var location in world.locations)
                if(!ids.ContainsKey(location.id+"-environment"))Issue("Missing unique landmark environment: "+location.name);
            if(!ids.ContainsKey("region5-ancient-tree-grove"))Issue("Missing unique ancient tree grove.");
            foreach(var island in geography.scenicIslands)
            {
                if(!geography.InsideRegion(island.center,geography.world.WetlandRegionId,island.radius+30))Issue("Island crosses regional boundary: "+island.id);
                if(!geography.IsForbiddenWater(island.center)||geography.RouteDistance(island.center)<island.radius+32)Issue("Island access exclusion failed: "+island.id);
                if(!ids.ContainsKey(island.id))Issue("Missing island: "+island.id);
            }
            foreach(var group in allSamples.GroupBy(s=>s.route))
            {
                RouteSample previous=null;
                foreach(var sample in group.OrderBy(s=>s.index))
                {
                    if(previous!=null&&previous.safe&&sample.safe&&sample.index==previous.index+1)
                    {
                        float distance=Vector2.Distance(previous.position,sample.position);
                        if(Mathf.Abs(sample.floor.y-previous.floor.y)>Mathf.Tan(world.maxWalkableSlope*Mathf.Deg2Rad)*distance+world.maxStepHeight)
                        {routeFailures++;Issue("Abrupt route height change: "+sample.route+" at "+sample.position);}
                    }
                    previous=sample;
                }
            }
            foreach(string error in world.ConfigurationErrors(true))Issue(error);
            Heatmap();
            Status=$"Region 5 validation completed: {chunks.Length} chunks; {allSamples.Count} route samples; {giants} giant crocodile; {ids.Count} unique IDs; {geography.scenicIslands.Length} scenic islands; {canopies} canopy VFX batches; {details} enhancement objects; {routeFailures} route failures; {seamFailures} seam failures; {issues.Count} recorded issues.\n2 m route sampling checks floor, slope, capsule clearance and adjacent heights. It does not replace controller/device playtesting.";
            Write();Stop();Debug.Log(Status);
        }
        static void Heatmap()
        {
            var boundary=world.regions.First(r=>r.id==world.WetlandRegionId).boundary;float minX=boundary.Min(p=>p.x),maxX=boundary.Max(p=>p.x),minZ=boundary.Min(p=>p.y),maxZ=boundary.Max(p=>p.y);
            const int side=160;var texture=new Texture2D(side,side,TextureFormat.RGB24,false);var pixels=new Color[side*side];
            for(int z=0;z<side;z++)for(int x=0;x<side;x++)
            {
                Vector2 p=new Vector2(Mathf.Lerp(minX,maxX,x/(float)(side-1)),Mathf.Lerp(minZ,maxZ,z/(float)(side-1)));Color color=new Color(.07f,.1f,.12f);
                if(geography.InsideRegion(p,geography.world.WetlandRegionId))
                {
                    float dx=geography.Height(p.x+1,p.y)-geography.Height(p.x-1,p.y),dz=geography.Height(p.x,p.y+1)-geography.Height(p.x,p.y-1);
                    float slope=Mathf.Atan(new Vector2(dx,dz).magnitude/2)*Mathf.Rad2Deg;
                    color=geography.IsForbiddenWater(p)||slope>15?new Color(.6f,.12f,.1f):slope>10?new Color(.8f,.63f,.12f):new Color(.1f,.4f,.2f);
                }
                pixels[z*side+x]=color;
            }
            foreach(var sample in allSamples)
            {
                int x=Mathf.Clamp(Mathf.RoundToInt((sample.position.x-minX)/(maxX-minX)*(side-1)),0,side-1);
                int z=Mathf.Clamp(Mathf.RoundToInt((sample.position.y-minZ)/(maxZ-minZ)*(side-1)),0,side-1);
                pixels[z*side+x]=sample.safe?Color.green:Color.red;
            }
            texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes("Library/Nusantara/Region5Walkability.png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
        }
    }
}
