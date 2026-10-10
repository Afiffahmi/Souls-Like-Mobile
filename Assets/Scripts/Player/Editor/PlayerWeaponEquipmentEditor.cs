using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerWeaponEquipment))]
public sealed class PlayerWeaponEquipmentEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var equipment = (PlayerWeaponEquipment)target;
        EditorGUILayout.HelpBox("Upgrade each weapon independently. Bonuses can target light, heavy, special, or a specific combo step. Agility speeds up attack animation, bow charge/draw and recovery. Existing attacks keep their captured stats. Skill cooldowns are separate.", MessageType.Info);
        Draw(equipment, PlayerCombatMode.Sword, equipment.sword);
        Draw(equipment, PlayerCombatMode.Bow, equipment.bow);
        Draw(equipment, PlayerCombatMode.Magic, equipment.magic);
    }
    void Draw(PlayerWeaponEquipment equipment, PlayerCombatMode mode, WeaponLoadout loadout)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(mode.ToString(), EditorStyles.boldLabel);
        if (loadout == null) { EditorGUILayout.HelpBox("Missing loadout. Reset this component to restore defaults.", MessageType.Error); return; }
        var definition = (WeaponDefinition)EditorGUILayout.ObjectField("Weapon definition", loadout.definition, typeof(WeaponDefinition), false);
        if (definition != loadout.definition)
        {
            if (definition == null || definition.weapon == mode)
            { Undo.RecordObject(equipment, "Change weapon definition"); loadout.definition = definition; Changed(equipment); }
            else Debug.LogWarning("Choose a " + mode + " weapon definition.", equipment);
        }
        if (loadout.definition == null) return;
        if (!loadout.definition.IsValid || loadout.definition.weapon != mode)
        { EditorGUILayout.HelpBox("Definition needs the matching weapon and 3–4 distinct slots.", MessageType.Error); return; }
        int level = EditorGUILayout.IntSlider("Upgrade level", loadout.upgradeLevel, 0, Mathf.Clamp(loadout.definition.maxUpgradeLevel, 0, 100));
        if (level != loadout.upgradeLevel)
        { Undo.RecordObject(equipment, "Upgrade weapon"); equipment.TrySetUpgradeLevel(mode, level); Changed(equipment); }
        foreach (var slot in loadout.definition.slots)
        {
            var current = loadout.Equipped(slot);
            var next = (WeaponAccessoryDefinition)EditorGUILayout.ObjectField(slot.ToString(), current, typeof(WeaponAccessoryDefinition), false);
            if (next == current) continue;
            Undo.RecordObject(equipment, "Change weapon accessory");
            if (equipment.TryEquip(mode, slot, next)) Changed(equipment);
            else Debug.LogWarning("Accessory must match " + mode + " / " + slot + ".", equipment);
        }
        foreach (CombatAttackInput input in System.Enum.GetValues(typeof(CombatAttackInput)))
        {
            var stats = equipment.Capture(mode, input);
            EditorGUILayout.LabelField(input.ToString(), $"Damage ({stats.FlatDamage:+0.##;-0.##;0} flat) x{stats.DamageMultiplier:0.##} | Agility x{stats.AgilityMultiplier:0.##} | Knockback time x{stats.KnockbackDurationMultiplier:0.##}");
        }
    }
    static void Changed(PlayerWeaponEquipment equipment)
    {
        EditorUtility.SetDirty(equipment);
        if (PrefabUtility.IsPartOfPrefabInstance(equipment)) PrefabUtility.RecordPrefabInstancePropertyModifications(equipment);
    }
}
