using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SoulsLike.Enemies.Editor
{
    public static class EnemyControllerBuilder
    {
        public static AnimatorController Build(EnemyProfile profile, string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            var used = new HashSet<string>();
            Set(machine, "Idle", profile.idle, 1, used);
            Set(machine, "Walk", profile.walk != null ? profile.walk : profile.idle, 1, used);
            Set(machine, "Run", profile.run != null ? profile.run : profile.walk, 1, used);
            Set(machine, "Hit", profile.hit, 1, used);
            Set(machine, "Die", profile.die, 1, used);
            foreach (var attack in profile.attacks)
            {
                if (attack == null || string.IsNullOrWhiteSpace(attack.stateName)) continue;
                if (used.Contains(attack.stateName)) throw new System.InvalidOperationException("Duplicate/reserved state name: " + attack.stateName);
                Set(machine, attack.stateName, attack.animation, attack.playbackSpeed, used);
            }
            machine.defaultState = Find(machine, "Idle");
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
            return controller;
        }
        private static AnimatorState Find(AnimatorStateMachine machine, string name)
        { foreach (var s in machine.states) if (s.state.name == name) return s.state; return null; }
        private static void Set(AnimatorStateMachine machine, string name, AnimationClip clip, float speed, HashSet<string> used)
        {
            var state = Find(machine, name) ?? machine.AddState(name, new Vector3(260 * (used.Count % 3), 90 * (used.Count / 3), 0));
            state.motion = clip; state.speed = speed; state.writeDefaultValues = true; used.Add(name);
        }
        [MenuItem("Tools/Enemy AI/Rebuild Selected Enemy Controller")]
        public static void RebuildSelected()
        {
            var brain = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<EnemyBrain>() : null;
            if (brain == null || brain.profile == null) { Debug.LogWarning("Select an enemy with an EnemyBrain and assigned profile."); return; }
            var driver = brain.GetComponent<EnemyAnimationDriver>();
            var controller = driver.animator.runtimeAnimatorController as AnimatorController;
            if (controller == null) { Debug.LogWarning("Assign a generated AnimatorController first."); return; }
            Build(brain.profile, AssetDatabase.GetAssetPath(controller));
        }
        [MenuItem("Tools/Enemy AI/Create Controller for Selected Profile")]
        public static void CreateForProfile()
        {
            var profile = Selection.activeObject as EnemyProfile;
            if (profile == null) { Debug.LogWarning("Select an EnemyProfile asset first."); return; }
            string path = AssetDatabase.GetAssetPath(profile);
            path = AssetDatabase.GenerateUniqueAssetPath(path.Substring(0, path.LastIndexOf('.')) + ".controller");
            Selection.activeObject = Build(profile, path);
        }
    }
    [CustomEditor(typeof(EnemyBrain))]
    public sealed class EnemyBrainInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var brain = (EnemyBrain)target;
            EditorGUILayout.HelpBox("Health is configured on the existing Enemy component. Open the profile for movement, detection and attack definitions. Placeholder clips do not animate the model. Replace clips and rebuild this enemy's controller.", MessageType.Info);
            if (brain.profile != null && GUILayout.Button("Select Enemy Profile")) Selection.activeObject = brain.profile;
            if (brain.profile != null && GUILayout.Button("Rebuild Controller from Profile"))
            {
                var driver = brain.GetComponent<EnemyAnimationDriver>();
                if (driver.animator != null && driver.animator.runtimeAnimatorController is AnimatorController controller)
                    EnemyControllerBuilder.Build(brain.profile, AssetDatabase.GetAssetPath(controller));
            }
        }
    }
}
