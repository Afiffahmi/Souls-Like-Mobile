using System;
using UnityEngine;

[Serializable]
public sealed class PlayerCombatLevelSettings
{
    [Min(1)] public int level = 1;
    [Tooltip("Bonus per player level above level 1, added to weapon and accessory percentages.")]
    [Min(0)] public float damagePercentPerLevel = 10;
    [Min(0)] public float speedPercentPerLevel = 2;
    [Min(0)] public float knockbackDurationPercentPerLevel = 5;
    public WeaponAttackStats Apply(WeaponAttackStats equipment)
    {
        int gained = Mathf.Clamp(level, 1, 100) - 1;
        return new WeaponAttackStats(equipment.FlatDamage,
            equipment.DamageMultiplier + gained * Mathf.Max(0, WeaponStatModifier.Finite(damagePercentPerLevel)) / 100,
            equipment.AgilityMultiplier + gained * Mathf.Max(0, WeaponStatModifier.Finite(speedPercentPerLevel)) / 100,
            equipment.KnockbackDurationMultiplier + gained * Mathf.Max(0, WeaponStatModifier.Finite(knockbackDurationPercentPerLevel)) / 100);
    }
}

public partial class PlayerStateManager
{
    [Header("Player level bonuses (level 1 preserves base stats)")]
    public PlayerCombatLevelSettings combatLevel = new PlayerCombatLevelSettings();
    public WeaponAttackStats ApplyPlayerLevelStats(WeaponAttackStats stats) => combatLevel != null ? combatLevel.Apply(stats) : stats;
    private PlayerWeaponEquipment weaponEquipment;
    private WeaponAttackStats activeWeaponStats = WeaponAttackStats.Default;
    private WeaponAttackStats bowWeaponStats = WeaponAttackStats.Default;
    private WeaponAttackStats bowHeavyWeaponStats = WeaponAttackStats.Default;
    public WeaponAttackStats CurrentWeaponAttackStats => bowHeavyActive ? bowHeavyWeaponStats : bowAttackActive ? bowWeaponStats : activeWeaponStats;

    /// <summary>Capture this value when a future spell/skill starts; use Damage and Duration on its base values.</summary>
    public WeaponAttackStats CaptureWeaponStats(PlayerCombatMode weapon, CombatAttackInput input, int attackNumber = 1)
    {
        if (weaponEquipment == null) weaponEquipment = GetComponent<PlayerWeaponEquipment>();
        return ApplyPlayerLevelStats(weaponEquipment != null && weaponEquipment.isActiveAndEnabled
            ? weaponEquipment.Capture(weapon, input, attackNumber) : WeaponAttackStats.Default);
    }
}
