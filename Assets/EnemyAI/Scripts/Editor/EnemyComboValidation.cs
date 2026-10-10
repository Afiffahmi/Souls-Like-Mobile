using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using SoulsLike.Enemies;
using ElementalGems;

[InitializeOnLoad]
public static class EnemyComboValidation
{
    const string Key = "EnemyComboValidation";
    static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly List<string> checks = new List<string>();
    static int stage, before, originalHealth, cycle;
    static double began;
    static float at, hitAtExit = -1;
    static EnemyBrain brain;
    static PlayerStateManager player;
    static GemSwordCombat sword;
    static ElementalEnemy elemental;
    static CombatAttackConfiguration config;
    static HealthBar bar;
    static Vector3 position;
    static bool repeated;
    static EnemyComboValidation()
    {
        EditorApplication.playModeStateChanged += s => {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false)) { stage = -1; began = EditorApplication.timeSinceStartup; }
        };
        EditorApplication.update += Tick;
    }
    public static void Begin()
    {
        if(EditorApplication.isPlaying) throw new Exception("Exit Play mode first");
        SessionState.SetBool(Key,true); SessionState.SetString(Key+"Result","RUNNING"); EditorApplication.EnterPlaymode();
    }
    public static string Result() => SessionState.GetString(Key+"Result","Not run");
    static void Check(bool ok,string text) { if(!ok) throw new Exception(text); checks.Add("PASS: "+text); }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying) return;
        try
        {
            if(EditorApplication.timeSinceStartup-began>35) throw new Exception("Timeout stage "+stage);
            if(stage==-1)
            {
                if(EditorApplication.timeSinceStartup-began<1) return;
                checks.Clear(); cycle=0; repeated=false;
                brain=UnityEngine.Object.FindFirstObjectByType<EnemyBrain>();
                player=UnityEngine.Object.FindFirstObjectByType<PlayerStateManager>(); sword=player.GetComponent<GemSwordCombat>();
                elemental=brain.GetComponent<ElementalEnemy>(); bar=brain.GetComponentInChildren<HealthBar>();
                config=player.GetComponentInChildren<Animator>().GetBehaviours<CombatAttackState>().Select(s=>s.configuration).First(c=>c!=null&&c.weapon==PlayerCombatMode.Sword);
                player.enabled=false;
                player.transform.position=brain.HomePosition-Vector3.forward*30;
                player.GetComponent<GemManager>().Equip(ElementType.Normal,false);
                brain.profile=UnityEngine.Object.Instantiate(brain.profile); brain.profile.detectionRadius=.1f; brain.profile.attacks=Array.Empty<EnemyAttackDefinition>();
                typeof(EnemyBrain).GetMethod("BeginReturn",Private).Invoke(brain,null);
                originalHealth=brain.Health.CurrentHealth;
                brain.StateChanged += s=> { if(s!=EnemyState.TakingDamage && brain.GetComponent<EnemyAnimationDriver>().TryGetTime("Hit",out var t)) hitAtExit=t; };
                stage=0; return;
            }
            if(stage==0)
            {
                if(brain.State!=EnemyState.Idle) return;
                elemental.ClearStatuses();
                before=brain.Health.CurrentHealth;
                string kind=cycle==0?"Light":"Heavy";
                for(int step=0;step<2;step++)
                {
                    StartStrike(step);
                    Contact(false);
                    Check(brain.Health.CurrentHealth==before-(step+1)*(cycle==0?sword.lightDamage:sword.heavyDamage),kind+" strike "+(step+1)+" still deals damage");
                    Check(brain.State!=EnemyState.TakingDamage && ((Vector3)typeof(ElementalEnemy).GetField("push",Private).GetValue(elemental)).sqrMagnitude<.001f,kind+" strike "+(step+1)+" has no ordinary stagger or knockback");
                }
                StartStrike(2); int beforeThird=brain.Health.CurrentHealth;
                Contact(true);
                Check(brain.Health.CurrentHealth==beforeThird && brain.State!=EnemyState.TakingDamage,"Missed third "+kind+" strike does not knock back or react");
                position=brain.transform.position; Contact(false);
                Check(brain.Health.CurrentHealth==beforeThird-(cycle==0?sword.lightDamage:sword.heavyDamage),"Third "+kind+" strike connects and deals damage");
                Check(brain.State==EnemyState.TakingDamage,"Third "+kind+" strike starts Hit animation");
                var push=(Vector3)typeof(ElementalEnemy).GetField("push",Private).GetValue(elemental);
                Check(push.sqrMagnitude<.0001f,"Third "+kind+" strike applies no backward push");
                Contact(false);
                Check(brain.Health.CurrentHealth==beforeThird-(cycle==0?sword.lightDamage:sword.heavyDamage),"Multiple contacts in the same swing do not repeat damage/knockback");
                Check(bar!=null && bar.slider.value==brain.Health.CurrentHealth,"Health bar tracks combo damage");
                at=Time.time; hitAtExit=-1; repeated=false; stage=1; return;
            }
            if(stage==1)
            {
                var driver=brain.GetComponent<EnemyAnimationDriver>();
                if(Time.time-at>.2f && !repeated)
                {
                    Check(brain.State==EnemyState.TakingDamage && driver.TryGetTime("Hit",out _),"Hit stays active beyond the old 100 ms cutoff");
                    driver.TryGetTime("Hit",out float t);
                    brain.Health.TakeDamage(1, true); driver.TryGetTime("Hit",out float after);
                    Check(Mathf.Abs(after-t)<.001f,"Repeated damage does not restart Hit");
                    brain.Health.Heal(1); Check(bar.slider.value==brain.Health.CurrentHealth,"Health bar tracks healing");
                    repeated=true;
                }
                if(Vector3.Distance(brain.transform.position,position)>=.05f) throw new Exception("Enemy moved during Hit reaction");
                if(brain.State==EnemyState.TakingDamage) return;
                Check(hitAtExit>=1,"Hit finishes its final frame before another AI state");
                Check(Vector3.Distance(brain.transform.position,position)<.05f,"Hit reaction finishes without moving the enemy backward");
                if(cycle==0){cycle=1;stage=0;return;}
                brain.Health.ForceKill();
                Check(brain.State==EnemyState.Dying && !bar.GetComponent<Canvas>().enabled && bar.slider.value==0,"Death interrupts safely and empties/hides health bar");
                Finish(true,"Light/heavy third-combo contact, no backward push, full Hit playback and health display verified.");
            }
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void StartStrike(int index)
    {
        var input=cycle==0?CombatAttackInput.LightAttack:CombatAttackInput.HeavyAttack;
        player.enabled=true;
        Check(player.NotifyAttackEntered(config,input,index),"Player combat accepts combo step "+(index+1));
        sword.Begin(Animator.StringToHash(config.StatePath(input,index)),input);
        player.enabled=false;
    }
    static void Contact(bool miss)
    {
        var collider=brain.GetComponent<CapsuleCollider>();
        var p=collider.bounds.center+(miss?Vector3.up*10:Vector3.zero);
        Physics.SyncTransforms();
        typeof(GemSwordCombat).GetMethod("Query",Private).Invoke(sword,new object[]{p-Vector3.up*.15f,p+Vector3.up*.15f});
    }
    static void Finish(bool success,string detail)
    {
        string report=(success?"PASS":"FAIL")+"\n"+string.Join("\n",checks)+"\n"+detail;
        SessionState.SetString(Key+"Result",report);SessionState.SetBool(Key,false);
        File.WriteAllText("Library/EnemyComboValidation.txt",report);Debug.Log(report);EditorApplication.ExitPlaymode();
    }
}
