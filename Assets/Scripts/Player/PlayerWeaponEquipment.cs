using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WeaponLoadout
{
    public WeaponDefinition definition;
    [Min(0)] public int upgradeLevel;
    public List<WeaponAccessoryDefinition> accessories = new List<WeaponAccessoryDefinition>();

    public WeaponAccessoryDefinition Equipped(WeaponAccessorySlot slot) => accessories?.Find(a =>
        a != null && definition != null && a.weapon == definition.weapon && a.slot == slot);

    public bool TryEquip(WeaponAccessorySlot slot, WeaponAccessoryDefinition accessory)
    {
        if (definition == null || !definition.IsValid || !definition.HasSlot(slot) ||
            (accessory != null && (accessory.weapon != definition.weapon || accessory.slot != slot))) return false;
        if (accessories == null) accessories = new List<WeaponAccessoryDefinition>();
        accessories.RemoveAll(a => a == null || a.slot == slot);
        if (accessory != null) accessories.Add(accessory);
        return true;
    }

    public WeaponAttackStats Evaluate(CombatAttackInput input, int attackNumber = 1)
    {
        if (definition == null || !definition.IsValid || !Enum.IsDefined(typeof(CombatAttackInput), input)) return WeaponAttackStats.Default;
        int level = Mathf.Clamp(upgradeLevel, 0, Mathf.Clamp(definition.maxUpgradeLevel, 0, 100));
        float flat = 0, damage = level * WeaponStatModifier.Finite(definition.damagePercentPerLevel),
            agility = level * WeaponStatModifier.Finite(definition.agilityPercentPerLevel),
            knockbackDuration = level * WeaponStatModifier.Finite(definition.knockbackDurationPercentPerLevel);
        void Add(WeaponStatModifier[] modifiers)
        {
            if (modifiers == null) return;
            foreach (var m in modifiers)
                if (m != null && m.Applies(input, Mathf.Max(1, attackNumber)))
                { flat += WeaponStatModifier.Finite(m.flatDamage); damage += WeaponStatModifier.Finite(m.damagePercent); agility += WeaponStatModifier.Finite(m.agilityPercent); knockbackDuration += WeaponStatModifier.Finite(m.knockbackDurationPercent); }
        }
        Add(definition.modifiers);
        // Evaluate each defined slot once: malformed serialized lists cannot stack duplicates.
        foreach (var slot in definition.slots) Add(Equipped(slot)?.modifiers);
        return new WeaponAttackStats(flat, 1 + damage / 100, 1 + agility / 100, 1 + knockbackDuration / 100);
    }
}

[DisallowMultipleComponent]
public sealed class PlayerWeaponEquipment : MonoBehaviour
{
    public WeaponLoadout sword = new WeaponLoadout();
    public WeaponLoadout bow = new WeaponLoadout();
    public WeaponLoadout magic = new WeaponLoadout();
    public event Action<PlayerCombatMode> EquipmentChanged;

    public WeaponLoadout Loadout(PlayerCombatMode weapon) => weapon == PlayerCombatMode.Sword ? sword :
        weapon == PlayerCombatMode.Bow ? bow : weapon == PlayerCombatMode.Magic ? magic : null;
    public WeaponAttackStats Capture(PlayerCombatMode weapon, CombatAttackInput input, int attackNumber = 1)
    {
        var loadout = Loadout(weapon);
        return loadout?.definition != null && loadout.definition.weapon == weapon
            ? loadout.Evaluate(input, attackNumber) : WeaponAttackStats.Default;
    }
    public bool TryEquip(PlayerCombatMode weapon, WeaponAccessorySlot slot, WeaponAccessoryDefinition accessory)
    {
        var loadout = Loadout(weapon);
        if (loadout?.definition == null || loadout.definition.weapon != weapon || !loadout.TryEquip(slot, accessory)) return false;
        EquipmentChanged?.Invoke(weapon);
        return true;
    }
    public bool TrySetUpgradeLevel(PlayerCombatMode weapon, int level)
    {
        var loadout = Loadout(weapon);
        if (loadout?.definition == null || !loadout.definition.IsValid || loadout.definition.weapon != weapon ||
            level < 0 || level > Mathf.Clamp(loadout.definition.maxUpgradeLevel, 0, 100)) return false;
        loadout.upgradeLevel = level;
        EquipmentChanged?.Invoke(weapon);
        return true;
    }
}
