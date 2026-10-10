using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ElementalGems;

public static class SwordSpecialUpgradeValidation
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m,Flags).Invoke(o,a);
    static void Set(object o,string f,object v) => o.GetType().GetField(f,Flags).SetValue(o,v);
    static void Check(bool ok,string message) { if (!ok) throw new InvalidOperationException(message); }
    public static void Run(PlayerStateManager player)
    {
        var config=player.anim.GetBehaviours<CombatAttackState>().Select(b=>b.configuration).First(c=>c!=null && c.weapon==PlayerCombatMode.Sword);
        var equipment=player.GetComponent<PlayerWeaponEquipment>();
        var health=player.GetComponent<PlayerOverall>();
        var enemyTarget=player.GetComponent<SoulsLike.Enemies.PlayerEnemyTarget>();
        var gem=player.GetComponent<GemManager>();
        var effects=player.GetComponent<GemLightSlashEffects>();
        var visuals=player.GetComponent<PlayerSwordVisuals>();
        var originalLoadout=equipment.sword;
        var originalEnemies=UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        var definition=ScriptableObject.CreateInstance<WeaponDefinition>();
        var charm=ScriptableObject.CreateInstance<WeaponAccessoryDefinition>();
        charm.weapon=PlayerCombatMode.Sword;charm.slot=WeaponAccessorySlot.Charm;
        charm.modifiers=new[]{new WeaponStatModifier {attacks=WeaponAttackMask.Special,flatDamage=5,damagePercent=30,agilityPercent=20,knockbackDurationPercent=50},
            new WeaponStatModifier {attacks=WeaponAttackMask.Special,attackNumber=3,agilityPercent=66}};
        var go=new GameObject("Temporary Special Upgrade Target");go.SetActive(false);
        var collider=go.AddComponent<CapsuleCollider>();collider.height=2;collider.center=Vector3.up;
        var target=go.AddComponent<Enemy>();Set(target,"maxHealth",100000);Set(target,"currentHealth",100000);Set(target,"debugLog",false);
        var elemental=go.AddComponent<ElementalEnemy>();
        var hits=new List<int>(); target.OnDamaged+=hits.Add;
        var hiddenMarker=new GameObject("Temporary Already Hidden Equipment");
        hiddenMarker.transform.SetParent(player.transform,false);
        var hiddenRenderer=hiddenMarker.AddComponent<MeshRenderer>();hiddenRenderer.forceRenderingOff=true;
        var characterRenderers=player.GetComponentsInChildren<Renderer>(true)
            .Where(r=>(r is MeshRenderer || r is SkinnedMeshRenderer) && r.gameObject.name!="Special Body Glow")
            .ToDictionary(r=>r,r=>r.forceRenderingOff);
        void CheckRestored() => Check(characterRenderers.All(pair=>pair.Key.forceRenderingOff==pair.Value),"Original character/equipment visibility is restored, including already hidden renderers");
        try
        {
            foreach(var e in originalEnemies)e.gameObject.SetActive(false);
            health.maxHealth=health.currentHealth=1000;
            player.combatLevel=new PlayerCombatLevelSettings();
            equipment.sword=new WeaponLoadout{definition=definition};
            player.swordDefaults.special.damage=45;player.swordDefaults.special.cooldownSeconds=0;
            player.swordDefaults.special.recoverySeconds=0;player.swordDefaults.special.knockbackDurationScale=1;
            foreach(bool upgraded in new[]{false,true})
            foreach(float fps in new[]{15f,60f})
            {
                Call(player,"ResetAttackSequence");Set(player,"attackRecoveryUntil",0d);
                var cooldown=typeof(PlayerStateManager).GetField("specialCooldowns",Flags).GetValue(player);
                ((System.Collections.IDictionary)typeof(CombatSpecialCooldowns).GetField("readyAt",Flags).GetValue(cooldown)).Clear();
                player.combatLevel.level=upgraded?6:1;
                equipment.sword.upgradeLevel=upgraded?2:0;
                equipment.sword.accessories.Clear();if(upgraded)equipment.sword.TryEquip(WeaponAccessorySlot.Charm,charm);
                player.swordDefaults.special.speed=upgraded?2:1;
                player.anim.Play(config.LocomotionStatePath,0,0);player.anim.Update(0);player.anim.Update(0);
                go.transform.position=player.transform.position+Vector3.forward*4;go.SetActive(true);Physics.SyncTransforms();
                gem.Equip(ElementType.Normal,false); hits.Clear();elemental.ClearStatuses();
                var stats=player.CaptureWeaponStats(PlayerCombatMode.Sword,CombatAttackInput.SpecialAttack);
                Check(Mathf.Abs(stats.Damage(45)-(upgraded?100:45))<.001f,"Base + player level + weapon upgrades + accessory damage composition");
                Check(Mathf.Abs(stats.KnockbackDurationMultiplier-(upgraded?1.85f:1f))<.001f,"Combined knockback duration bonuses");
                float expected=upgraded?4/(2*1.34f)+1/(2*2f):5f;
                var readout=WeaponAttackReadout.Capture(equipment,equipment.sword,CombatAttackInput.SpecialAttack);
                Check(readout.seconds.All(t=>Mathf.Abs(t-expected)<.001f),"UI duration matches per-hit level and equipment speed");
                Check(player.TryAttack(CombatAttackInput.SpecialAttack),"Scaled special accepted");
                Check(characterRenderers.Keys.All(r=>r.forceRenderingOff),"Character and equipment disappear immediately on accepted special input");
                Check(health.IsInvulnerable,"Invulnerability begins with accepted input");
                player.anim.Update(0);player.anim.Update(0);Call(visuals,"LateUpdate");
                var aura=player.GetComponent<GemSwordSpecialEffects>();
                Check(aura!=null && aura.Active,"Body effect begins with the cycle");
                // Editing level/equipment mid-cycle cannot change the captured hits or timing.
                player.combatLevel.level=99; equipment.sword.upgradeLevel=10; equipment.sword.accessories.Clear();
                int pushes=0, seenHits=0;bool ghost=false;
                var revealedSteps=new HashSet<int>();var hiddenSteps=new HashSet<int>();
                for(int f=0;f<fps*6 && player.IsSwordSpecialAttacking;f++)
                {
                    int before=health.currentHealth;
                    health.TakeDamage(100000);
                    Check(!enemyTarget.ReceiveDamage(100000,null,false) && health.currentHealth==before,"Direct and enemy damage are blocked throughout the cycle");
                    Call(player,"AdvanceSwordSpecialAttack",1f/fps);
                    player.anim.Update(0);player.anim.Update(0);Call(visuals,"LateUpdate");Call(effects,"LateUpdate");
                    aura.Tick(1f/fps);ghost|=aura.VisibleAfterimages>0;
                    if(player.IsSwordSpecialAttacking)
                    {
                        bool reveal=effects.TrailActive;
                        Check(characterRenderers.All(pair=>pair.Key.forceRenderingOff==(pair.Value || !reveal)),"Character and equipment appear only with the slash window");
                        var shell=(SkinnedMeshRenderer)typeof(GemSwordSpecialEffects).GetField("shell",Flags).GetValue(aura);
                        Check(shell.enabled && !shell.forceRenderingOff,"Gem aura remains visible while character geometry is hidden");
                        int step=(int)typeof(PlayerStateManager).GetField("swordSpecialStep",Flags).GetValue(player);
                        (reveal?revealedSteps:hiddenSteps).Add(step);
                    }
                    if(hits.Count>seenHits)
                    {
                        Check(hits.Count==seenHits+1,"One damage event per accelerated hit");
                        Check(elemental.KnockbackRemaining>0,"Every scaled hit pushes");
                        float expectedPush=config.swordSpecial.baseKnockback/12*(upgraded?1.85f:1f);
                        Check(Mathf.Abs(elemental.KnockbackRemaining-expectedPush)<.001f,"Knockback uses captured scaled duration");
                        elemental.ClearStatuses();pushes++;seenHits=hits.Count;
                    }
                }
                Check(hits.Count==5 && hits.All(h=>h==(upgraded?100:45)) && pushes==5,"Exactly five correctly scaled damage hits including accelerated windows");
                Check(Mathf.Abs(player.SwordSpecialElapsed-expected)<.001f,"Actual duration matches captured per-step speed bonuses");
                Check(ghost && !aura.Active && aura.VisibleAfterimages==0,"Afterimages appear and clean up after completion");
                Check(revealedSteps.Count==5 && hiddenSteps.Count==5,"Each of the five strikes has both hidden travel and visible slash frames at base and upgraded speed");
                CheckRestored();
                Check(!health.IsInvulnerable,"Protection ends at completion");
                int hp=health.currentHealth;health.TakeDamage(7);Check(health.currentHealth==hp-7,"Damage resumes after cycle");
            }
            player.combatLevel.level=1;equipment.sword.upgradeLevel=0;player.swordDefaults.special.speed=1;
            player.anim.Play(config.LocomotionStatePath,0,0);player.anim.Update(0);player.anim.Update(0);
            go.transform.position=player.transform.position+Vector3.forward*3;Physics.SyncTransforms();
            Check(player.TryAttack(CombatAttackInput.SpecialAttack),"Gem colour test starts");player.anim.Update(0);player.anim.Update(0);
            var visual=player.GetComponent<GemSwordSpecialEffects>();
            foreach(var element in (ElementType[])Enum.GetValues(typeof(ElementType)))
            {
                if(!gem.Equip(element,false))continue;
                visual.Tick(0);
                Check(Vector3.Distance(new Vector3(visual.Tint.r,visual.Tint.g,visual.Tint.b),new Vector3(gem.Equipped.color.r,gem.Equipped.color.g,gem.Equipped.color.b))<.001f,"Body glow follows equipped gem colour");
            }
            Call(player,"ResetAttackSequence");CheckRestored();
            player.anim.Play(config.LocomotionStatePath,0,0);player.anim.Update(0);player.anim.Update(0);
            go.transform.position=player.transform.position+Vector3.forward*3;Physics.SyncTransforms();
            Check(player.TryAttack(CombatAttackInput.SpecialAttack),"Special restarts after cancellation");
            player.enabled=false;
            Check(!health.IsInvulnerable && !visual.Active && visual.VisibleAfterimages==0,"Disable clears invulnerability and all body effects");
            CheckRestored();
            player.enabled=true;
            visual.Begin();visual.enabled=false;CheckRestored();visual.enabled=true;
        }
        finally
        {
            Call(player,"ResetAttackSequence");equipment.sword=originalLoadout;
            UnityEngine.Object.DestroyImmediate(hiddenMarker);
            UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(definition);UnityEngine.Object.DestroyImmediate(charm);
            foreach(var e in originalEnemies)if(e!=null)e.gameObject.SetActive(true);
        }
    }
}
