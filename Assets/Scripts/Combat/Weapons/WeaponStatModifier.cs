using System;
using UnityEngine;

public enum WeaponAccessorySlot { Blade, Grip, Sheath, Charm, Limbs, String, Quiver, Focus, Core, Conduit }
[Flags] public enum WeaponAttackMask { None = 0, Light = 1, Heavy = 2, Special = 4, All = 7 }

[Serializable]
public sealed class WeaponStatModifier
{
    public WeaponAttackMask attacks = WeaponAttackMask.All;
    [Tooltip("0 affects every combo step. 1, 2, 3... targets that attack number.")]
    [Min(0)] public int attackNumber;
    public float flatDamage;
    [Tooltip("20 means +20% damage. Negative values allow trade-offs.")]
    public float damagePercent;
    [Tooltip("20 means +20% attack speed: attack time is divided by 1.2.")]
    public float agilityPercent;
    [Tooltip("50 means an existing knockback lasts 50% longer. Does not grant push strength or stun.")]
    public float knockbackDurationPercent;

    public bool Applies(CombatAttackInput input, int number) =>
        Enum.IsDefined(typeof(CombatAttackInput), input) &&
        (attacks & (WeaponAttackMask)(1 << (int)input)) != 0 && (attackNumber == 0 || attackNumber == number);
    public static float Finite(float value, float fallback = 0) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
}

/// <summary>Value snapshot: later equipment changes cannot alter a committed attack.</summary>
public readonly struct WeaponAttackStats
{
    public readonly float FlatDamage, DamageMultiplier, AgilityMultiplier, KnockbackDurationMultiplier;
    public static WeaponAttackStats Default => new WeaponAttackStats(0, 1, 1);
    public WeaponAttackStats(float flatDamage, float damageMultiplier, float agilityMultiplier, float knockbackDurationMultiplier = 1f)
    {
        KnockbackDurationMultiplier = Mathf.Clamp(WeaponStatModifier.Finite(knockbackDurationMultiplier, 1), 0, 4);
        FlatDamage = WeaponStatModifier.Finite(flatDamage);
        DamageMultiplier = Mathf.Clamp(WeaponStatModifier.Finite(damageMultiplier, 1), 0, 100);
        AgilityMultiplier = Mathf.Clamp(WeaponStatModifier.Finite(agilityMultiplier, 1), .1f, 4f);
    }
    public float Damage(float baseDamage) => Mathf.Max(0, WeaponStatModifier.Finite(baseDamage) + FlatDamage) * DamageMultiplier;
    // Periodic effects scale by percentage only; a per-hit flat bonus must not be added every tick.
    public float DamagePerSecond(float baseDamage) => Mathf.Max(0, WeaponStatModifier.Finite(baseDamage)) * DamageMultiplier;
    public float KnockbackDuration(float baseSeconds) => Mathf.Max(0, WeaponStatModifier.Finite(baseSeconds)) * KnockbackDurationMultiplier;
    public float Duration(float baseSeconds) => Mathf.Max(0, WeaponStatModifier.Finite(baseSeconds)) / AgilityMultiplier;
}
