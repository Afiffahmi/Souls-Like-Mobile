using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WeaponUpgradeValidation
{
    static readonly List<string> checks = new List<string>();
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add("PASS: " + label); }
    static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;
    static object Field(object o, string name) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, args);
    [MenuItem("Tools/Combat/Validate Weapon Upgrades")]
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
        checks.Clear();
        var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
        var accessory = ScriptableObject.CreateInstance<WeaponAccessoryDefinition>();
        var wrong = ScriptableObject.CreateInstance<WeaponAccessoryDefinition>();
        try
        {
            var loadout = new WeaponLoadout { definition = definition };
            Check(Near(loadout.Evaluate(CombatAttackInput.LightAttack).Damage(20), 20), "Empty level-zero loadout preserves base damage");
            loadout.upgradeLevel = 2;
            Check(Near(loadout.Evaluate(CombatAttackInput.LightAttack).Damage(20), 24), "Two upgrades add 20% damage");
            Check(Near(loadout.Evaluate(CombatAttackInput.LightAttack).Duration(1.04f), 1), "Agility divides attack duration");
            accessory.weapon = PlayerCombatMode.Sword; accessory.slot = WeaponAccessorySlot.Sheath;
            accessory.modifiers = new[] { new WeaponStatModifier { flatDamage = 5, damagePercent = 30, agilityPercent = 46 } };
            Check(loadout.TryEquip(WeaponAccessorySlot.Sheath, accessory), "Matching sword sheath equips");
            var captured = loadout.Evaluate(CombatAttackInput.LightAttack);
            Check(Near(captured.Damage(20), 37.5f) && Near(captured.Duration(3), 2), "Flat damage, upgrade, accessory and agility compose correctly");
            Check(Near(captured.DamagePerSecond(10), 15), "Periodic effects do not repeat flat damage bonuses");
            loadout.accessories.Add(accessory);
            Check(Near(loadout.Evaluate(CombatAttackInput.LightAttack).Damage(20), 37.5f), "Duplicate serialized accessories cannot stack in one slot");
            wrong.weapon = PlayerCombatMode.Bow; wrong.slot = WeaponAccessorySlot.String;
            Check(!loadout.TryEquip(WeaponAccessorySlot.Sheath, wrong) && !loadout.TryEquip(WeaponAccessorySlot.String, wrong), "Wrong weapon and nonexistent slot rejected");
            Check(loadout.TryEquip(WeaponAccessorySlot.Sheath, null) && loadout.accessories.Count == 0, "Unequip removes duplicate slot entries");
            Check(Near(captured.Damage(20), 37.5f), "Captured attack is unchanged after unequipping");
            accessory.modifiers[0] = new WeaponStatModifier { attacks = WeaponAttackMask.Light, attackNumber = 3, damagePercent = 50 };
            loadout.upgradeLevel = 0; loadout.TryEquip(WeaponAccessorySlot.Sheath, accessory);
            Check(Near(loadout.Evaluate(CombatAttackInput.LightAttack, 3).Damage(20), 30) &&
                Near(loadout.Evaluate(CombatAttackInput.LightAttack, 2).Damage(20), 20) &&
                Near(loadout.Evaluate(CombatAttackInput.HeavyAttack, 3).Damage(20), 20), "Combo-step bonus affects only the selected input and step");
            accessory.modifiers[0] = new WeaponStatModifier { attacks = WeaponAttackMask.Special, agilityPercent = 50 };
            Check(Near(loadout.Evaluate(CombatAttackInput.SpecialAttack).Duration(3), 2) &&
                Near(loadout.Evaluate(CombatAttackInput.LightAttack).Duration(3), 3), "Special-only agility does not affect light attacks");
            Check(Near(new WeaponAttackStats(0, -5, -5).AgilityMultiplier, .1f) && Near(new WeaponAttackStats(0, 1, 20).AgilityMultiplier, 4), "Agility bounds prevent zero-speed and runaway animation");
            Check(Near(new WeaponAttackStats(float.NaN, float.NaN, float.PositiveInfinity).Damage(20), 20), "Nonfinite stats sanitize safely");
        }
        finally { UnityEngine.Object.DestroyImmediate(definition); UnityEngine.Object.DestroyImmediate(accessory); UnityEngine.Object.DestroyImmediate(wrong); }

        var scene = EditorSceneManager.OpenPreviewScene("Assets/main_scene_test.unity");
        try
        {
            var player = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerStateManager>(true)).Single();
            var equipment = player.GetComponent<PlayerWeaponEquipment>();
            Check(equipment != null && new[] { equipment.sword, equipment.bow, equipment.magic }.All(l => l.definition.IsValid && l.definition.slots.Length == 4), "Scene player has three valid four-slot weapon loadouts");
            Check(!equipment.TrySetUpgradeLevel(PlayerCombatMode.Sword, -1) && !equipment.TrySetUpgradeLevel(PlayerCombatMode.Sword, 101), "Invalid upgrade levels rejected");
            equipment.TrySetUpgradeLevel(PlayerCombatMode.Sword, 2);
            Check(Near(equipment.Capture(PlayerCombatMode.Bow, CombatAttackInput.LightAttack).Damage(20), 20) &&
                Near(equipment.Capture(PlayerCombatMode.Magic, CombatAttackInput.LightAttack).Damage(20), 20), "Sword upgrades do not leak into bow or magic");
            var magicFocus = AssetDatabase.LoadAssetAtPath<WeaponAccessoryDefinition>("Assets/Data/Combat/Weapons/Accessories/Magic_QuickFocus.asset");
            Check(equipment.TryEquip(PlayerCombatMode.Magic, WeaponAccessorySlot.Focus, magicFocus) &&
                Near(player.CaptureWeaponStats(PlayerCombatMode.Magic, CombatAttackInput.SpecialAttack).Duration(1), .8f), "Future magic skills receive their own accessory stats");
            var animator = player.anim;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.Update(0);
            Call(player, "InitializeCombat");
            var config = AssetDatabase.LoadAssetAtPath<CombatAttackConfiguration>("Assets/Animation/PlayerLocomotion/Combat/Sword/Attacks/SwordAttackConfiguration.asset");
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            var states = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)).OfType<AnimatorState>()
                .Where(s => s.behaviours.OfType<CombatAttackState>().Any()).ToArray();
            Check(states.Length > 0 && states.All(s => s.speedParameterActive && controller.parameters.Any(p => p.name == s.speedParameter && p.type == AnimatorControllerParameterType.Float)), "All configured attack animations have agility parameters");
            var quick = AssetDatabase.LoadAssetAtPath<WeaponAccessoryDefinition>("Assets/Data/Combat/Weapons/Accessories/Sword_QuickdrawSheath.asset");
            equipment.TryEquip(PlayerCombatMode.Sword, WeaponAccessorySlot.Sheath, quick);
            var expected = equipment.Capture(PlayerCombatMode.Sword, CombatAttackInput.LightAttack);
            animator.SetInteger("CombatMode", 1);
            animator.Play(config.StatePath(CombatAttackInput.LightAttack, 0), 0, 0); animator.Update(0);
            var behaviour = animator.GetBehaviours<CombatAttackState>().First(s => s.configuration == config && s.input == CombatAttackInput.LightAttack && s.stepIndex == 0);
            behaviour.OnStateEnter(animator, animator.GetCurrentAnimatorStateInfo(0), 0);
            Check(Near(animator.GetFloat(CombatAttackState.WindupSpeedParameter(0)), expected.AgilityMultiplier), "Sword state drives animation from accessory agility");
            var sword = player.GetComponent<ElementalGems.GemSwordCombat>();
            Check(Near((float)Field(sword, "capturedDamage"), expected.Damage(sword.lightDamage)), "Sword hit system captures upgraded damage");
            equipment.TryEquip(PlayerCombatMode.Sword, WeaponAccessorySlot.Sheath, null);
            behaviour.OnStateUpdate(animator, animator.GetCurrentAnimatorStateInfo(0), 0);
            Check(Near(animator.GetFloat(CombatAttackState.WindupSpeedParameter(0)), expected.AgilityMultiplier), "Unequipping mid-swing does not change committed timing");
            Call(player, "ResetAttackSequence");

            var bowString = AssetDatabase.LoadAssetAtPath<WeaponAccessoryDefinition>("Assets/Data/Combat/Weapons/Accessories/Bow_FastString.asset");
            animator.SetInteger("CombatMode", 3);
            animator.Play("Base Layer.Attack.Bow", 0, 0); animator.Update(0);
            float AdvanceLight()
            {
                Check((bool)Call(player, "TryBeginBowAttack"), "Bow light request accepted");
                player.NotifyBowAttackEntered();
                Call(player, "AdvanceBowAttack", .1f, true);
                float frame = player.BowAnimationFrame;
                Call(player, "ResetBowAttack"); return frame;
            }
            float baseline = AdvanceLight();
            equipment.TryEquip(PlayerCombatMode.Bow, WeaponAccessorySlot.String, bowString);
            float faster = AdvanceLight();
            Check(faster > baseline, "Bow draw advances further with a faster string");
            Check((bool)Call(player, "TryBeginBowHeavyAttack"), "Heavy bow accepts upgraded loadout");
            var heavyConfig = (BowHeavyAttackConfiguration)Field(player, "bowHeavyConfiguration");
            double clock = (double)typeof(PlayerStateManager).GetProperty("BowHeavyClock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
            float shortened = player.CurrentWeaponAttackStats.Duration(heavyConfig.holdDuration);
            Call(player, "AdvanceBowHeavyHold", clock + shortened + .01, true);
            Check(!player.IsChargingBowHeavyAttack && (bool)Field(player, "bowHeavyCharged"), "Heavy bow charge completes at the agility-adjusted duration");
            var shooter = player.GetComponent<PlayerBowShooter>(); Call(shooter, "Awake");
            Call(shooter, "QueueArrow");
            var queue = (IEnumerable)Field(shooter, "pendingReleases");
            object queued = queue.Cast<object>().Single();
            float damage = (float)queued.GetType().GetField("damage").GetValue(queued);
            var field = (ElementalGems.GroundFieldSnapshot)queued.GetType().GetField("field").GetValue(queued);
            Check(Near(damage, shooter.heavyArrowDamage * .9f), "Heavy arrow captures string damage trade-off");
            Check(field != null && Near(field.damagePerSecond, shooter.heavyGroundField.damagePerSecond * .9f), "Heavy ground field captures weapon damage multiplier");
            equipment.TryEquip(PlayerCombatMode.Bow, WeaponAccessorySlot.String, null);
            Check(Near((float)queued.GetType().GetField("damage").GetValue(queued), damage) && Near(player.CurrentWeaponAttackStats.AgilityMultiplier, 1.3f), "Equipment changes cannot alter a queued arrow or committed heavy volley");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        string result = string.Join("\n", checks);
        System.IO.File.WriteAllText("Temp/WeaponUpgradeValidation.txt", result);
        Debug.Log("Weapon upgrade validation: " + checks.Count + " checks passed.");
        return result;
    }
}
