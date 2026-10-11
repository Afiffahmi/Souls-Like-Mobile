using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using SoulsLike.Riding;

public static class HorseWalkingLayerSetup
{
    public static void Configure(HorseAnimationSet set, Transform model)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before editing horse animations.");
        var controller = set.horseController as AnimatorController;
        if (!controller || !model) throw new InvalidOperationException("Horse controller and model are required.");
        var states = controller.layers[0].stateMachine.states;
        // Repair missing profile references from the actual controller assignments.
        foreach (var entry in set.animations)
        {
            var state = states.Select(s => s.state).FirstOrDefault(s => "Base Layer." + s.name == entry.horseState);
            if (!state || !(state.motion is AnimationClip clip)) continue;
            entry.horseControllerClip = clip;
            if (!entry.horseClip) entry.horseClip = clip;
        }
        var walk = set.Get(HorseMotion.Walk);
        if (walk == null || !walk.horseControllerClip) throw new InvalidOperationException("Assign a horse Walk clip first.");
        const string maskPath = HorseRidingSetup.Folder + "/HorseWalkingLegs.mask";
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (!mask)
        {
            mask = new AvatarMask { name = "HorseWalkingLegs" };
            AssetDatabase.CreateAsset(mask, maskPath);
        }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        var bones = model.GetComponentsInChildren<Transform>(true);
        mask.transformCount = bones.Length;
        for (int i = 0; i < mask.transformCount; i++)
        {
            var path = AnimationUtility.CalculateTransformPath(bones[i], model);
            mask.SetTransformPath(i, path);
            mask.SetTransformActive(i, path.Contains("/back_leg_pelvis_") || path.Contains("/front_scapula_"));
        }
        if (!controller.parameters.Any(p => p.name == HorseAnimationController.WalkTimeParameter))
            controller.AddParameter(HorseAnimationController.WalkTimeParameter, AnimatorControllerParameterType.Float);
        var layers = controller.layers;
        int index = Array.FindIndex(layers, l => l.name == HorseAnimationController.WalkingLayerName);
        if (index < 0)
        {
            controller.AddLayer(HorseAnimationController.WalkingLayerName);
            layers = controller.layers;
            index = layers.Length - 1;
        }
        var layer = layers[index];
        layer.avatarMask = mask;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        layer.defaultWeight = 0;
        var machine = layer.stateMachine;
        var gait = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "WalkCycle") ?? machine.AddState("WalkCycle");
        gait.motion = walk.horseControllerClip;
        gait.timeParameter = HorseAnimationController.WalkTimeParameter;
        gait.timeParameterActive = true;
        gait.writeDefaultValues = false;
        machine.defaultState = gait;
        layers[index] = layer;
        controller.layers = layers;
        EditorUtility.SetDirty(gait);
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(mask);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(set);
    }
}
