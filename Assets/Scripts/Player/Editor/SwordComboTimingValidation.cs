using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

[InitializeOnLoad]
public static class SwordComboTimingValidation
{
    const string Key="SwordComboTimingValidation";
    const CombatAttackInput Light=CombatAttackInput.LightAttack;
    static readonly List<string> checks=new List<string>();
    static PlayerStateManager player;
    static Animator animator;
    static CombatAttackConfiguration config;
    static int stage;
    static double began, recoveryStarted;
    static SwordComboTimingValidation()
    {
        EditorApplication.playModeStateChanged += s => {
            if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
            { stage=-1; began=EditorApplication.timeSinceStartup; }
        };
        EditorApplication.update+=Tick;
    }
    public static void Begin()
    {
        if(EditorApplication.isPlaying) throw new Exception("Exit Play mode first");
        SessionState.SetBool(Key,true); SessionState.SetString(Key+"Result","RUNNING"); EditorApplication.EnterPlaymode();
    }
    public static string Result()=>SessionState.GetString(Key+"Result","Not run");
    static void Check(bool ok,string label)
    { if(!ok) throw new Exception(label); checks.Add("PASS: "+label); }
    static AnimatorStateInfo Info=>animator.GetCurrentAnimatorStateInfo(0);
    static bool At(int index)=>Info.IsName(config.StatePath(Light,index));
    static float Frame(int index)=>Info.normalizedTime*config.lightAttackChain[index].animation.length*config.lightAttackChain[index].animation.frameRate;
    static void Advance(float seconds)
    {
        animator.speed=1;
        animator.Update(seconds);
        animator.speed=0;
    }
    static void ToFrame(int index,float frame)
    {
        for(int i=0;i<1000 && At(index) && Frame(index)<frame;i++) Advance(.005f);
        Check(At(index),"Attack "+(index+1)+" remains active through frame "+frame);
    }
    static void StartFirst()
    {
        Check(player.TryAttack(Light),"Fresh light attack accepted from sword locomotion");
        Advance(.001f); Advance(.001f);
        Check(At(0) && player.CurrentAttackNumber==1,"Real Animator enters light attack 1");
    }
    static void EarlyChain(int index,int gate)
    {
        ToFrame(index,18);
        Check(player.TryAttack(Light),"Attack "+(index+1)+" accepts an early queued follow-up");
        ToFrame(index,gate-1);
        float last=Frame(index);
        for(int i=0;i<1000 && At(index);i++) { last=Frame(index); Advance(.005f); }
        Check(At(index+1) && player.CurrentAttackNumber==index+2,"Queued follow-up enters attack "+(index+2));
        Check(last>=gate-.3f && last<=gate+.3f,"Queued attack chains at frame "+gate+" (observed "+last.ToString("F2")+")");
    }
    static void LateChain(int index)
    {
        ToFrame(index,40);
        Check(player.TryAttack(Light),"Late follow-up accepted after attack "+(index+1)+" gate");
        Advance(.001f); Advance(.001f);
        Check(At(index+1) && player.CurrentAttackNumber==index+2,"Late follow-up starts immediately without waiting for clip end");
    }
    static void FinishUnqueued(int index)
    {
        float last=0;
        for(int i=0;i<1000 && At(index);i++) { last=Info.normalizedTime; Advance(.005f); }
        Check(Info.IsName(config.LocomotionStatePath) && last>=.995f,"Unqueued attack "+(index+1)+" plays its full clip and returns to locomotion");
    }
    static void FinishThird()
    {
        ToFrame(2,40);
        foreach(CombatAttackInput input in Enum.GetValues(typeof(CombatAttackInput)))
            Check(!player.TryAttack(input),"Attack 3 blocks "+input+" before animation completion");
        FinishUnqueued(2);
        Check(player.AttackRecoveryRemaining>=.199f && player.AttackRecoveryRemaining<=.201f,"Attack 3 starts exactly 0.2 seconds of recovery after its animation");
        foreach(CombatAttackInput input in Enum.GetValues(typeof(CombatAttackInput)))
            Check(!player.TryAttack(input),"Post-animation recovery blocks "+input);
        recoveryStarted=Time.timeAsDouble;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying) return;
        try
        {
            if(EditorApplication.timeSinceStartup-began>45) throw new Exception("Timeout at stage "+stage);
            if(stage==-1)
            {
                if(EditorApplication.timeSinceStartup-began<1) return;
                checks.Clear();
                foreach(var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
                player=UnityEngine.Object.FindFirstObjectByType<PlayerStateManager>();
                player.GetComponent<PlayerInput>()?.DeactivateInput();
                animator=player.GetComponentInChildren<Animator>();
                config=animator.GetBehaviours<CombatAttackState>().Select(b=>b.configuration).First(c=>c!=null&&c.weapon==PlayerCombatMode.Sword);
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                animator.SetInteger("CombatMode",(int)PlayerCombatMode.Sword);
                animator.SetInteger("ParryReturnMode",(int)PlayerCombatMode.Sword);
                animator.Play(config.LocomotionStatePath,0,0); Advance(.001f);
                Check(!player.IsChangingEquipment && !player.IsAttacking,"Player starts in sword locomotion");
                StartFirst(); ToFrame(0,40); FinishUnqueued(0);
                Check(player.AttackRecoveryRemaining==0,"Attack 1 has no extra recovery");
                StartFirst(); EarlyChain(0,25); ToFrame(1,40); FinishUnqueued(1);
                Check(player.AttackRecoveryRemaining==0,"Attack 2 has no extra recovery");
                StartFirst(); EarlyChain(0,25); EarlyChain(1,28); FinishThird();
                // Attempting raw Animator triggers must not start sword damage either.
                animator.SetTrigger("LightAttack"); Advance(.001f); Advance(.001f); Advance(.001f);
                Check(!At(0),"Direct Animator trigger cannot bypass finisher recovery");
                stage=0; return;
            }
            if(stage==0)
            {
                if(Time.timeAsDouble-recoveryStarted<.2)
                {
                    if(player.TryAttack(Light)) throw new Exception("Accepted attack before 0.2-second recovery ended");
                    return;
                }
                Check(player.AttackRecoveryRemaining==0,"Recovery expires after 0.2 seconds");
                StartFirst(); LateChain(0); LateChain(1); FinishThird();
                stage=1; return;
            }
            if(stage==1)
            {
                if(Time.timeAsDouble-recoveryStarted<.21) return;
                StartFirst();
                Check(!player.HasBufferedAttack,"Inputs rejected during recovery were not queued");
                Finish(true,"Actual Animator transitions verified: queued gates at frames 25/28, immediate late follow-ups, full unqueued clips, full third attack plus 0.2-second lockout.");
            }
        }
        catch(Exception ex) { Finish(false,ex.ToString()); }
    }
    static void Finish(bool success,string detail)
    {
        string report=(success?"PASS":"FAIL")+"\n"+string.Join("\n",checks)+"\n"+detail;
        SessionState.SetString(Key+"Result",report); SessionState.SetBool(Key,false);
        File.WriteAllText("Library/SwordComboTimingValidation.txt",report);
        Debug.Log(report); EditorApplication.ExitPlaymode();
    }
}
