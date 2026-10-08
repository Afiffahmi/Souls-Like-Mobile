using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

[CustomEditor(typeof(RollConfiguration))]
public sealed class RollConfigurationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var config = (RollConfiguration)target;
        bool valid = config.Validate(out var error);
        EditorGUILayout.HelpBox(valid ? "Distance, duration, recovery and grounded requirement apply to new rolls. After replacing clips, apply to update the Animator. Movement uses the CharacterController; no root motion or invulnerability." : error, valid ? MessageType.Info : MessageType.Error);
        using (new EditorGUI.DisabledScope(!valid || EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Apply Roll Clips to Animator"))
                try { RollAnimatorBuilder.Apply(config); }
                catch (Exception exception) { Debug.LogException(exception, config); }
    }
}

public static class RollAnimatorBuilder
{
    public static void Apply(RollConfiguration config)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply outside Play Mode.");
        if (!config.Validate(out var error)) throw new ArgumentException(error);
        var c = config.animatorController as AnimatorController;
        if (c == null) throw new ArgumentException("Assign the base Animator Controller.");
        Ensure(c,"Roll",AnimatorControllerParameterType.Trigger);
        Ensure(c,"RollPlaybackSpeed",AnimatorControllerParameterType.Float);
        Ensure(c,"RollX",AnimatorControllerParameterType.Float);
        Ensure(c,"RollY",AnimatorControllerParameterType.Float);
        var root=c.layers[0].stateMachine;
        var normal=root.stateMachines.Single(s=>s.stateMachine.name=="Normal").stateMachine;
        var attack=root.stateMachines.Single(s=>s.stateMachine.name=="Attack").stateMachine;
        foreach(var definition in config.rolls)
        {
            var parent=definition.mode==PlayerCombatMode.Normal?normal:attack;
            string name=definition.mode+"_Roll";
            var state=parent.states.FirstOrDefault(s=>s.state.name==name).state;
            if(state==null)state=parent.AddState(name,new Vector3(1250,100+(int)definition.mode*160));
            else if(!state.behaviours.OfType<CombatRollState>().Any(b=>b.configuration==config))throw new InvalidOperationException("Roll state owned by another configuration: "+name);
            state.motion=BuildMotion(c, state, definition);state.speed=1f;state.speedParameter="RollPlaybackSpeed";state.speedParameterActive=true;
            state.tag="CombatRoll";state.writeDefaultValues=false;
            var behaviour=state.behaviours.OfType<CombatRollState>().FirstOrDefault()??state.AddStateMachineBehaviour<CombatRollState>();
            behaviour.configuration=config;behaviour.mode=definition.mode;
            var destination=definition.mode==PlayerCombatMode.Normal?normal.states.Single(s=>s.state.name=="Locomotion").state:attack.states.Single(s=>s.state.name==definition.mode.ToString()).state;
            var sources=definition.mode==PlayerCombatMode.Normal?normal.states.Where(s=>s.state.name=="Idle"||s.state.name=="Locomotion").Select(s=>s.state).ToArray():new[]{destination};
            foreach(var source in sources)
            {
                foreach(var old in source.transitions.Where(t=>t.name=="RollConfig/Entry").ToArray())source.RemoveTransition(old);
                var entry=source.AddTransition(state);Setup(entry,false);entry.name="RollConfig/Entry";
                entry.AddCondition(AnimatorConditionMode.If,0,"Roll");entry.AddCondition(AnimatorConditionMode.Equals,(int)definition.mode,"CombatMode");
                source.transitions=new[]{entry}.Concat(source.transitions.Where(t=>t!=entry)).ToArray();
            }
            foreach(var old in state.transitions.ToArray())state.RemoveTransition(old);
            var back=state.AddTransition(destination);Setup(back,true);back.name="Finish roll to same mode";
        }
        foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(c)))EditorUtility.SetDirty(obj);
        AssetDatabase.SaveAssetIfDirty(c);EditorUtility.SetDirty(config);AssetDatabase.SaveAssetIfDirty(config);
    }
    static Motion BuildMotion(AnimatorController controller, AnimatorState state, RollDefinition definition)
    {
        if (!definition.UsesDirectionalAnimations) return definition.animation;
        var reference = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)).OfType<BlendTree>().Single(t => t.name == "Walk_8Directions");
        string name = definition.mode + "_Roll_8Directions";
        var tree = state.motion as BlendTree;
        if (tree != null && tree.name != name) throw new InvalidOperationException("Unexpected roll tree: " + tree.name);
        if (tree == null)
        {
            tree = new BlendTree { name = name };
            AssetDatabase.AddObjectToAsset(tree, controller);
        }
        tree.blendType = reference.blendType;
        tree.blendParameter = "RollX"; tree.blendParameterY = "RollY"; tree.useAutomaticThresholds = false;
        var clips = definition.directionalAnimations.Clips;
        var children = new ChildMotion[clips.Length];
        for (int i=0;i<clips.Length;i++)
        {
            var child = reference.children.Single(ch => ch.position == RollDirectionalAnimations.Positions[i]);
            child.motion = clips[i]; child.timeScale = clips[i].length; child.cycleOffset = 0f; child.mirror = false;
            children[i] = child;
        }
        tree.children = children;
        return tree;
    }
    static void Ensure(AnimatorController c,string name,AnimatorControllerParameterType type)
    {
        var existing=c.parameters.FirstOrDefault(p=>p.name==name);
        if(existing!=null&&existing.type!=type)throw new InvalidOperationException("Parameter mismatch: "+name);
        if(existing==null)c.AddParameter(name,type);
    }
    static void Setup(AnimatorStateTransition t,bool exit)
    {
        t.hasExitTime=exit;t.exitTime=1f;t.duration=0;t.hasFixedDuration=true;t.canTransitionToSelf=false;t.interruptionSource=TransitionInterruptionSource.None;
    }
}
