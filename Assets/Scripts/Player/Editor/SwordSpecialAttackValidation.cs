using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

// Runs in a temporary Play Mode session. Scene and Inspector changes are discarded on exit.
[InitializeOnLoad]
public static class SwordSpecialAttackValidation
{
    const string Key = "SwordSpecialValidationPending";
    static double began;
    static SwordSpecialAttackValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode) began = EditorApplication.timeSinceStartup;
        };
        EditorApplication.update += Tick;
    }
    [MenuItem("Tools/Combat/Validate Sword Special Attack")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetBool(Key, true);
        SessionState.SetString("SwordSpecialValidation", "RUNNING");
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup - began < 1) return;
        SessionState.SetBool(Key, false);
        try { RunChecks(); }
        catch (Exception e) { SessionState.SetString("SwordSpecialValidation", "FAIL " + e); }
        finally { EditorApplication.ExitPlaymode(); }
    }
    public static string Result() => SessionState.GetString("SwordSpecialValidation", "Not run");
    public static object RunChecks()
    {
        if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("Run in Play Mode");
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerStateManager>();
        var animator = player.anim;
        var config = animator.GetBehaviours<CombatAttackState>().Select(b => b.configuration).First(c => c != null && c.weapon == PlayerCombatMode.Sword);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, flags).Invoke(o,a);
        void Set(object o, string f, object v) => o.GetType().GetField(f,flags).SetValue(o,v);
        var checks = new System.Collections.Generic.List<string>();
        void Check(bool b, string message) { if (!b) throw new System.InvalidOperationException(message); checks.Add(message); }
        var targets = new System.Collections.Generic.List<GameObject>();
        var oldEnemies = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        var origin = player.transform.position;
        try
        {
            foreach (var e in oldEnemies) e.gameObject.SetActive(false);
            player.GetComponent<UnityEngine.InputSystem.PlayerInput>()?.DeactivateInput();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = 0;
            var visuals = player.GetComponent<PlayerSwordVisuals>();
            var effects = player.GetComponent<ElementalGems.GemLightSlashEffects>();
            var melee = player.GetComponent<ElementalGems.GemSwordCombat>();
            player.GetComponent<ElementalGems.GemManager>().Equip(ElementalGems.ElementType.Normal, false);
            player.swordDefaults.special.cooldownSeconds = 0;
            player.swordDefaults.special.recoverySeconds = 0;
            player.swordDefaults.special.speed = 1;
            player.combatLevel.level = 1;
            player.GetComponent<PlayerWeaponEquipment>().sword.upgradeLevel = 0;
            player.GetComponent<PlayerWeaponEquipment>().sword.accessories.Clear();
            player.swordDefaults.special.knockbackDurationScale = 1;
            Check(config.Validate(out _), "Configuration is valid");
            foreach (int count in new[]{1,2,3,5})
            foreach (float fps in new[]{15f,30f,60f,120f})
            {
                Call(player, "ResetAttackSequence"); Set(player,"attackRecoveryUntil",0d);
                player.Controller.enabled = false;
                player.transform.SetPositionAndRotation(origin, Quaternion.identity);
                player.Controller.enabled = true;
                animator.SetInteger("CombatMode",1); animator.SetInteger("ParryReturnMode",1);
                animator.Play(config.LocomotionStatePath,0,0); animator.Update(0); animator.Update(0);
                int[] hits = new int[count];
                for (int i=0;i<count;i++)
                {
                    var go = new GameObject("Temporary Sword Special Target " + i); go.SetActive(false);
                    float angle = 2*Mathf.PI*i/count;
                    go.transform.position = origin + new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*4;
                    var col=go.AddComponent<CapsuleCollider>(); col.height=2; col.center=Vector3.up; col.radius=.3f;
                    var child = new GameObject("Extra collider"); child.transform.SetParent(go.transform,false);
                    var extra=child.AddComponent<SphereCollider>(); extra.radius=.25f; extra.center=Vector3.up;
                    var enemy=go.AddComponent<Enemy>(); Set(enemy,"maxHealth",10000); Set(enemy,"currentHealth",10000); Set(enemy,"debugLog",false);
                    go.AddComponent<ElementalGems.ElementalEnemy>();
                    int n=i; enemy.OnDamaged += _ => hits[n]++;
                    go.SetActive(true); targets.Add(go);
                }
                Physics.SyncTransforms();
                Check(player.TryAttack(CombatAttackInput.SpecialAttack), count + " targets @"+fps+": accepted");
                animator.Update(0); animator.Update(0); Call(visuals,"LateUpdate");
                Check(player.IsSwordSpecialAttacking && player.IsAttackMovementLocked,"Special locks movement");
                Check(!player.TryAttack(CombatAttackInput.LightAttack) && !player.TryParry() && !player.TrySetCombatMode(PlayerCombatMode.Bow),"Special rejects competing actions");
                int[] choices = new int[5];
                var shown = new System.Collections.Generic.HashSet<int>();
                int pushes=0, lastHits=0;
                float highest=origin.y;
                for (int frame=0;frame<fps*5+2 && player.IsSwordSpecialAttacking;frame++)
                {
                    int step=player.CurrentAttackNumber-1;
                    if (step>=0 && step<5) choices[step]=targets.FindIndex(g=>g.GetComponent<Enemy>()==player.SwordSpecialTarget);
                    Call(player,"AdvanceSwordSpecialAttack",1f/fps);
                    animator.Update(0); animator.Update(0); Call(visuals,"LateUpdate");
                    Physics.SyncTransforms(); Call(effects,"LateUpdate");
                    highest=Mathf.Max(highest,player.transform.position.y);
                    if (effects.ActiveSlash != null) shown.Add(player.CurrentAttackNumber);
                    int total=hits.Sum();
                    if(total>lastHits)
                    {
                        Check(total==lastHits+1,"Only selected target receives one hit per slash");
                        var target=player.SwordSpecialTarget.GetComponent<ElementalGems.ElementalEnemy>();
                        Check(target.KnockbackRemaining>0,"Neutral sword special applies knockback");
                        target.ClearStatuses(); pushes++; lastHits=total;
                    }
                    Call(effects,"LateUpdate");
                    Check(hits.Sum()==total,"Repeated slash sampling cannot duplicate a hit");
                }
                Check(hits.Sum()==5, count+" targets @"+fps+": exactly five hits; got "+string.Join(",",hits));
                Check(hits.All(h=>h>0),"Every nearby target is visited before repeats");
                Check(choices.Take(count).Distinct().Count()==count,"Target bag has no premature repeat");
                Check(shown.Count==5 && pushes==5,"Every attack shows a slash and knocks back");
                Check(highest<=origin.y+.2f,"Player dashes toward targets without a jump arc on level ground");
                Check(Mathf.Abs(player.SwordSpecialElapsed-5)<.0001f && !player.IsAttacking && !player.IsAttackMovementLocked,"Five-second cycle returns to locomotion");
                foreach(var go in targets) UnityEngine.Object.DestroyImmediate(go); targets.Clear();
            }
            Call(player,"ResetAttackSequence"); Set(player,"attackRecoveryUntil",0d);
            animator.Play(config.LocomotionStatePath,0,0); animator.Update(0);
            Physics.SyncTransforms();
            Check(!player.TryAttack(CombatAttackInput.SpecialAttack) && player.SpecialAttackCooldownRemaining==0,"No target does not consume cooldown");
            for (int i=0;i<2;i++)
            {
                var go=new GameObject("Temporary Special Lifecycle Target"); go.SetActive(false);
                go.transform.position=player.transform.position+Vector3.forward*(2+i);
                go.AddComponent<CapsuleCollider>();
                var e=go.AddComponent<Enemy>(); Set(e,"maxHealth",10000); Set(e,"currentHealth",10000); Set(e,"debugLog",false);
                go.AddComponent<ElementalGems.ElementalEnemy>(); go.SetActive(true); targets.Add(go);
            }
            Physics.SyncTransforms();
            Check(player.TryAttack(CombatAttackInput.SpecialAttack),"Lifecycle test starts"); animator.Update(0); animator.Update(0);
            float pausedAt=player.SwordSpecialElapsed;
            Call(player,"UpdateSwordSpecialAttack");
            Check(player.SwordSpecialElapsed==pausedAt,"Animator pause freezes the special");
            var removed=player.SwordSpecialTarget; removed.gameObject.SetActive(false);
            Call(player,"AdvanceSwordSpecialAttack",1f); animator.Update(0); animator.Update(0);
            Check(player.SwordSpecialTarget!=null && player.SwordSpecialTarget!=removed,"Unavailable target is replaced on the next hit");
            player.SwordSpecialTarget.gameObject.SetActive(false);
            Call(player,"AdvanceSwordSpecialAttack",4f); animator.Update(0); animator.Update(0);
            Check(!player.IsAttacking && player.SwordSpecialElapsed==5,"Losing all targets completes safely without extending the cycle");
            foreach(var go in targets) go.SetActive(true);
            Physics.SyncTransforms();
            Check(player.TryAttack(CombatAttackInput.SpecialAttack),"Interruption test starts"); animator.Update(0); animator.Update(0);
            Call(player,"AdvanceSwordSpecialAttack",.4f); animator.Update(0);
            animator.Play(config.LocomotionStatePath,0,0); animator.Update(0); animator.Update(0);
            Check(!player.IsSwordSpecialAttacking && !player.IsAttackMovementLocked && effects.ActiveSlash==null,"External animation interruption releases special movement and effects");
            player.swordDefaults.special.cooldownSeconds=5;
            Check(player.TryAttack(CombatAttackInput.SpecialAttack),"Cooldown test starts"); animator.Update(0); animator.Update(0);
            Call(player,"AdvanceSwordSpecialAttack",.1f);
            player.enabled=false;
            Check(!player.IsSwordSpecialAttacking && effects.ActiveSlash==null,"Disabling the player clears the special");
            player.enabled=true;
            animator.Play(config.LocomotionStatePath,0,0); animator.Update(0); animator.Update(0);
            Check(player.SpecialAttackCooldownRemaining>0 && !player.TryAttack(CombatAttackInput.SpecialAttack),"Interrupting or disabling cannot reset the cooldown");
        
            SwordSpecialUpgradeValidation.Run(player);
            checks.Add("Player-level/equipment scaling, immunity, accelerated hits and gem body effects passed");
            SessionState.SetString("SwordSpecialValidation", "PASS " + checks.Count + " checks");
            return new { status="PASS", checks=checks.Count, summary=checks.Where(s=>s.Contains("targets @") || s.Contains("No target")).ToArray() };
        }
        catch(System.Exception ex) { SessionState.SetString("SwordSpecialValidation", "FAIL " + ex); return new {status="FAIL",error=ex.ToString(),recent=checks.Skip(Mathf.Max(0,checks.Count-8)).ToArray()}; }
        finally
        {
            Call(player,"ResetAttackSequence");
            foreach(var go in targets) UnityEngine.Object.DestroyImmediate(go);
            foreach(var e in oldEnemies) if(e!=null) e.gameObject.SetActive(true);
        }
        
        
    }
}

