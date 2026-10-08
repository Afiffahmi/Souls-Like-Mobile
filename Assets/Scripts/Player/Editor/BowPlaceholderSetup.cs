using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BowPlaceholderSetup
{
    public const string Folder = "Assets/Animation/PlayerLocomotion/Combat/Bow";
    public const string PrefabPath = Folder + "/BowPlaceholder.prefab";
    private const string ControllerPath = "Assets/Animation/PlayerLocomotion/PlayerLocomotion.controller";
    private const string CharacterPath = "Assets/DoubleL/FBX_Animations/Bow/Attack A/Bow_Attack_A_1_All.fbx";
    private const string ModelPath = "Assets/Bow_Unity_Ready.fbx";

    [MenuItem("Tools/Combat/Rebuild Bow Placeholder Assets")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Build bow assets outside Play Mode.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var character = FindClip(CharacterPath, "Bow_Attack_A_1_All");
        var draw = FindClip(ModelPath, "Bow_Draw.001");
        var hold = FindClip(ModelPath, "Bow_Hold.001");
        var release = FindClip(ModelPath, "Bow_Release.001");
        Check(controller != null && sourceModel != null && character != null && draw != null &&
            hold != null && release != null, "Bow source assets or animation clips are missing.");
        Check(character.length * character.frameRate > BowAttackTimeline.ReleaseFrame,
            "The character clip must include frames 11 through the release after frame 35.");
        var attack = controller.layers[0].stateMachine.stateMachines.Single(s => s.stateMachine.name == "Attack").stateMachine;
        var locomotion = attack.states.Single(s => s.state.name == "Bow").state;
        var trees = AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<BlendTree>()
            .ToDictionary(t => t.name, EditorJsonUtility.ToJson);
        var equipment = attack.states.Where(s => s.state.name == "Bow_Equip" || s.state.name == "Bow_Unequip")
            .ToDictionary(s => s.state.name, s => EditorJsonUtility.ToJson(s.state));

        AnimationClip bodyClip = CopyClip(character, "Bow_LightAttack");
        CopyClip(draw, "Bow_Model_Draw");
        CopyClip(hold, "Bow_Model_Hold");
        CopyClip(release, "Bow_Model_Release");
        EnsureParameter(controller, "BowTime", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "BowFinished", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "LightAttack", AnimatorControllerParameterType.Trigger);
        Undo.RegisterCompleteObjectUndo(controller, "Set up bow attack");
        var state = attack.states.FirstOrDefault(s => s.state.name == "Bow_LightAttack").state;
        if (state == null) state = attack.AddState("Bow_LightAttack", new Vector3(940, 640));
        state.motion = bodyClip;
        state.tag = "CombatAttack";
        state.writeDefaultValues = false;
        state.speed = 0f;
        state.timeParameter = "BowTime";
        state.timeParameterActive = true;
        var behaviour = state.behaviours.OfType<CombatBowAttackState>().FirstOrDefault();
        if (behaviour == null) behaviour = state.AddStateMachineBehaviour<CombatBowAttackState>();
        behaviour.characterAnimation = bodyClip;
        foreach (var transition in state.transitions.ToArray()) state.RemoveTransition(transition);
        var back = state.AddTransition(locomotion);
        Configure(back);
        back.name = "Bow finished";
        back.AddCondition(AnimatorConditionMode.If, 0f, "BowFinished");
        foreach (var transition in locomotion.transitions.Where(t => t.name == "Bow/LightAttack").ToArray())
            locomotion.RemoveTransition(transition);
        var entry = locomotion.AddTransition(state);
        Configure(entry);
        entry.name = "Bow/LightAttack";
        entry.AddCondition(AnimatorConditionMode.If, 0f, "LightAttack");
        entry.AddCondition(AnimatorConditionMode.Equals, (int)PlayerCombatMode.Bow, "CombatMode");
        locomotion.transitions = new[] { entry }.Concat(locomotion.transitions.Where(t => t != entry)).ToArray();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath)) EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(controller);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            var instance = UnityEngine.Object.Instantiate(sourceModel);
            try
            {
                instance.name = "BowPlaceholder";
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                {
                    animator.applyRootMotion = false;
                    animator.enabled = false;
                }
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                    if (size > 0.001f) instance.transform.localScale *= 1.5f / size;
                }
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        Check(trees.All(pair => AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<BlendTree>()
            .Any(t => t.name == pair.Key && EditorJsonUtility.ToJson(t) == pair.Value)), "Locomotion trees changed.");
        Check(equipment.All(pair => attack.states.Any(s => s.state.name == pair.Key &&
            EditorJsonUtility.ToJson(s.state) == pair.Value)), "Existing bow equipment states changed.");
        ValidateTimeline();
        Debug.Log("[Bow Setup] Assets ready; locomotion trees and bow equip/unequip states preserved.");
    }

    [MenuItem("Tools/Combat/Attach Bow Placeholder to Selected Player")]
    public static void AttachSelected()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Attach outside Play Mode.");
        var player = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<PlayerStateManager>() : null;
        Check(player != null, "Select the player root first.");
        var visuals = player.GetComponent<PlayerBowVisuals>() ?? Undo.AddComponent<PlayerBowVisuals>(player.gameObject);
        Undo.RecordObject(visuals, "Assign bow placeholder");
        visuals.bowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        visuals.drawAnimation = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Bow_Model_Draw.anim");
        visuals.holdAnimation = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Bow_Model_Hold.anim");
        visuals.releaseAnimation = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Bow_Model_Release.anim");
        EditorUtility.SetDirty(visuals);
    }

    private static AnimationClip FindClip(string path, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c =>
            !c.name.StartsWith("__preview__", StringComparison.Ordinal) &&
            (string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase) ||
             c.name.EndsWith("|" + name, StringComparison.OrdinalIgnoreCase)));

    private static AnimationClip CopyClip(AnimationClip source, string name)
    {
        string path = Folder + "/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = UnityEngine.Object.Instantiate(source);
            AssetDatabase.CreateAsset(clip, path);
        }
        else EditorUtility.CopySerialized(source, clip);
        clip.name = name;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(clip);
        return clip;
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        var parameter = controller.parameters.FirstOrDefault(p => p.name == name);
        Check(parameter == null || parameter.type == type, "Parameter type mismatch: " + name);
        if (parameter == null) controller.AddParameter(name, type);
    }

    private static void Configure(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.duration = 0f;
        transition.hasFixedDuration = true;
        transition.canTransitionToSelf = false;
        transition.interruptionSource = TransitionInterruptionSource.None;
    }

    [MenuItem("Tools/Combat/Validate Bow Timing")]
    public static void ValidateTimeline()
    {
        var timeline = new BowAttackTimeline();
        timeline.Begin();
        timeline.RequestRelease();
        timeline.Advance(22f, 70f);
        Check(timeline.Phase == BowAttackPhase.Draw && timeline.Frame == 33f, "Tap skipped the draw.");
        timeline.Advance(1f, 70f);
        Check(timeline.Phase == BowAttackPhase.Release && timeline.Frame == 35f, "Tap did not release at frame 35.");
        timeline.Advance(100f, 70f);
        Check(timeline.Phase == BowAttackPhase.Finished && timeline.Frame == 70f, "Release did not finish.");
        timeline.Begin();
        timeline.Advance(23f, 70f);
        Check(timeline.Phase == BowAttackPhase.Hold && timeline.Frame == 34f, "Hold did not start at frame 34.");
        timeline.Advance(600.5f, 70f);
        Check(timeline.Phase == BowAttackPhase.Hold && timeline.Frame >= 34f && timeline.Frame < 35f, "Hold escaped frames 34-35.");
        timeline.RequestRelease();
        timeline.Advance(0f, 70f);
        Check(timeline.Phase == BowAttackPhase.Release && timeline.Frame == 35f, "Held release did not start at frame 35.");
        timeline.Reset();
        Check(timeline.Phase == BowAttackPhase.None, "Reset left the bow held.");
        timeline.Begin();
        timeline.RequestRelease();
        timeline.Advance(100f, 70f);
        Check(timeline.Phase == BowAttackPhase.Finished, "Low frame rate prevented completion.");
        timeline.Begin();
        timeline.Advance(11.5f, 70f, 2f);
        Check(timeline.Phase == BowAttackPhase.Hold && timeline.Frame == 34f, "Double-speed draw did not halve draw time.");
        timeline.Begin();
        timeline.RequestRelease();
        timeline.Advance(20f, 70f, 2f);
        Check(timeline.Phase == BowAttackPhase.Release && timeline.Frame == 43.5f, "Draw speed changed release carry-over.");
        timeline.Advance(3f, 70f, 2f);
        Check(timeline.Frame == 46.5f, "Draw speed changed release playback.");
        Debug.Log("[Bow Validation] Timeline PASS, including 2x draw and unchanged release speed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Bow validation] " + message);
    }
}

