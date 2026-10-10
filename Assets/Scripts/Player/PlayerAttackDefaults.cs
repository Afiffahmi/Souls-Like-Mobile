using System;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class AttackDefaultSettings
{
    [Min(0)] public float damage = 18;
    [Tooltip("Default playback/casting speed before equipment SPD. 0.5 = slower, 2 = twice as fast.")]
    [Min(.01f)] public float speed = 1;
    [Tooltip("Default knockback-time scale before accessory duration bonuses. 1 preserves the gem/attack push; 0 disables it. Does not grant knockback to a hit without push.")]
    [Range(0,4)] public float knockbackDurationScale = 1;
    [Tooltip("Base charge/cast seconds. Actual casting time is divided by Speed and equipment SPD. Used by bow heavy and future spells.")]
    [Min(0)] public float castSeconds;
    [Tooltip("Default delay after the attack animation; for a combo, after its final step. Equipment SPD shortens it.")]
    [Min(0)] public float recoverySeconds;
    [Tooltip("Special skill cooldown, separate from attack/cast speed.")]
    [Min(0)] public float cooldownSeconds = 5;
    public AttackDefaultSettings Snapshot() => new AttackDefaultSettings {
        damage = Mathf.Max(0, WeaponStatModifier.Finite(damage)),
        speed = Mathf.Max(.01f, WeaponStatModifier.Finite(speed,1)),
        knockbackDurationScale = Mathf.Clamp(WeaponStatModifier.Finite(knockbackDurationScale,1),0,4),
        castSeconds = Mathf.Max(0, WeaponStatModifier.Finite(castSeconds)),
        recoverySeconds = Mathf.Max(0, WeaponStatModifier.Finite(recoverySeconds)),
        cooldownSeconds = Mathf.Max(0, WeaponStatModifier.Finite(cooldownSeconds)) };
}
[Serializable]
public sealed class WeaponDefaultSettings
{
    public AttackDefaultSettings light = new AttackDefaultSettings();
    public AttackDefaultSettings heavy = new AttackDefaultSettings { damage = 30 };
    public AttackDefaultSettings special = new AttackDefaultSettings { damage = 45 };
    public AttackDefaultSettings Get(CombatAttackInput input) => input == CombatAttackInput.HeavyAttack ? heavy :
        input == CombatAttackInput.SpecialAttack ? special : light;
}

public partial class PlayerStateManager
{
    [Header("Default Attack Tuning (before equipment bonuses)")]
    public WeaponDefaultSettings swordDefaults = new WeaponDefaultSettings();
    public WeaponDefaultSettings bowDefaults = new WeaponDefaultSettings();
    public WeaponDefaultSettings magicDefaults = new WeaponDefaultSettings();
    [Header("Sword combo defaults")]
    [Range(1,8)] public float swordComboWindupSpeed = 3;
    [Range(1,8)] public float swordLightRecoverySpeed = 2.5f;
    [Header("Sword heavy defaults")]
    [Min(0)] public float swordHeavyBaseKnockback = 5;
    [Min(0)] public float swordHeavyLowDamageMultiplier = 1, swordHeavyHighDamageMultiplier = 1.25f;
    [Min(0)] public float swordHeavyLowPushMultiplier = 1, swordHeavyHighPushMultiplier = 1.25f;
    [Tooltip("Base held low-slash passes. The second charge tier adds one pass; the third adds two. Longer holds stay at that maximum.")]
    [Min(1)] public int swordHeavyHeldLowPasses = 2;
    [Tooltip("Base seconds per charge tier, divided by Sword Heavy Speed and equipment SPD. Tiers occur at 1x, 2x and 3x this duration.")]
    [Min(.01f)] public float swordHeavyChargeSecondsPerStage = 2f;
    [Header("Bow draw defaults")]
    [Min(.01f)] public float bowDefaultDrawSpeed = 2;
    [Tooltip("Base hold seconds needed for a gem-charged light arrow, divided by Bow Light Speed and equipment SPD.")]
    [Min(.01f)] public float bowLightChargeSeconds = 4f;
    [SerializeField, HideInInspector] private bool attackDefaultsInitialized;
    private AttackDefaultSettings activeAttackDefaults, bowAttackDefaults, bowHeavyAttackDefaults;
    public AttackDefaultSettings CurrentAttackDefaults => bowSpecialActive ? bowSpecialDefaults : bowHeavyActive ? bowHeavyAttackDefaults :
        bowAttackActive ? bowAttackDefaults : activeAttackDefaults;

