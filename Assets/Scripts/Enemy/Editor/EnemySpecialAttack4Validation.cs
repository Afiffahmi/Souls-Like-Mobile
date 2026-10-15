using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using ElementalGems;
using SoulsLike.Enemies;

[InitializeOnLoad]
public static class EnemySpecialAttack4Validation
{
    const string Key="EnemySpecialAttack4Validation";
    static readonly string Output=Path.GetFullPath("Temp/SpecialAttack4Review");
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static double began;
    static readonly List<string> checks=new List<string>();
    static EnemyBrain brain;static EnemySlashVisual visual;static EnemyAttackDefinition attack;static Animator animator;
    static PlayerEnemyTarget target;static PlayerOverall health;
    static EnemySpecialAttack4Validation()
    {
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))began=EditorApplication.timeSinceStartup;};
        EditorApplication.update+=Tick;
    }
    public static string Result()=>SessionState.GetString(Key+"Result","Not run");
    public static void Begin(){if(EditorApplication.isPlaying)throw new Exception("Exit Play mode first");SessionState.SetBool(Key,true);SessionState.SetString(Key+"Result","RUNNING");EditorApplication.EnterPlaymode();}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add("PASS "+message);}
    static void Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Private).Invoke(obj,args);
    static void Sample(float frame)
    {
        animator.Play("Base Layer."+attack.stateName,0,frame/(attack.animation.length*attack.animation.frameRate));animator.Update(0);
        Call(brain,"TickAttack");
        var motor=brain.GetComponent<EnemyNavMeshMotor>();if(motor.Ready)brain.transform.position=motor.NavigationPosition;
        Call(visual,"LateUpdate");
        Physics.SyncTransforms();Call(brain.GetComponent<EnemySlashDamage>(),"LateUpdate");
    }
    static void Start(int index){typeof(EnemyBrain).GetProperty("Target").SetValue(brain,target);Call(brain,"BeginAttack",index);}
    static int Waves()=>UnityEngine.Object.FindObjectsByType<EnemyGroundSlash>().Length;
    static void Capture(Scene scene,Vector3 center,Vector3 offset,string name,float ortho)
    {
        Directory.CreateDirectory(Output);
        var go=new GameObject("Special 4 validation camera",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);
        var camera=go.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.065f,.08f);
        camera.orthographic=true;camera.orthographicSize=ortho;camera.nearClipPlane=.05f;camera.farClipPlane=100;
        go.transform.position=center+offset;go.transform.LookAt(center);
        var rt=new RenderTexture(960,640,24);var previous=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(960,640,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,640),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);}
        finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup-began<1)return;
        SessionState.SetBool(Key,false);checks.Clear();
        try
        {
            brain=UnityEngine.Object.FindObjectsByType<EnemyBrain>().First(b=>b.name=="enemy_1_onehand");
            visual=brain.GetComponent<EnemySlashVisual>();animator=brain.GetComponent<EnemyAnimationDriver>().animator;
            brain.profile=UnityEngine.Object.Instantiate(brain.profile);brain.profile.transitionDuration=0;brain.profile.obstructionLayers=0;
            brain.profile.healthLossSpecialsEnabled=false;brain.profile.resetPositionEnabled=false;brain.debugRepeatSpecialAttacks=false;
            target=UnityEngine.Object.FindFirstObjectByType<PlayerEnemyTarget>();health=target.GetComponent<PlayerOverall>();
            target.GetComponent<CharacterController>().enabled=false;target.GetComponent<PlayerStateManager>().enabled=false;
            var contact=target.gameObject.AddComponent<CapsuleCollider>();contact.height=2;contact.radius=.4f;contact.center=Vector3.up;
            target.GetComponent<PlayerElementalState>().enabled=false;target.GetComponent<GemManager>().Equip(ElementType.Normal,false);target.evadeDuringRoll=false;target.parryEnabled=false;target.blockWhileHoldingGuard=false;health.maxHealth=health.currentHealth=1000;
            brain.GetComponent<EnemyNavMeshMotor>().Stop();
            target.transform.position=brain.transform.position+brain.transform.forward*8;
            int index=Array.FindIndex(brain.profile.attacks,a=>a!=null&&a.name=="EnemySpecialAttack4");attack=brain.profile.attacks[index];
            var opening=attack;var spin=opening.followUpSteps.Single();
            Check(opening.IsUsable&&opening.animation.name=="EnemySpecialAttack4_1","New jump clip and spin follow-up are usable");
            typeof(EnemyBrain).GetProperty("Target").SetValue(brain,target);
            Check((bool)typeof(EnemyBrain).GetMethod("CanUseAttack",Private).Invoke(brain,new object[]{index}),"Special 4 can start with the player 8 metres away");
            int before=Waves();
            var motor=brain.GetComponent<EnemyNavMeshMotor>();
            Vector3 launch=motor.NavigationPosition, heading=brain.transform.forward;
            Start(index);Sample(2.9f);
            Check(Vector3.Distance(motor.NavigationPosition,launch)<.05f&&visual.ActiveSlash==null,"Jump anticipation does not move or emit a slash");
            Sample(12);Vector3 midway=motor.NavigationPosition;
            Check(Vector3.ProjectOnPlane(midway-launch,Vector3.up).magnitude>1,"Jump advances toward the distant player");
            target.transform.position+=brain.transform.right*4;
            Sample(24.1f);Vector3 landing=motor.NavigationPosition;
            Check(brain.CurrentAttack==spin,"Landing automatically starts the circular spin");
            Check(Vector3.ProjectOnPlane(landing-launch,Vector3.up).magnitude>5,"Jump covers the approach distance");
            Check(Mathf.Abs(Vector3.Dot(landing-launch,Vector3.Cross(Vector3.up,heading)))<.2f,"Jump does not track a player who dodges sideways after takeoff");
            Check(health.currentHealth==1000&&Waves()==before,"Jump has no attack damage or ranged projectile");
            attack=spin;target.transform.position=landing+heading*2;
            Sample(4.9f);Check(visual.ActiveSlash==null,"No circular slash before spin frame 5");
            Sample(5.1f);Check(visual.ActiveSlash!=null&&visual.ActiveSlash.FullCircle,"Circular slash starts at spin frame 5");
            for(float frame=5.5f;frame<=20;frame+=.5f)Sample(frame);
            Check(Vector3.ProjectOnPlane(visual.ActiveSlash.transform.position-landing,Vector3.up).magnitude<.05f,"Circular slash stays centred on the landing position");
            Check(Vector3.ProjectOnPlane(motor.NavigationPosition-landing,Vector3.up).magnitude<.05f,"Enemy remains in the same area during the spin");
            Capture(brain.gameObject.scene,landing+Vector3.up,new Vector3(7,6,7),"special4-jump-spin",5);
            for(float frame=20.5f;frame<30;frame+=.5f)Sample(frame);
            Check(health.currentHealth==1000-spin.damage,"Local circular slash deals one configured hit");
            target.transform.position=landing+heading*8;Physics.SyncTransforms();
            Check(!brain.CanHit(target,spin),"Circular slash cannot hit the player at ranged distance");
            Sample(30.1f);Check(visual.ActiveSlash==null,"Circular slash ends at spin frame 30");
            Sample(42.9f);Check(Waves()==before,"Ground slash waits until spin frame 43");
            Sample(43.1f);Check(Waves()==before+1&&opening.animationAction==null,"Ground slash launches at spin frame 43 after the jump and stationary slash");
            var wave=UnityEngine.Object.FindObjectsByType<EnemyGroundSlash>().Last();
            var element=brain.GetComponent<ElementalEnemy>().CurrentElement;
            var action=(EnemyGroundSlashAction)spin.animationAction;
            var gem=Array.Find(action.elementPalette,g=>g!=null&&g.element==element);
            Color expected=gem!=null?gem.color:ElementalReactionVfx.Tint(element);
            Check(wave.Element==element&&!action.useInnateElement,"Ground slash uses the enemy's current gem");
            Check((Color)typeof(EnemyGroundSlash).GetField("tint",Private).GetValue(wave)==expected,"Ground slash colour matches the gem palette");
            Sample(44);Check(Waves()==before+1,"Ground slash launches only once");
            wave.Advance(.2f);Check(wave.TravelledDistance>0,"Ground crescent travels forward from the impact");
            Sample(90.1f);Check(brain.State!=EnemyState.Attacking,"Jump-spin sequence finishes normally");
            SessionState.SetString(Key+"Result","PASS\n"+string.Join("\n",checks));
        }
        catch(Exception e){SessionState.SetString(Key+"Result","FAIL "+e.GetBaseException()+"\n"+string.Join("\n",checks));}
        finally{EditorApplication.ExitPlaymode();}
    }
}
