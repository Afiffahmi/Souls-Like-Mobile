using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Runs in an isolated preview scene; does not enter Play Mode or save the user's scene.</summary>
public static class BowHeavyAttackValidation
{
    private const string Report = "Temp/BowHeavyValidation.txt";
    [MenuItem("Tools/Combat/Validate Bow Heavy Attacks")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Validate outside Play Mode.");
        var config = AssetDatabase.LoadAssetAtPath<BowHeavyAttackConfiguration>("Assets/Animation/PlayerLocomotion/Combat/Bow/BowHeavyAttackConfiguration.asset");
        Check(config != null && config.Validate(), "Configuration/clips invalid.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animation/PlayerLocomotion/PlayerLocomotion.controller");
        var machine = controller.layers[0].stateMachine.stateMachines.Single(s => s.stateMachine.name == "Attack").stateMachine;
        foreach (var name in new[] { "Bow_HeavyAttack_1", "Bow_HeavyAttack_2" })
        {
            var state = machine.states.Single(s => s.state.name == name).state;
            Check(state.timeParameterActive && state.timeParameter == "BowHeavyTime" && state.speed == 0, "Heavy frame clock missing.");
            Check(state.transitions.Length == 1 && state.transitions[0].conditions.Single().parameter == "BowHeavyFinished", "Heavy can exit before completion.");
        }
        Check(machine.anyStateTransitions.Length == 0 && controller.layers[0].stateMachine.anyStateTransitions.Length == 0, "An Any State transition could interrupt heavy.");

        var scene = EditorSceneManager.OpenPreviewScene("Assets/main_scene_test.unity");
        var arrows = new List<BowArrowProjectile>();
        PlayerBowShooter shooter = null;
        try
        {
            var player = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerStateManager>(true)).Single();
            var animator = player.anim;
            var visuals = player.GetComponent<PlayerBowVisuals>();
            shooter = player.GetComponent<PlayerBowShooter>();
            Check(shooter != null && shooter.arrowPrefab != null && visuals != null && visuals.bowPrefab != null, "Existing projectile/visual references missing.");
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.Update(0f);
            player.logStateChanges = false;
            Call(player, "InitializeCombat");
            Call(visuals, "Start");
            Call(shooter, "Awake"); Call(shooter, "OnEnable");
            shooter.hitLayers = 0; // Preview test checks spawning without hitting the user's world.
            shooter.ArrowSpawned += a => { arrows.Add(a); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(a.gameObject, scene); };
            animator.SetInteger("CombatMode", 3);
            animator.Play("Base Layer.Attack.Bow", 0, 0f); animator.Update(0f);
            int previewState = 0;
            void SyncPreviewCallbacks()
            {
                // Edit-mode Animator.Update samples states but does not dispatch SMB callbacks.
                // Invoke the authored callbacks when the evaluated state actually changes.
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (previewState == info.fullPathHash) return;
                previewState = info.fullPathHash;
                var state = machine.states.Single(s => info.IsName("Base Layer.Attack." + s.state.name)).state;
                foreach (var behaviour in state.behaviours) behaviour.OnStateEnter(animator, info, 0);
            }
            SyncPreviewCallbacks();
            var releases = new List<float>();
            player.BowReleased += () => releases.Add(player.BowHeavyAnimationFrame);

            void Tick(float dt)
            {
                Call(player, "AdvanceBowHeavyAnimation", dt);
                animator.Update(0f);
                SyncPreviewCallbacks();
                Call(visuals, "LateUpdate"); Call(shooter, "LateUpdate");
            }
            void StartHeavy(double seconds, bool held)
            {
                int before = arrows.Count;
                player.BeginHeavyAttackHold();
                Check(player.IsChargingBowHeavyAttack && player.IsAttacking && player.IsAttackMovementLocked,
                    $"Heavy press was not reserved: enabled={player.isActiveAndEnabled}, animator={animator.isInitialized}/{animator.isActiveAndEnabled}, mode={player.CombatMode}, return={animator.GetInteger("ParryReturnMode")}, transition={animator.IsInTransition(0)}, locomotion={animator.GetCurrentAnimatorStateInfo(0).IsTag("CombatLocomotion")}, equipment={player.IsChangingEquipment}, parry={player.IsParrying}, roll={player.IsRolling}, combat={Field(player,"hasCombatParameters")}, heavy={Field(player,"hasBowHeavyControl")}, held={((BowHeavyHoldCycle)Field(player,"bowHeavyCycle")).Held}");
                Check(arrows.Count == before, "Arrow fired on press.");
                double now = (double)typeof(PlayerStateManager).GetProperty("BowHeavyClock", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player);
                // Simulated editor timestamps can subtract to just below 3 due to double rounding.
                // Exact-boundary logic is tested separately with an integer-origin clock.
                Call(player, "AdvanceBowHeavyHold", now + seconds + (held ? 0.000001 : 0), held);
                animator.Update(0f); animator.Update(0f);
                SyncPreviewCallbacks();
                Check(!player.IsChargingBowHeavyAttack && player.IsBowHeavyAttacking, "Heavy selection failed.");
                Check(!player.TryAttack(CombatAttackInput.LightAttack) && !player.TryAttack(CombatAttackInput.HeavyAttack) &&
                    !player.TryAttack(CombatAttackInput.SpecialAttack) && !player.TryParry() && !player.TrySetCombatMode(PlayerCombatMode.Sword), "Action interrupted volley.");
            }
            StartHeavy(2.999, false);
            for (int i=0; i<21; i++) Tick(1f/30f);
            Check(arrows.Count == 0, "Single fired before frame 22.");
            Tick(1f/30f);
            Check(arrows.Count == 1 && releases.SequenceEqual(new[] {22f}), "Single did not spawn one arrow at 22.");
            for (int i=0; i<35; i++) Tick(1f/30f);
            Check(!player.IsAttacking, "Single failed to return to bow locomotion.");
            releases.Clear();
            StartHeavy(3, true);
            for (int i=0; i<24; i++) Tick(1f/30f);
            Check(arrows.Count == 1, "Charged fired before frame 25.");
            for (int i=0; i<11; i++) Tick(1f/30f);
            Check(arrows.Count == 4 && releases.SequenceEqual(new[] {25f,30f,35f}), "Charged frames/count incorrect.");
            Check(player.IsAttacking, "Final release lost action lock.");
            for (int i=0; i<30; i++) Tick(1f/30f);
            Check(!player.IsAttacking && !player.TryAttack(CombatAttackInput.HeavyAttack), "Held button repeated after completion.");
            player.EndHeavyAttackHold();
            Check(arrows.Count == 4 && !player.IsAttacking, "Release after charge fired single.");
            releases.Clear();
            StartHeavy(3, true);
            Tick(10f); Check(releases.SequenceEqual(new[] {25f}) && player.IsAttacking, "Hitch skipped first pose.");
            Tick(0f); Check(releases.SequenceEqual(new[] {25f,30f}) && player.IsAttacking, "Hitch merged second arrow.");
            Tick(0f); Check(releases.SequenceEqual(new[] {25f,30f,35f}) && player.IsAttacking, "Hitch merged final arrow.");
            Tick(0f); Tick(0f);
            player.EndHeavyAttackHold();
            Check(arrows.Count == 7 && arrows.Distinct().Count() == 7, "Projectiles were not spawned separately.");
            player.BeginHeavyAttackHold(); player.CancelHeavyAttackHold();
            Check(!player.IsAttacking && arrows.Count == 7, "Cancellation fired an arrow or left lock.");

            // Regression: the original bow light attack still emits exactly one projectile.
            player.bowAttackSpeed = 1f;
            Check(player.TryAttack(CombatAttackInput.LightAttack), "Existing light attack rejected.");
            animator.Update(0f); animator.Update(0f);
            SyncPreviewCallbacks();
            Call(player, "AdvanceBowAttack", 1f, false);
            animator.Update(0f); Call(visuals,"LateUpdate"); Call(shooter,"LateUpdate");
            Check(arrows.Count == 8, "Light attack projectile regression.");
            Call(player, "AdvanceBowAttack", 3f, false); animator.Update(0f); animator.Update(0f);
            SyncPreviewCallbacks();
            Check(!player.IsAttacking, "Light attack failed to exit.");
            string result = "PASS: Animator references/transitions; scene projectile wiring; single frame 22; charged frames 25/30/35; independent projectile instances; action locks; held-button latch; release after charged; hitch handling; cancellation; existing bow light attack.\n";
            File.WriteAllText(Report, result); Debug.Log("[Bow Heavy Validation] " + result);
        }
        finally
        {
            if(shooter != null) Call(shooter,"OnDisable");
            foreach(var arrow in arrows) if(arrow != null) UnityEngine.Object.DestroyImmediate(arrow.gameObject);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target,args);
    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static void Check(bool condition, string message) { if(!condition) throw new InvalidOperationException("[Bow Heavy Validation] " + message); }
}
