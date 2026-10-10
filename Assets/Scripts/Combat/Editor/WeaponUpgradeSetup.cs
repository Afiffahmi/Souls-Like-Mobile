using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WeaponUpgradeSetup
{
    public const string Root = "Assets/Data/Combat/Weapons";
    [MenuItem("Tools/Combat/Set Up Weapon Upgrades")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Folder("Assets/Data/Combat"); Folder(Root); Folder("Assets/Data/Combat/Weapons/Accessories");
        var sword = Weapon("Sword", PlayerCombatMode.Sword, WeaponAccessorySlot.Blade, WeaponAccessorySlot.Grip, WeaponAccessorySlot.Sheath, WeaponAccessorySlot.Charm);
        var bow = Weapon("Bow", PlayerCombatMode.Bow, WeaponAccessorySlot.Limbs, WeaponAccessorySlot.String, WeaponAccessorySlot.Quiver, WeaponAccessorySlot.Charm);
        var magic = Weapon("Magic", PlayerCombatMode.Magic, WeaponAccessorySlot.Focus, WeaponAccessorySlot.Core, WeaponAccessorySlot.Conduit, WeaponAccessorySlot.Charm);
        Accessory("Sword_QuickdrawSheath", PlayerCombatMode.Sword, WeaponAccessorySlot.Sheath, 0, 20, WeaponAttackMask.All,
            "Faster sword attacks and recovery.");
        Accessory("Sword_HeavyBlade", PlayerCombatMode.Sword, WeaponAccessorySlot.Blade, 30, -15, WeaponAttackMask.All,
            "More damage with slower swings.");
        Accessory("Sword_FinisherGrip", PlayerCombatMode.Sword, WeaponAccessorySlot.Grip, 40, 0, WeaponAttackMask.Light,
            "Boosts only the third light combo attack.", 3);
        Accessory("Sword_SpecialCharm", PlayerCombatMode.Sword, WeaponAccessorySlot.Charm, 25, 10, WeaponAttackMask.Special,
            "Stronger, faster special attacks. Cooldown is unchanged.");
        Accessory("Bow_FastString", PlayerCombatMode.Bow, WeaponAccessorySlot.String, -10, 30, WeaponAttackMask.All,
            "Faster draw, charge and release with lighter hits.");
        Accessory("Bow_ReinforcedLimbs", PlayerCombatMode.Bow, WeaponAccessorySlot.Limbs, 30, -10, WeaponAttackMask.All,
            "Stronger arrows with slower attacks.");
        Accessory("Bow_HeavyQuiver", PlayerCombatMode.Bow, WeaponAccessorySlot.Quiver, 20, 10, WeaponAttackMask.Heavy,
            "Boosts heavy arrows and their ground-field damage.");
        Accessory("Bow_PrecisionCharm", PlayerCombatMode.Bow, WeaponAccessorySlot.Charm, 15, 0, WeaponAttackMask.Light,
            "Stronger light arrows.");
        Accessory("Magic_QuickFocus", PlayerCombatMode.Magic, WeaponAccessorySlot.Focus, 0, 25, WeaponAttackMask.All,
            "Faster casts when spells consume the weapon stats API.");
        Accessory("Magic_PowerCore", PlayerCombatMode.Magic, WeaponAccessorySlot.Core, 35, -15, WeaponAttackMask.All,
            "Stronger, slower spells.");
        Accessory("Magic_HeavyConduit", PlayerCombatMode.Magic, WeaponAccessorySlot.Conduit, 20, 15, WeaponAttackMask.Heavy,
            "Boosts heavy magic skills.");
        Accessory("Magic_SpecialCharm", PlayerCombatMode.Magic, WeaponAccessorySlot.Charm, 30, 0, WeaponAttackMask.Special,
            "Boosts special magic skills.");
        foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerStateManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var equipment = player.GetComponent<PlayerWeaponEquipment>() ?? Undo.AddComponent<PlayerWeaponEquipment>(player.gameObject);
            Undo.RecordObject(equipment, "Configure weapon loadouts");
            if (equipment.sword.definition == null) equipment.sword.definition = sword;
            if (equipment.bow.definition == null) equipment.bow.definition = bow;
            if (equipment.magic.definition == null) equipment.magic.definition = magic;
            EditorUtility.SetDirty(equipment);
            if (PrefabUtility.IsPartOfPrefabInstance(equipment)) PrefabUtility.RecordPrefabInstancePropertyModifications(equipment);
            var animator = player.anim != null ? player.anim : player.GetComponentInChildren<Animator>();
            var runtime = animator != null ? animator.runtimeAnimatorController : null;
            if (runtime is AnimatorOverrideController overrides) runtime = overrides.runtimeAnimatorController;
            if (runtime is AnimatorController controller) ConfigureController(controller);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }
        AssetDatabase.SaveAssets();
    }
    public static void ConfigureController(AnimatorController controller)
    {
        Undo.RegisterCompleteObjectUndo(controller, "Configure weapon agility");
        void Visit(AnimatorStateMachine machine)
        {
            foreach (var child in machine.states)
            {
                var state = child.state;
                var attack = state.behaviours.OfType<CombatAttackState>().FirstOrDefault();
                if (attack == null || attack.configuration == null) continue;
                string parameter = CombatAttackState.AttackSpeedParameter(attack.configuration.weapon, attack.input, attack.stepIndex);
                var existing = controller.parameters.FirstOrDefault(p => p.name == parameter);
                if (existing != null && existing.type != AnimatorControllerParameterType.Float) throw new InvalidOperationException("Parameter mismatch: " + parameter);
                if (existing == null) controller.AddParameter(new AnimatorControllerParameter { name = parameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
                Undo.RecordObject(state, "Configure attack agility");
                state.speedParameter = parameter; state.speedParameterActive = true;
                EditorUtility.SetDirty(state);
            }
            foreach (var child in machine.stateMachines) Visit(child.stateMachine);
        }
        foreach (var layer in controller.layers) Visit(layer.stateMachine);
        EditorUtility.SetDirty(controller);
    }
    static void Folder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'), System.IO.Path.GetFileName(path));
    }
    static WeaponDefinition Weapon(string name, PlayerCombatMode mode, params WeaponAccessorySlot[] slots)
    {
        string path = "Assets/Data/Combat/Weapons/" + name + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<WeaponDefinition>(); asset.weapon = mode; asset.slots = slots;
        AssetDatabase.CreateAsset(asset, path); return asset;
    }
    static void Accessory(string name, PlayerCombatMode mode, WeaponAccessorySlot slot, float damage, float agility, WeaponAttackMask attacks, string description, int number = 0)
    {
        string path = "Assets/Data/Combat/Weapons/Accessories/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<WeaponAccessoryDefinition>(path) != null) return;
        var asset = ScriptableObject.CreateInstance<WeaponAccessoryDefinition>();
        asset.weapon = mode; asset.slot = slot; asset.description = description;
        asset.modifiers = new[] { new WeaponStatModifier { attacks = attacks, attackNumber = number, damagePercent = damage, agilityPercent = agility } };
        AssetDatabase.CreateAsset(asset, path);
    }
}
