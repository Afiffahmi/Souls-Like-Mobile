using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BowVolleyTargetingValidation
{
    [MenuItem("Tools/Combat/Validate Charged Bow Volley Targeting")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
        var random=UnityEngine.Random.state;
        var scene=EditorSceneManager.NewPreviewScene();
        int checks=0;
        void Check(bool value,string message) { if(!value) throw new InvalidOperationException(message); checks++; }
        GameObject Create(string name) { var go=new GameObject(name); SceneManager.MoveGameObjectToScene(go,scene); return go; }
        float Distance(Vector3 a,Vector3 b) => Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
        try
        {
            var floor=Create("Volley test floor"); var floorCollider=floor.AddComponent<BoxCollider>();
            floor.transform.position=Vector3.down*0.5f; floorCollider.size=new Vector3(30,1,30);
            var player=Create("Volley test player"); var shooter=player.AddComponent<PlayerBowShooter>();
            var enemies=new List<Enemy>();
            for(int i=0;i<6;i++)
            {
                var go=Create("Enemy "+i);
                float angle=i*Mathf.PI*2/5;
                go.transform.position=new Vector3(Mathf.Cos(angle)*2.7f,0,Mathf.Sin(angle)*2.7f);
                var enemy=go.AddComponent<Enemy>();
                typeof(Enemy).GetField("currentHealth",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(enemy,100);
                var body=go.AddComponent<BoxCollider>(); body.center=Vector3.up; body.size=new Vector3(0.5f,2,0.5f);
                // Several colliders on one enemy must never count as several enemies.
                var extra=go.AddComponent<SphereCollider>(); extra.center=Vector3.up; extra.radius=0.2f;
                enemies.Add(enemy);
            }
            var outside=enemies[5]; outside.transform.position=new Vector3(5.01f,0,0);
            var samples=new HashSet<Vector3>();
            for(int count=1;count<=5;count++)
            {
                for(int i=0;i<5;i++) enemies[i].gameObject.SetActive(i<count);
                Physics.SyncTransforms();
                var repeated=new HashSet<Enemy>();
                for(int seed=0;seed<40;seed++)
                {
                    UnityEngine.Random.InitState(seed+count*100);
                    var points=new List<Vector3>();
                    for(int shot=0;shot<3;shot++)
                    {
                        var args=new object[]{shot,Vector3.zero};
                        bool found=(bool)Call(shooter,"TryGetChargedVolleyPoint",args);
                        var point=(Vector3)args[1];
                        Check(found,"Could not find valid ground with "+count+" enemies.");
                        var targets=(Enemy[])Field(shooter,"volleyReleasedTargets"); var target=targets[shot];
                        Check(target!=null && target!=outside && enemies.IndexOf(target)<count,"Selected an unavailable/out-of-range enemy.");
                        Check(Distance(point,target.transform.position)>=shooter.volleyMinOffset-0.001f && Distance(point,target.transform.position)<=shooter.volleyMaxOffset+0.001f,"Landing offset outside configured annulus.");
                        Check(Distance(point,Vector3.zero)<=shooter.heavyGroundDistance+0.001f && Mathf.Abs(point.y)<0.001f,"Landing outside radius or off ground.");
                        foreach(var earlier in points) Check(Distance(point,earlier)>=shooter.volleyPointSeparation-0.001f,"Duplicate/overlapping landing points.");
                        foreach(var enemy in enemies.Where(e=>e.isActiveAndEnabled))
                        {
                            Check(Distance(point,enemy.transform.position)>=shooter.volleyMinOffset-0.001f,"Point directly on another enemy.");
                            var bounds=enemy.GetComponent<BoxCollider>().bounds;
                            Check(point.x<bounds.min.x || point.x>bounds.max.x || point.z<bounds.min.z || point.z>bounds.max.z,"Point inside enemy body footprint.");
                        }
                        points.Add(point); samples.Add(point);
                    }
                    var used=((Enemy[])Field(shooter,"volleyReleasedTargets")).GroupBy(e=>e).ToArray();
                    Check(used.Length==Mathf.Min(count,3),"Volley enemy distribution incorrect.");
                    if(count==2) { Check(used.Any(g=>g.Count()==2) && used.Any(g=>g.Count()==1),"Two enemies must receive 2+1 arrows."); repeated.Add(used.Single(g=>g.Count()==2).Key); }
                }
                if(count==2) Check(repeated.Count==2,"The doubled enemy never randomized.");
            }
            Check(samples.Count>500,"Scatter did not vary across volleys.");
            for(int i=0;i<5;i++) enemies[i].gameObject.SetActive(i<2);
            Physics.SyncTransforms();
            var first=new object[]{0,Vector3.zero}; Check((bool)Call(shooter,"TryGetChargedVolleyPoint",first),"Initial moving-target shot failed.");
            var assigned=(Enemy[])Field(shooter,"volleyAssignments"); var moved=assigned[1];
            moved.transform.position=new Vector3(8,0,0); Physics.SyncTransforms();
            var second=new object[]{1,Vector3.zero}; Check((bool)Call(shooter,"TryGetChargedVolleyPoint",second),"Replacement shot failed.");
            Check(((Enemy[])Field(shooter,"volleyReleasedTargets"))[1]!=moved,"Enemy outside radius remained selected.");
            foreach(var enemy in enemies) enemy.gameObject.SetActive(false);
            Physics.SyncTransforms();
            for(int shot=0;shot<3;shot++) Check((bool)Call(shooter,"TryGetChargedVolleyPoint",new object[]{shot,Vector3.zero}),"No-enemy ground fallback failed.");
            floor.SetActive(false); Physics.SyncTransforms();
            Check(!(bool)Call(shooter,"TryGetChargedVolleyPoint",new object[]{0,Vector3.zero}),"Missing ground must never fabricate a direct target.");

            // Exercise the real shooter route: timeline releases enqueue independently,
            // keep elemental payload creation, and leave the short-press route unchanged.
            floor.SetActive(true); enemies[0].gameObject.SetActive(true);
            enemies[0].transform.position=new Vector3(0,0,2);
            Physics.SyncTransforms();
            var manager=player.GetComponent<PlayerStateManager>();
            Call(shooter,"Awake"); shooter.hitLayers=0;
            shooter.launchPoint=Create("Test muzzle").transform; shooter.launchPoint.position=Vector3.up*1.5f;
            var prefab=Create("Test arrow prefab"); prefab.transform.position=Vector3.up*100;
            shooter.arrowPrefab=prefab.AddComponent<BowArrowProjectile>();
            Set(manager,"bowHeavyActive",true); Set(manager,"bowHeavyCharged",true); Set(manager,"bowHeavyCharging",false);
            var timeline=(BowHeavyAttackTimeline)Field(manager,"bowHeavyTimeline");
            timeline.Begin(new[]{25,30,35},57);
            var releasedPoints=new List<Vector3>(); var releaseFrames=new List<float>();
            shooter.ArrowSpawned += arrow => {
                releasedPoints.Add(arrow.GroundPoint); releaseFrames.Add(timeline.Frame);
                Check(arrow.IsGroundShot && arrow.GetComponent<ElementalGems.GemArrowPayload>()!=null,"Shooter lost ground/elemental payload routing.");
                UnityEngine.Object.DestroyImmediate(arrow.gameObject);
            };
            Check(!timeline.Advance(24),"Volley released early.");
            foreach(float step in new[]{1f,5f,5f})
            {
                Check(timeline.Advance(step),"Missing timeline release.");
                Call(shooter,"QueueArrow"); Call(shooter,"LateUpdate");
            }
            Check(releaseFrames.SequenceEqual(new[]{25f,30f,35f}) && releasedPoints.Distinct().Count()==3,"Actual shooter did not create three distinct frame-timed arrows.");
            foreach(var p in releasedPoints) Check(Distance(p,enemies[0].transform.position)>=shooter.volleyMinOffset && Distance(p,enemies[0].transform.position)<=shooter.volleyMaxOffset,"Charged route used direct/forward targeting.");
            Set(manager,"bowHeavyCharged",false); timeline.Begin(new[]{22},45);
            Check(!timeline.Advance(21) && timeline.Advance(1),"Single timing changed.");
            Call(shooter,"QueueArrow"); Call(shooter,"LateUpdate");
            Check(releasedPoints.Count==4 && releaseFrames[3]==22 && Distance(releasedPoints[3],new Vector3(0,0,5))<0.01f,"Single heavy must remain one forward-ground arrow at frame 22.");
            Debug.Log("[Bow Volley Validation] PASS: "+checks+" assertions; 200 volleys; 1/2/3/4/5-enemy distribution; random 2+1 assignment; distinct ground points; collider exclusion; 5m cap; moving target revalidation; no-enemy/missing-floor handling; real shooter frames 25/30/35; elemental payload; single frame 22 regression.");
        }
        finally { UnityEngine.Random.state=random; EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static object Field(object obj,string name) => obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
    private static void Set(object obj,string name,object value) => obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
    private static object Call(object obj,string name,params object[] args) => obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
}
