using System.Collections.Generic;
using System.Linq;
using ElementalGems;
using UnityEngine;

/// <summary>Read-only estimates from the same attack sources gameplay uses. No copied balance values.</summary>
public sealed class WeaponAttackReadout
{
    public readonly List<float> damage = new List<float>();
    public readonly List<float> seconds = new List<float>();
    public readonly List<float> knockback = new List<float>();
    public bool Available => damage.Count > 0;
    public string Damage => Range(damage);
    public string Time => Range(seconds) + "s";
    public string Knockback => Range(knockback) + "s";
    static string Range(List<float> values)
    {
        if (values.Count == 0) return "--";
        float min = values.Min(), max = values.Max();
        return Mathf.Abs(max - min) < .005f ? min.ToString("0.##") : min.ToString("0.##") + "-" + max.ToString("0.##");
    }
    void Add(WeaponAttackStats stats, float baseDamage, float duration, GemAttack gem, bool push, float damageScale = 1, float defaultKnockbackScale = 1)
    {
        // Enemy defenses, elemental matchups and temporary gem combo stacks are target-dependent.
        damage.Add(Mathf.Max(0, Mathf.RoundToInt(stats.Damage(baseDamage) * damageScale * gem.damageScale)));
        seconds.Add(stats.Duration(duration));
        knockback.Add(push ? Mathf.Max(0,gem.knockback)/12f * Mathf.Clamp(stats.KnockbackDurationMultiplier*defaultKnockbackScale,0,4) : 0);
    }
    public static WeaponAttackReadout Capture(PlayerWeaponEquipment equipment, WeaponLoadout loadout, CombatAttackInput input)
    {
        var result = new WeaponAttackReadout();
        if (equipment == null || loadout?.definition == null) return result;
        var player = equipment.GetComponent<PlayerStateManager>();
        var animator = player != null ? player.anim : null;
        if (animator == null) return result;
        var gems = equipment.GetComponent<GemManager>();
        var gem = new GemAttack(gems != null ? gems.Equipped : null);
        var mode = loadout.definition.weapon;
        var defaults = player.CaptureAttackDefaults(mode,input);
        if (mode == PlayerCombatMode.Sword)
        {
            var melee = equipment.GetComponent<GemSwordCombat>();
            var config = animator.GetBehaviours<CombatAttackState>().Select(s => s.configuration)
                .FirstOrDefault(c => c != null && c.weapon == mode);
            if (melee == null || config == null) return result;
            if (input == CombatAttackInput.SpecialAttack)
            {
                var special = config.specialAttack;
                if (special?.animation != null) result.Add(loadout.Evaluate(input), defaults.damage,
                    special.animation.length / defaults.speed + defaults.recoverySeconds, gem, true, defaultKnockbackScale:defaults.knockbackDurationScale);
                return result;
            }
            var chain = config.Chain(input);
            if (chain == null) return result;
            for (int i = 0; i < chain.Count; i++)
            {
                var step = chain[i];
                if (step?.animation == null) continue;
                var stats = loadout.Evaluate(input, i + 1);
                float length = step.animation.length, speed = defaults.speed;
                if (input == CombatAttackInput.HeavyAttack && config.UsesSwordHeavyCharge)
                {
                    var heavy = config.swordHeavyCharge;
                    var low = heavy.lowDamageHeavyAttack; var high = heavy.highDamageHeavyAttack;
                    float fps = step.animation.frameRate;
                    float tap = length / speed + defaults.recoverySeconds;
                    float fullCharge = PlayerStateManager.ChargeDuration(player.swordHeavyChargeSecondsPerStage, defaults, stats) * 3 * stats.AgilityMultiplier;
                    float held = Mathf.Max(heavy.holdCheckFrame / fps / speed, fullCharge)
                        + (length + ((Mathf.Max(1,player.swordHeavyHeldLowPasses) + 1) * (low.slash.endFrame - low.slash.startFrame)
                        - low.slash.startFrame) / fps) / speed + defaults.recoverySeconds;
                    result.Add(stats, defaults.damage, tap, gem.WithKnockbackMultiplier(player.swordHeavyLowPushMultiplier, player.swordHeavyBaseKnockback), true, player.swordHeavyLowDamageMultiplier,defaults.knockbackDurationScale);
                    result.Add(stats, defaults.damage, held, gem.WithKnockbackMultiplier(player.swordHeavyHighPushMultiplier, player.swordHeavyBaseKnockback), true, player.swordHeavyHighDamageMultiplier,defaults.knockbackDurationScale);
                }
                else
                {
                    float duration = length / speed;
                    bool light = input == CombatAttackInput.LightAttack;
                    if (light && step.slash != null && step.slash.enabled && step.slash.IsValid(step.animation))
                    {
                        float start = step.slash.startFrame / step.animation.frameRate;
                        float end = step.slash.endFrame / step.animation.frameRate;
                        duration = (start / (i > 0 ? Mathf.Clamp(player.swordComboWindupSpeed,1,8) : 1)
                            + end - start + (length - end) / Mathf.Clamp(player.swordLightRecoverySpeed,1,8)) / speed;
                    }
                    result.Add(stats, defaults.damage, duration + (i==chain.Count-1?defaults.recoverySeconds:0),
                        gem, !light || i == 2,defaultKnockbackScale:defaults.knockbackDurationScale);
                }
            }
        }
        else if (mode == PlayerCombatMode.Bow)
        {
            var shooter = equipment.GetComponent<PlayerBowShooter>();
            if (shooter == null) return result;
            var stats = loadout.Evaluate(input);
            if (input == CombatAttackInput.LightAttack)
            {
                var clip = animator.GetBehaviours<CombatBowAttackState>().Select(s => s.characterAnimation).FirstOrDefault(c => c != null);
                if (clip != null)
                {
                    float draw = (BowAttackTimeline.HoldFrame - BowAttackTimeline.DrawFrame) /
                        (Mathf.Max(.01f, player.bowDefaultDrawSpeed) * clip.frameRate * defaults.speed);
                    float release = (clip.length * clip.frameRate - BowAttackTimeline.ReleaseFrame) /
                        (clip.frameRate * defaults.speed) + defaults.recoverySeconds;
                    result.Add(stats, defaults.damage, draw + release, new GemAttack(null), false);
                    // Add() divides by agility; convert the effective charge time back for that shared path.
                    float charged = Mathf.Max(draw, PlayerStateManager.ChargeDuration(player.bowLightChargeSeconds, defaults, stats) * stats.AgilityMultiplier) + release;
                    result.Add(stats, defaults.damage, charged, gem, true, defaultKnockbackScale:defaults.knockbackDurationScale);
                }
            }
            else if (input == CombatAttackInput.HeavyAttack)
            {
                var config = animator.GetBehaviours<CombatBowHeavyAttackState>().Select(s => s.configuration).FirstOrDefault(c => c != null);
                if (config != null && config.Validate())
                {
                    result.Add(stats, defaults.damage, config.singleAnimation.length / defaults.speed + defaults.recoverySeconds, gem, true,defaultKnockbackScale:defaults.knockbackDurationScale);
                    float charge = PlayerStateManager.ChargeDuration(defaults.castSeconds, defaults, stats) * stats.AgilityMultiplier;
                    result.Add(stats, defaults.damage, charge + config.chargedAnimation.length / defaults.speed + defaults.recoverySeconds, gem, true,defaultKnockbackScale:defaults.knockbackDurationScale);
                }
            }
        }
        // Magic and bow special have no implemented attack source yet; never fabricate actual values.
        return result;
    }
}