public static class BowPlaceholderValidation
{
    [MenuItem("Tools/Combat/Validate Bow Scene Integration")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Validate outside Play Mode.");
        BowPlaceholderSetup.ValidateTimeline();
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Revamp/main_scene_test.unity");
        try
        {
            var player = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerStateManager>(true)).Single();
            var animator = player.anim;
            var visuals = player.GetComponent<PlayerBowVisuals>();
            Require(visuals != null && visuals.bowPrefab != null && visuals.drawAnimation != null &&
                visuals.holdAnimation != null && visuals.releaseAnimation != null, "Scene bow references are missing.");
            Require(animator != null && animator.isHuman, "Scene player needs a humanoid Animator.");
            player.bowAttackSpeed = 1f;
            player.bowDrawSpeed = 2f;
            player.logStateChanges = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);
            Invoke(player, "InitializeCombat");
            animator.Play("Base Layer.Normal", 0, 0f);
            animator.Update(0f);
            Require(player.TrySetCombatMode(PlayerCombatMode.Bow), "Bow equip request rejected.");
            TickAnimator(animator, 300);
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Attack.Bow"), "Equip did not reach bow locomotion.");
            Require(!player.IsChangingEquipment, "Equipment lock was not cleared.");
            int releases = 0;
            player.BowReleased += () => releases++;
            Require(player.TryAttack(CombatAttackInput.LightAttack), "Bow light attack rejected.");
            animator.Update(0f);
            Require(player.IsAttackMovementLocked, "Bow light attack did not lock movement.");
            Require(!player.TryAttack(CombatAttackInput.LightAttack), "Bow attack queued another shot.");
            Require(!player.TrySetCombatMode(PlayerCombatMode.Normal), "Equipment interrupted bow attack.");
            Require(!player.TryParry(), "Parry interrupted bow attack.");
            Advance(player, animator, 0.2f, false);
            Require(player.BowPhase == BowAttackPhase.Draw, "Tap skipped the draw.");
            Advance(player, animator, 0.6f, false);
            Require(player.BowPhase == BowAttackPhase.Release && releases == 1, "Tap did not release exactly once.");
            Advance(player, animator, 2f, false);
            TickAnimator(animator, 2);
            Require(!player.IsAttacking && !player.IsAttackMovementLocked, "Tap did not return to locomotion.");
            player.BeginLightAttackHold();
            animator.Update(0f);
            Advance(player, animator, 1f, true);
            Require(player.IsHoldingBow && releases == 1, "Held shot released too soon.");
            Advance(player, animator, 20f, true);
            Require(player.IsHoldingBow && player.BowAnimationFrame >= 34f && player.BowAnimationFrame < 35f,
                "Long hold escaped frames 34-35.");
            player.EndLightAttackHold();
            Advance(player, animator, 0f, false);
            Require(player.BowPhase == BowAttackPhase.Release && releases == 2, "Pointer-up did not release exactly once.");
            Advance(player, animator, 2f, false);
            TickAnimator(animator, 2);
            Require(!player.IsAttacking, "Held shot did not return to locomotion.");
            player.BeginLightAttackHold();
            animator.Update(0f);
            Advance(player, animator, 1f, true);
            Invoke(player, "DisableCombat");
            TickAnimator(animator, 2);
            Require(!player.IsAttacking && player.BowPhase == BowAttackPhase.None, "Disable left bow latched.");
            Require(player.TrySetCombatMode(PlayerCombatMode.Normal), "Bow unequip request rejected.");
            TickAnimator(animator, 300);
            Require(!player.IsChangingEquipment && player.CombatMode == PlayerCombatMode.Normal, "Bow unequip did not finish.");
            foreach (var clip in new[] { visuals.drawAnimation, visuals.holdAnimation, visuals.releaseAnimation })
            {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                Require(bindings.Length > 0, clip.name + " has no animation curves.");
                Require(bindings.All(b => string.IsNullOrEmpty(b.path) || visuals.bowPrefab.transform.Find(b.path) != null),
                    clip.name + " has bindings missing from the bow prefab.");
            }
            Debug.Log("[Bow Validation] PASS: equip, tap, long hold, release-once, movement/action locks, disable reset, unequip, and model clip bindings.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void TickAnimator(Animator animator, int frames)
    {
        for (int i = 0; i < frames; i++) animator.Update(1f / 60f);
    }

    private static void Advance(PlayerStateManager player, Animator animator, float seconds, bool held)
    {
        Invoke(player, "AdvanceBowAttack", seconds, held);
        animator.Update(0f);
    }

    private static void Invoke(PlayerStateManager player, string method, params object[] arguments) =>
        typeof(PlayerStateManager).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, arguments);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Bow Validation] " + message);
    }
}