    public AttackDefaultSettings GetAttackDefaults(PlayerCombatMode mode, CombatAttackInput input)
    {
        InitializeAttackDefaults();
        return (mode == PlayerCombatMode.Bow ? bowDefaults : mode == PlayerCombatMode.Magic ? magicDefaults : swordDefaults).Get(input);
    }
    public AttackDefaultSettings CaptureAttackDefaults(PlayerCombatMode mode, CombatAttackInput input) => GetAttackDefaults(mode,input).Snapshot();

    public static float ChargeDuration(float baseSeconds, AttackDefaultSettings defaults, WeaponAttackStats stats) =>
        Mathf.Max(.01f, stats.Duration(Mathf.Max(0, WeaponStatModifier.Finite(baseSeconds)) / defaults.speed));

    /// <summary>One-time migration from legacy components/assets. Never writes back to shared configuration assets.</summary>
    public void InitializeAttackDefaults()
    {
        if (attackDefaultsInitialized) return;
        var animator = anim != null ? anim : GetComponentInChildren<Animator>();
        var melee = GetComponent<ElementalGems.GemSwordCombat>();
        var shooter = GetComponent<PlayerBowShooter>();
        if (melee != null) { swordDefaults.light.damage=melee.lightDamage; swordDefaults.heavy.damage=melee.heavyDamage; swordDefaults.special.damage=melee.specialDamage; }
        if (shooter != null) { bowDefaults.light.damage=shooter.arrowDamage; bowDefaults.heavy.damage=shooter.heavyArrowDamage; }
        bowDefaults.light.speed = Mathf.Max(.01f, WeaponStatModifier.Finite(bowAttackSpeed,1));
        bowDefaults.heavy.speed = bowDefaults.light.speed;
        bowDefaultDrawSpeed = Mathf.Max(.01f, WeaponStatModifier.Finite(bowDrawSpeed,1));
        if (animator != null)
        {
            foreach (var config in animator.GetBehaviours<CombatAttackState>().Select(s=>s.configuration).Where(c=>c!=null).Distinct())
            {
                var target = config.weapon==PlayerCombatMode.Bow?bowDefaults:config.weapon==PlayerCombatMode.Magic?magicDefaults:swordDefaults;
                if(config.lightAttackChain.Count>0) { target.light.speed=config.lightAttackChain[0].playbackSpeed;target.light.recoverySeconds=config.lightAttackChain.Last().postAttackRecovery; }
                if(config.heavyAttackChain.Count>0) { target.heavy.speed=config.heavyAttackChain[0].playbackSpeed;target.heavy.recoverySeconds=config.heavyAttackChain.Last().postAttackRecovery; }
                if(config.specialAttack!=null) { target.special.speed=config.specialAttack.playbackSpeed;target.special.cooldownSeconds=config.specialAttack.cooldown; }
                if(config.weapon==PlayerCombatMode.Sword) {
                    swordComboWindupSpeed=config.lightComboWindupSpeed;swordLightRecoverySpeed=config.lightComboFinisherRecoverySpeed;
                    var h=config.swordHeavyCharge;
                    if(h!=null) { swordHeavyBaseKnockback=h.baseKnockback;swordHeavyHeldLowPasses=h.heldLowPasses;
                        swordHeavyLowDamageMultiplier=h.lowDamageHeavyAttack.damageMultiplier;swordHeavyHighDamageMultiplier=h.highDamageHeavyAttack.damageMultiplier;
                        swordHeavyLowPushMultiplier=h.lowDamageHeavyAttack.knockbackMultiplier;swordHeavyHighPushMultiplier=h.highDamageHeavyAttack.knockbackMultiplier; }
                }
            }
            var bowConfig=animator.GetBehaviours<CombatBowHeavyAttackState>().Select(s=>s.configuration).FirstOrDefault(c=>c!=null);
            if(bowConfig!=null) { bowDefaults.heavy.speed=bowDefaults.light.speed*bowConfig.playbackSpeed;
                bowDefaults.heavy.castSeconds=bowConfig.holdDuration*bowConfig.playbackSpeed; }
        }
        attackDefaultsInitialized=true;
    }
}
