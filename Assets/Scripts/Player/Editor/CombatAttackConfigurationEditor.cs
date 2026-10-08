using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

[CustomEditor(typeof(CombatAttackConfiguration))]
public sealed class CombatAttackConfigurationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var config = (CombatAttackConfiguration)target;
        bool valid = config.Validate(out string error);
        EditorGUILayout.HelpBox(valid
            ? "Edit the chain lists, clips, speeds and normalized timing windows above, then apply to update the generated Animator states. Empty chains disable their input. Special never chains."
            : error, valid ? MessageType.Info : MessageType.Error);
        using (new EditorGUI.DisabledScope(!valid || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Apply Configuration to Animator"))
            {
                try { CombatAttackAnimatorBuilder.Apply(config); Debug.Log("Applied attack configuration: " + config.name, config); }
                catch (Exception exception) { Debug.LogException(exception, config); }
            }
        }
    }
}

public static class CombatAttackAnimatorBuilder
{
    public static void Apply(CombatAttackConfiguration config)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply attack configuration outside Play Mode.");
        if (!config.Validate(out string error)) throw new ArgumentException(error);
        var controller = config.animatorController as AnimatorController;
        if (controller == null) throw new ArgumentException("Assign the base AnimatorController asset.");
        var path = config.combatStateMachinePath.Split('.');
        var layer = controller.layers.Single(l => l.name == path[0]);
        var parent = layer.stateMachine;
        for (int i=1;i<path.Length;i++) parent = parent.stateMachines.Single(s=>s.stateMachine.name==path[i]).stateMachine;
        var locomotion = parent.states.Single(s=>s.state.name==config.weapon.ToString()).state;
        var group = parent.stateMachines.FirstOrDefault(s=>s.stateMachine.name==config.AttackMachineName).stateMachine;
        if (group != null && group.states.Any(s=> !s.state.behaviours.OfType<CombatAttackState>().Any(b=>b.configuration==config)))
            throw new InvalidOperationException("The attack group contains states owned by another configuration. Use a separate group.");
        foreach (var input in (CombatAttackInput[])Enum.GetValues(typeof(CombatAttackInput)))
            EnsureParameter(controller, input.ToString(), AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "AttackBuffered", AnimatorControllerParameterType.Bool);
        Undo.RegisterCompleteObjectUndo(controller, "Apply attack configuration");
        Undo.RegisterCompleteObjectUndo(locomotion, "Apply attack configuration");
        if (group == null) group = parent.AddStateMachine(config.AttackMachineName, new Vector3(920,100));
        var originalTrees = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)).OfType<BlendTree>().ToDictionary(t=>t.name, EditorJsonUtility.ToJson);
        // Only the generated group and its named locomotion entry transitions are managed here.
        foreach (var t in locomotion.transitions.ToArray())
            if (t.name.StartsWith("AttackConfig/") && t.destinationState != null && group.states.Any(s=>s.state==t.destinationState))
                locomotion.RemoveTransition(t);
        foreach (var s in group.states)
            foreach (var t in s.state.transitions.ToArray()) s.state.RemoveTransition(t);
        var required = new HashSet<string>();
        var entries = new List<AnimatorStateTransition>();
        int row=0;
        foreach (var input in (CombatAttackInput[])Enum.GetValues(typeof(CombatAttackInput)))
        {
            var chain = config.Chain(input);
            int count = input==CombatAttackInput.SpecialAttack ? 1 : chain.Count;
            var states = new List<AnimatorState>();
            for(int i=0;i<count;i++)
            {
                string stateName=config.StateName(input,i); required.Add(stateName);
                var state=group.states.FirstOrDefault(s=>s.state.name==stateName).state;
                if(state==null) state=group.AddState(stateName,new Vector3(250+i*310,80+row*180));
                var b=state.behaviours.OfType<CombatAttackState>().FirstOrDefault();
                if(b==null) b=state.AddStateMachineBehaviour<CombatAttackState>();
                b.configuration=config; b.input=input; b.stepIndex=i;
                state.tag="CombatAttack"; state.writeDefaultValues=false;
                state.motion=input==CombatAttackInput.SpecialAttack?config.specialAttack.animation:chain[i].animation;
                state.speed=input==CombatAttackInput.SpecialAttack?config.specialAttack.playbackSpeed:chain[i].playbackSpeed;
                states.Add(state);
            }
            if(count>0)
            {
                var entry=locomotion.AddTransition(states[0]); Configure(entry,false,0);
                entry.name="AttackConfig/"+input;
                entry.AddCondition(AnimatorConditionMode.If,0,input.ToString());
                entry.AddCondition(AnimatorConditionMode.Equals,(int)config.weapon,"CombatMode");
                entries.Add(entry);
            }
            for(int i=0;i<count;i++)
            {
                if(i+1<count)
                {
                    var next=states[i].AddTransition(states[i+1]); Configure(next,true,chain[i].chainTransitionTime);
                    next.name="Buffered follow-up";
                    next.AddCondition(AnimatorConditionMode.If,0,"AttackBuffered");
                }
                var back=states[i].AddTransition(locomotion); Configure(back,true,1f);
                back.name="Finish and reset chain";
            }
            row++;
        }
        foreach(var state in group.states.ToArray()) if(!required.Contains(state.state.name)) group.RemoveState(state.state);
        locomotion.transitions=entries.Concat(locomotion.transitions.Where(t=>!entries.Contains(t))).ToArray();
        var arranged=group.states;
        for(int i=0;i<arranged.Length;i++)
        {
            var b=arranged[i].state.behaviours.OfType<CombatAttackState>().Single();
            arranged[i].position=new Vector3(250+b.stepIndex*310,80+(int)b.input*180);
        }
        group.states=arranged;
        group.defaultState=group.states.FirstOrDefault().state;
        group.entryPosition=new Vector3(20,80);
        foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller))) EditorUtility.SetDirty(obj);
        AssetDatabase.SaveAssetIfDirty(controller);
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        if(!AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)).OfType<BlendTree>().All(t=>originalTrees[t.name]==EditorJsonUtility.ToJson(t)))
            throw new InvalidOperationException("Unexpected locomotion tree modification.");
    }

    static void EnsureParameter(AnimatorController controller,string name,AnimatorControllerParameterType type)
    {
        var existing=controller.parameters.FirstOrDefault(p=>p.name==name);
        if(existing!=null && existing.type!=type) throw new InvalidOperationException("Parameter type mismatch: "+name);
        if(existing==null) controller.AddParameter(name,type);
    }
    static void Configure(AnimatorStateTransition t,bool exit,float time)
    {
        t.hasExitTime=exit; t.exitTime=time; t.duration=0f; t.hasFixedDuration=true;
        t.canTransitionToSelf=false; t.interruptionSource=TransitionInterruptionSource.None;
    }
}
