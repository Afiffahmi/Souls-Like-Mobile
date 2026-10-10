using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class BowSpecialAttackSetup
{
    const string ControllerPath = "Assets/Animation/PlayerLocomotion/PlayerLocomotion.controller";
    const string ClipPath = "Assets/Animation/PlayerLocomotion/Combat/Bow/Attacks/Bow_Skill_9.anim";
    const string Request = "Temp/BowSpecialSetup.request";
    const string Result = "Temp/BowSpecialSetup.result";

    // One explicit setup request after script import; never enters Play Mode or runs tests.
    [InitializeOnLoadMethod]
    static void OnReload() => EditorApplication.delayCall += ProcessRequest;
    static void ProcessRequest()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        { EditorApplication.delayCall += ProcessRequest; return; }
        File.Delete(Request);
        try { Apply(); File.WriteAllText(Result, "Bow special setup completed. No gameplay tests were run."); }
        catch (Exception e) { File.WriteAllText(Result, e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Tools/Combat/Set Up Bow Special Attack")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before setting up bow special.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (controller == null || clip == null || clip.frameRate <= 0 || clip.length * clip.frameRate <= 63 || clip.isLooping)
            throw new InvalidOperationException("Bow_Skill_9 must be non-looping and contain frame 63.");
        var machine = controller.layers[0].stateMachine.stateMachines.Single(s => s.stateMachine.name == "Attack").stateMachine;
        Undo.RegisterCompleteObjectUndo(controller, "Set up bow special attack");
        Undo.RegisterCompleteObjectUndo(machine, "Set up bow special attack");
        var parameter = controller.parameters.FirstOrDefault(p => p.name == PlayerStateManager.BowSpecialTimeParameter);
        if (parameter != null && parameter.type != AnimatorControllerParameterType.Float) throw new InvalidOperationException("BowSpecialTime must be Float.");
        if (parameter == null) controller.AddParameter(PlayerStateManager.BowSpecialTimeParameter, AnimatorControllerParameterType.Float);
        for (int i = 0; i < 2; i++)
        {
            string name = i == 0 ? "Bow_SpecialAttack_Low" : "Bow_SpecialAttack_High";
            var state = machine.states.FirstOrDefault(s => s.state.name == name).state;
            if (state == null) state = machine.AddState(name, new Vector3(1150 + i * 320, 1050));
            else if (!state.behaviours.OfType<CombatBowSpecialAttackState>().Any()) throw new InvalidOperationException(name + " is owned by another behaviour.");
            Undo.RegisterCompleteObjectUndo(state, "Set up bow special state");
            state.motion = clip; state.speed = 0; state.speedParameterActive = false;
            state.timeParameter = PlayerStateManager.BowSpecialTimeParameter; state.timeParameterActive = true;
            state.tag = "CombatAttack"; state.writeDefaultValues = false;
            var behaviour = state.behaviours.OfType<CombatBowSpecialAttackState>().FirstOrDefault() ?? state.AddStateMachineBehaviour<CombatBowSpecialAttackState>();
            behaviour.animation = clip; behaviour.highDamage = i == 1;
            EditorUtility.SetDirty(behaviour); EditorUtility.SetDirty(state);
        }
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shader/ElementalGems/BowSpecialEnergy.shader");
        if (shader == null) throw new InvalidOperationException("Bow special energy shader is missing.");
        const string materialPath = "Assets/Materials/ElementalGems/Resources/BowSpecialEnergy.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) { material = new Material(shader) { name = "BowSpecialEnergy" }; AssetDatabase.CreateAsset(material, materialPath); }
        EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller); AssetDatabase.SaveAssetIfDirty(material);
        Debug.Log("Bow special ready: low arrows 17/27/35, high wave 63. Tune Player State Manager > BOW > Special attack.");
    }
}
