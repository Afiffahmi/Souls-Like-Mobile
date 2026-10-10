using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ElementalGems;
using SoulsLike.Enemies;

[InitializeOnLoad]
public static class EnemyElementalComboValidation
{
    const string Key = "EnemyElementalComboValidation";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly ElementType[] Elements = { ElementType.Fire, ElementType.Nature, ElementType.Darkness };
    static readonly List<string> checks = new List<string>();
    static EnemyBrain brain;
    static PlayerStateManager player;
    static GemSwordCombat sword;
    static ElementalEnemy elemental;
    static CombatAttackConfiguration config;
    static int stage, cycle, step, afterContact, reactions, savedGem;
    static double began;
    static float at, hitExit;
    static Vector3 position;
    static EnemyElementalComboValidation()
    {
        EditorApplication.playModeStateChanged += s => {
            if(s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
            { stage = -1; began = EditorApplication.timeSinceStartup; }
        };
        EditorApplication.update += Tick;
    }
    public static void Begin()
    {
        if(EditorApplication.isPlaying) throw new Exception("Exit Play mode first");
        SessionState.SetBool(Key,true); SessionState.SetString(Key+"Result","RUNNING"); EditorApplication.EnterPlaymode();
    }
    public static string Result() => SessionState.GetString(Key+"Result","Not run");
    static void Check(bool ok,string message)
    { if(!ok) throw new Exception(message); checks.Add("PASS: "+message); }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying) return;
        try
        {
            if(EditorApplication.timeSinceStartup-began>60) throw new Exception("Timed out at stage "+stage);
            if(stage==-1)
            {
                if(EditorApplication.timeSinceStartup-began<1) return;
                checks.Clear(); cycle=0; step=0;
                brain=UnityEngine.Object.FindFirstObjectByType<EnemyBrain>();
                player=UnityEngine.Object.FindFirstObjectByType<PlayerStateManager>();
                sword=player.GetComponent<GemSwordCombat>(); elemental=brain.GetComponent<ElementalEnemy>();
                config=player.GetComponentInChildren<Animator>().GetBehaviours<CombatAttackState>().Select(s=>s.configuration).First(c=>c!=null&&c.weapon==PlayerCombatMode.Sword);
                savedGem=PlayerPrefs.GetInt("ElementalGems.Equipped.v1",-1);
                Check(player.GetComponent<GemManager>().EquippedElement==(ElementType)savedGem,"Runtime restores the saved gem selection");
                player.enabled=false; player.transform.position=brain.HomePosition-Vector3.forward*30;
                brain.profile=UnityEngine.Object.Instantiate(brain.profile);
                brain.profile.detectionRadius=.1f; brain.profile.attacks=Array.Empty<EnemyAttackDefinition>();
                typeof(EnemyBrain).GetMethod("BeginReturn",Private).Invoke(brain,null);
                brain.StateChanged += state => {
                    if(state==EnemyState.TakingDamage) reactions++;
                    else if(brain.GetComponent<EnemyAnimationDriver>().TryGetTime("Hit",out float t)) hitExit=t;
                };
                stage=0; return;
            }
            if(stage==0)
            {
                if(brain.State!=EnemyState.Idle) return;
                elemental.ClearStatuses(); brain.Health.Heal(brain.Health.MaxHealth);
                player.GetComponent<GemManager>().Equip(Elements[cycle/2],false);
                reactions=0; step=0; position=brain.transform.position;
                stage=1;
            }
            string label=Elements[cycle/2]+(cycle%2==0?" Light":" Heavy")+" strike "+(step+1);
            if(stage==1)
            {
                var input=cycle%2==0?CombatAttackInput.LightAttack:CombatAttackInput.HeavyAttack;
                player.enabled=true;
                Check(player.NotifyAttackEntered(config,input,step),label+" enters player combo");
                sword.Begin(Animator.StringToHash(config.StatePath(input,step)),input);
                player.enabled=false;
                int before=brain.Health.CurrentHealth;
                Physics.SyncTransforms();
                var p=brain.GetComponent<CapsuleCollider>().bounds.center;
                typeof(GemSwordCombat).GetMethod("Query",Private).Invoke(sword,new object[]{p-Vector3.up*.15f,p+Vector3.up*.15f});
                Check(brain.Health.CurrentHealth<before,label+" deals contact damage");
                Check(reactions==(step==2?1:0),label+" requests Hit only on third strike");
                afterContact=brain.Health.CurrentHealth; at=Time.time; hitExit=-1;
                stage=2; return;
            }
            if(stage==2)
            {
                // Wait long enough for several DOT ticks and the entire Hit clip.
                if(step<2 && reactions!=0) throw new Exception(label+" triggered Hit from damage over time");
                if(step==2 && reactions!=1) throw new Exception(label+" restarted Hit from damage over time");
                if(Vector3.Distance(position,brain.transform.position)>.05f) throw new Exception(label+" moved the enemy");
                if(Time.time-at<1.2f) return;
                Check(brain.Health.CurrentHealth<afterContact,label+" damage-over-time ticks still reduce health");
                Check(reactions==(step==2?1:0),label+" DOT does not trigger or repeat Hit");
                if(step<2) { step++; stage=1; return; }
                Check(hitExit>=1,label+" Hit completes before AI resumes");
                Check(brain.State!=EnemyState.TakingDamage,label+" ongoing DOT does not keep enemy in Hit");
                Check(brain.GetComponentInChildren<HealthBar>().slider.value==brain.Health.CurrentHealth,label+" health bar includes contact and DOT damage");
                cycle++;
                if(cycle<Elements.Length*2) { stage=0; return; }
                Check(PlayerPrefs.GetInt("ElementalGems.Equipped.v1",-1)==savedGem,"Saved gem selection is unchanged");
                Finish(true,"Fire, Nature and Darkness light/heavy combos: damage-over-time remains active; only attack 3 plays one full Hit reaction, without displacement.");
            }
        }
        catch(Exception ex) { Finish(false,ex.ToString()); }
    }
    static void Finish(bool success,string detail)
    {
        string report=(success?"PASS":"FAIL")+"\n"+string.Join("\n",checks)+"\n"+detail;
        SessionState.SetString(Key+"Result",report); SessionState.SetBool(Key,false);
        File.WriteAllText("Library/EnemyElementalComboValidation.txt",report);
        Debug.Log(report); EditorApplication.ExitPlaymode();
    }
}
