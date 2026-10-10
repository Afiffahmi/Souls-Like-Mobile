using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using ElementalGems;
using SoulsLike.Enemies;

[InitializeOnLoad]
public static class EnemySlashValidation
{
    const string Key="EnemySlashValidation";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> checks=new List<string>();
    static EnemyBrain brain;
    static EnemySlashVisual visual;
    static PlayerStateManager player;
    static PlayerEnemyTarget target;
    static PlayerOverall health;
    static Animator enemyAnimator, playerAnimator;
    static CombatAttackConfiguration sword;
    static EnemyAttackDefinition attack;
    static Vector3 playerPosition;
    static double began;
    static bool ready;
    static EnemySlashValidation()
    {
        EditorApplication.playModeStateChanged+=s=>{
            if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
            { began=EditorApplication.timeSinceStartup; ready=true; }
        };
        EditorApplication.update+=Tick;
    }
    public static void Begin()
    {
        if(EditorApplication.isPlaying) throw new Exception("Exit Play mode first");
        SessionState.SetBool(Key,true); SessionState.SetString(Key+"Result","RUNNING"); EditorApplication.EnterPlaymode();
    }
    public static string Result()=>SessionState.GetString(Key+"Result","Not run");
    static void Check(bool ok,string text) { if(!ok) throw new Exception(text); checks.Add("PASS: "+text); }
    static void Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Private).Invoke(obj,args);
    static void Field(object obj,string field,object value)=>obj.GetType().GetField(field,Private).SetValue(obj,value);
    static void ResetPlayer()
    {
        player.enabled=false; player.enabled=true;
        player.transform.position=playerPosition; Physics.SyncTransforms();
        playerAnimator.SetInteger("CombatMode",(int)PlayerCombatMode.Sword);
        playerAnimator.SetInteger("ParryReturnMode",(int)PlayerCombatMode.Sword);
        playerAnimator.Play(sword.LocomotionStatePath,0,0); playerAnimator.Update(0);
        health.currentHealth=health.maxHealth;
    }
    static void Start(int index)
    {
        brain.GetComponent<ElementalEnemy>().ClearStatuses();
        ResetPlayer();
        typeof(EnemyBrain).GetProperty("Target").GetSetMethod(true).Invoke(brain,new object[]{target});
        Call(brain,"BeginAttack",index);
        attack=brain.CurrentAttack;
        enemyAnimator.Play("Base Layer."+attack.stateName,0,0); enemyAnimator.Update(0);
        Check(brain.CanHit(target,attack),attack.stateName+" target is in valid range and facing");
    }
    static void Sample(float frame)
    {
        float time=frame/(attack.animation.length*attack.animation.frameRate);
        enemyAnimator.Play("Base Layer."+attack.stateName,0,time); enemyAnimator.Update(0);
        Call(brain,"TickAttack");
        Call(visual,"LateUpdate");
    }
    static void Guard()
    {
        player.BeginParryHold();
        Check(player.IsParryWindowOpen,"Guard press opens existing timed parry window");
        // Set the existing guard to its held pose independently of the editor's
        // deltaTime; this test samples animations manually in one editor tick.
        player.NotifyParryStarted(PlayerCombatMode.Sword);
        float hold=(float)typeof(PlayerStateManager).GetProperty("ParryHoldTime",Private).GetValue(player);
        Field(player,"parryPlaybackTime",hold);
        Field(player,"parryDeadline",Time.timeAsDouble-1d);
        Check(player.IsDefending && !player.IsParryWindowOpen,"Held guard remains defense after timed parry expires");
    }
    static void CheckWindup(int index)
    {
        Start(index);
        var driver=brain.GetComponent<EnemyAnimationDriver>();
        Sample(0);
        float rate=attack.animation.length*attack.animation.frameRate;
        float before=enemyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        enemyAnimator.Update(.1f);
        float advanced=(enemyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime-before)*rate;
        float expected=.1f*attack.animation.frameRate*attack.playbackSpeed*attack.windupSpeedMultiplier;
        Check(Mathf.Abs(advanced-expected)<.03f,attack.stateName+" Animator advances at configured windup speed");
        Check(health.currentHealth==1000 && visual.ActiveSlash==null,"Slow windup produces no early slash or damage");
        Sample(attack.hitStartFrame+.01f);
        Check(Mathf.Approximately(enemyAnimator.GetFloat(EnemyAnimationDriver.AttackPhaseSpeedParameter),1f),"Normal attack speed resumes at Hit Start Frame");
        before=enemyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        enemyAnimator.Update(.025f);
        advanced=(enemyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime-before)*rate;
        Check(Mathf.Abs(advanced-.025f*attack.animation.frameRate*attack.playbackSpeed)<.03f,"Active slash advances at normal attack speed");
        Sample(attack.hitEndFrame+.01f);
        Check(Mathf.Approximately(enemyAnimator.GetFloat(EnemyAnimationDriver.AttackPhaseSpeedParameter),1f),"Recovery retains normal attack speed");
        Start(index);
        enemyAnimator.speed=0;
        driver.UpdateAttackSpeed(attack,0);
        before=enemyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        enemyAnimator.Update(.2f);
        Check(enemyAnimator.speed==0 && Mathf.Abs(enemyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime-before)<.001f,"Windup multiplier preserves external stun/pause");
        enemyAnimator.speed=1;
        brain.Interrupt(); enemyAnimator.Update(0);
        Check(Mathf.Approximately(enemyAnimator.GetFloat(EnemyAnimationDriver.AttackPhaseSpeedParameter),1f),"Hit interruption resets windup multiplier");
        var state=enemyAnimator.GetCurrentAnimatorStateInfo(0);
        Check(state.IsName("Base Layer.Hit") && Mathf.Approximately(state.speedMultiplier,1f),"Hit animation is not slowed by attack windup");
    }
    static void Tick()
    {
        if(!ready || !SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup-began<1) return;
        ready=false;
        try
        {
            checks.Clear();
            brain=UnityEngine.Object.FindFirstObjectByType<EnemyBrain>(); visual=brain.GetComponent<EnemySlashVisual>();
            player=UnityEngine.Object.FindFirstObjectByType<PlayerStateManager>();
            target=player.GetComponent<PlayerEnemyTarget>(); health=player.GetComponent<PlayerOverall>();
            player.GetComponent<PlayerInput>()?.DeactivateInput(); player.GetComponent<GemManager>().Equip(ElementType.Normal,false);
            playerAnimator=player.GetComponentInChildren<Animator>(); enemyAnimator=brain.GetComponent<EnemyAnimationDriver>().animator;
            sword=playerAnimator.GetBehaviours<CombatAttackState>().Select(b=>b.configuration).First(c=>c!=null&&c.weapon==PlayerCombatMode.Sword);
            brain.GetComponent<EnemyNavMeshMotor>().Stop();
            brain.profile=UnityEngine.Object.Instantiate(brain.profile); brain.profile.transitionDuration=0;
            brain.profile.obstructionLayers=0;
            target.successfulParryKnockback=0; // Isolate slash/reaction checks from the existing parry push.
            playerPosition=brain.transform.position+brain.transform.forward*1.5f;
            health.maxHealth=1000;
            Check(visual!=null,"Reusable slash component is present");
            GemCrescentSlash cached=null;
            for(int index=0;index<3;index++)
            {
                CheckWindup(index);
                Start(index);
                string name=attack.stateName;
                Check(attack.damageAtWindowEnd && attack.slashPrefab!=null && attack.slash.enabled,name+" is configured for shared slash and end-frame damage");
                float start=attack.hitStartFrame, end=attack.hitEndFrame;
                Sample(start-.1f);
                Check(visual.ActiveSlash==null && health.currentHealth==1000,name+" has no slash or damage before start");
                Sample(start+.01f);
                Check(visual.ActiveSlash!=null && visual.ActiveSlash.gameObject.activeInHierarchy,name+" shows slash at start frame");
                if(cached!=null) Check(visual.ActiveSlash==cached,"Slash instance is reused across attacks");
                cached=visual.ActiveSlash;
                Check(cached.element==ElementType.Normal,name+" uses normal sword visual");
                Sample(end-.05f);
                Check(health.currentHealth==1000,name+" does not damage player during visible slash");
                Sample(end+.01f);
                Check(visual.ActiveSlash==null,name+" hides slash at end frame");
                Check(health.currentHealth==1000-attack.damage,name+" undefended player takes configured damage at end");
                Sample(end+2);
                Check(health.currentHealth==1000-attack.damage,name+" deals damage only once");

                Start(index); Sample(start+.1f);
                Check(player.TryParry(),name+" accepts player parry input during slash");
                Sample((start+end)*.5f);
                Check(brain.State==EnemyState.TakingDamage && health.currentHealth==1000,name+" timed parry cancels damage and reacts enemy");
                Check(visual.ActiveSlash==null,name+" successful parry immediately clears slash");

                Start(index); Guard(); Sample(start+.1f); Sample(end+.01f);
                Check(health.currentHealth==1000 && brain.State==EnemyState.Attacking,name+" held defense blocks final damage without a timed parry");

                Start(index); Guard(); Sample(start+.1f); player.EndParryHold();
                Check(!player.IsDefending,"Released guard is not defense"); Sample(end+.01f);
                Check(health.currentHealth==1000-attack.damage,name+" releasing defense before end allows damage");

                Start(index); Sample(start-.1f); Sample(end+.1f);
                Check(health.currentHealth==1000 && visual.ActiveSlash==null,name+" wholly skipped window creates no invisible hit");

                Start(index); Sample(start+.1f);
                player.transform.position=playerPosition+Vector3.up*20; Physics.SyncTransforms(); Sample(end+.1f);
                Check(health.currentHealth==1000,name+" escaping range before end avoids damage");

                Start(index); Sample(start+.1f); brain.Interrupt();
                Check(visual.ActiveSlash==null && brain.CurrentAttack==null && health.currentHealth==1000,name+" interruption cancels pending damage and slash");
            }
            Start(0); Sample(attack.hitStartFrame+.1f); brain.Health.ForceKill();
            Check(visual.ActiveSlash==null && brain.State==EnemyState.Dying,"Death clears slash and cancels pending hit");
            Finish(true,"All three enemy attacks verified: shared cached slash, authoritative start/end frames, timed parry, held/released defense, end-frame damage, one hit, misses, interruption and death.");
        }
        catch(Exception ex) { Finish(false,ex.ToString()); }
    }
    static void Finish(bool success,string detail)
    {
        string report=(success?"PASS":"FAIL")+"\n"+string.Join("\n",checks)+"\n"+detail;
        SessionState.SetString(Key+"Result",report); SessionState.SetBool(Key,false);
        File.WriteAllText("Library/EnemySlashValidation.txt",report); Debug.Log(report); EditorApplication.ExitPlaymode();
    }
}
