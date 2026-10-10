using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using SoulsLike.Enemies;

[InitializeOnLoad]
public static class EnemyParryWindupValidation
{
    const string Key="EnemyParryWindupValidation";
    static readonly List<string> log=new List<string>();
    static EnemyBrain brain;
    static EnemyAnimationDriver driver;
    static PlayerStateManager player;
    static PlayerEnemyTarget target;
    static Animator playerAnimator;
    static CombatAttackConfiguration sword;
    static double began;
    static int attacks, parries;
    static float started;
    static bool setup, parried;
    static EnemyParryWindupValidation()
    {
        EditorApplication.playModeStateChanged+=s=>{
            if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
            { began=EditorApplication.timeSinceStartup; setup=false; }
        };
        EditorApplication.update+=Tick;
    }
    public static void Begin()
    {
        if(EditorApplication.isPlaying) throw new Exception("Exit Play mode first");
        SessionState.SetBool(Key,true); SessionState.SetString(Key+"Result","RUNNING"); EditorApplication.EnterPlaymode();
    }
    public static string Result()=>SessionState.GetString(Key+"Result","Not run");
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying) return;
        try
        {
            if(EditorApplication.timeSinceStartup-began>100) throw new Exception("Timeout attacks="+attacks+" parries="+parries);
            if(!setup)
            {
                if(EditorApplication.timeSinceStartup-began<1) return;
                setup=true; log.Clear(); attacks=parries=0;
                brain=UnityEngine.Object.FindFirstObjectByType<EnemyBrain>(); driver=brain.GetComponent<EnemyAnimationDriver>();
                player=UnityEngine.Object.FindFirstObjectByType<PlayerStateManager>(); target=player.GetComponent<PlayerEnemyTarget>();
                player.GetComponent<PlayerInput>()?.DeactivateInput(); playerAnimator=player.GetComponentInChildren<Animator>();
                sword=playerAnimator.GetBehaviours<CombatAttackState>().Select(s=>s.configuration).First(c=>c!=null&&c.weapon==PlayerCombatMode.Sword);
                brain.profile=UnityEngine.Object.Instantiate(brain.profile);
                brain.profile.requireLineOfSight=false; brain.profile.obstructionLayers=0; brain.profile.globalAttackCooldown=0;
                brain.profile.avoidRepeatingAttack=false; brain.postAttackStandbySeconds=0;
                var attack=UnityEngine.Object.Instantiate(brain.profile.attacks[0]);
                attack.windupSpeedMultiplier=.1f; attack.cooldown=0;
                brain.profile.attacks=new[]{attack};
                typeof(EnemyBrain).GetMethod("BeginReturn",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(brain,null);
                brain.AttackStarted+=a=>{
                    attacks++; parried=false; started=Time.time;
                    player.enabled=false; player.enabled=true;
                    playerAnimator.SetInteger("CombatMode",1); playerAnimator.SetInteger("ParryReturnMode",1);
                    playerAnimator.Play(sword.LocomotionStatePath,0,0); playerAnimator.Update(0);
                    log.Add("Attack "+attacks+" started: speed="+driver.animator.GetFloat(EnemyAnimationDriver.AttackPhaseSpeedParameter));
                };
                player.ParrySucceeded+=()=>{parries++;log.Add("Parry "+parries+" succeeded");};
            }
            // Keep actual parry knockback enabled while maintaining test contact.
            player.transform.position=brain.transform.position+brain.transform.forward*1.5f;
            Physics.SyncTransforms();
            if(brain.State!=EnemyState.Attacking || brain.CurrentAttack==null) return;
            var a=brain.CurrentAttack;
            if(!driver.TryGetTime(a.stateName,out float t)) return;
            float frame=t*a.animation.length*a.animation.frameRate;
            float speed=driver.animator.GetFloat(EnemyAnimationDriver.AttackPhaseSpeedParameter);
            if(frame<a.hitStartFrame-.5f && Mathf.Abs(speed-a.windupSpeedMultiplier)>.001f)
                throw new Exception("Windup lost after parry: attack="+attacks+" frame="+frame+" speed="+speed);
            if(frame>=a.hitStartFrame && !parried)
            {
                float minimum=(a.hitStartFrame/a.animation.frameRate)/a.playbackSpeed/a.windupSpeedMultiplier;
                float elapsed=Time.time-started;
                log.Add("Attack "+attacks+" reached frame "+frame.ToString("F2")+" after "+elapsed.ToString("F3")+"s, expected >= "+minimum.ToString("F3"));
                if(elapsed<minimum-.15f) throw new Exception("Attack skipped slowed windup");
                if(attacks>=3 && parries>=2) { Finish(true,"Three consecutive attacks, including two real parries with knockback, respected slow windup."); return; }
                parried=true;
                if(!player.TryParry()) throw new Exception("Player could not request parry");
            }
        }
        catch(Exception ex){Finish(false,ex.ToString());}
    }
    static void Finish(bool success,string detail)
    {
        string report=(success?"PASS":"FAIL")+"\n"+string.Join("\n",log)+"\n"+detail;
        SessionState.SetBool(Key,false);SessionState.SetString(Key+"Result",report);
        File.WriteAllText("Library/EnemyParryWindupValidation.txt",report);Debug.Log(report);EditorApplication.ExitPlaymode();
    }
}
