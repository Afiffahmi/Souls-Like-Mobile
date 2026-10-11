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
            bool hasPhaseSpeed = false;
            foreach (var parameter in controller.parameters)
                if (parameter.name == EnemyAnimationDriver.AttackPhaseSpeedParameter)
                {
                    if (parameter.type != AnimatorControllerParameterType.Float)
                        throw new System.InvalidOperationException("EnemyAttackPhaseSpeed must be a float parameter.");
                    hasPhaseSpeed = true;
                }
            if (!hasPhaseSpeed) controller.AddParameter(new AnimatorControllerParameter {
                name = EnemyAnimationDriver.AttackPhaseSpeedParameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var machine = controller.layers[0].stateMachine;
            var used = new HashSet<string>();
            Set(machine, "Idle", profile.idle, 1, used);
            Set(machine, "Walk", profile.walk != null ? profile.walk : profile.idle, 1, used);
            Set(machine, "Run", profile.run != null ? profile.run : profile.walk, 1, used);
            Set(machine, "Hit", profile.hit, 1, used);
            Set(machine, "Die", profile.die, 1, used);
            Set(machine, "ResetPosition", profile.resetPositionAnimation, profile.resetPositionPlaybackSpeed, used);
            Set(machine, "Spawn", profile.spawnAnimation, profile.spawnPlaybackSpeed, used);
            var definitions = new Dictionary<string, EnemyAttackDefinition>();
            foreach (var attack in profile.attacks)
            {
                AddAttack(machine, attack, used, definitions);
                if (attack != null && attack.followUpSteps != null)
                    foreach (var step in attack.followUpSteps) AddAttack(machine, step, used, definitions);
            }
            machine.defaultState = Find(machine, profile.spawnAnimation != null ? "Spawn" : "Idle");
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
            return controller;
        }
        private static AnimatorState Find(AnimatorStateMachine machine, string name)
        { foreach (var s in machine.states) if (s.state.name == name) return s.state; return null; }
        private static void AddAttack(AnimatorStateMachine machine, EnemyAttackDefinition attack, HashSet<string> used,
            Dictionary<string, EnemyAttackDefinition> definitions)
        {
            if (attack == null || string.IsNullOrWhiteSpace(attack.stateName)) return;
            if (definitions.TryGetValue(attack.stateName, out var existing) && existing == attack) return;
            if (used.Contains(attack.stateName)) throw new System.InvalidOperationException("Duplicate/reserved state name: " + attack.stateName);
            definitions.Add(attack.stateName, attack);
            // Attack speed comes from the runtime parameter, so Inspector edits apply immediately.
            Set(machine, attack.stateName, attack.animation, 1f, used);
            var state = Find(machine, attack.stateName);
            state.speedParameter = EnemyAnimationDriver.AttackPhaseSpeedParameter;
            state.speedParameterActive = true;
        }
        private static void Set(AnimatorStateMachine machine, string name, AnimationClip clip, float speed, HashSet<string> used)
        {
            var state = Find(machine, name) ?? machine.AddState(name, new Vector3(260 * (used.Count % 3), 90 * (used.Count / 3), 0));
            state.motion = clip; state.speed = speed; state.writeDefaultValues = true; used.Add(name);
            state.speedParameterActive = false;
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
        private bool showSpecialDebug = true;
        private bool showHealth = true, showProfile = true, showAttacks = true;
        private readonly Dictionary<Object, SerializedObject> inlineObjects = new Dictionary<Object, SerializedObject>();
        private readonly HashSet<EnemyAttackDefinition> drawnAttacks = new HashSet<EnemyAttackDefinition>();
        private readonly HashSet<EnemyAttackDefinition> expandedAttacks = new HashSet<EnemyAttackDefinition>();
        private SerializedObject Inline(Object asset)
        {
            if (!inlineObjects.TryGetValue(asset, out var view))
            { view = new SerializedObject(asset); inlineObjects.Add(asset, view); }
            view.Update(); return view;
        }
        private void OnDisable()
        {
            foreach (var view in inlineObjects.Values) view.Dispose();
            inlineObjects.Clear();
        }
        private void DrawConfiguration(EnemyBrain brain)
        {
            showHealth = EditorGUILayout.Foldout(showHealth, "Health (this enemy)", true);
            var health = brain.GetComponent<Enemy>();
            if (showHealth && health != null)
            {
                var view = Inline(health);
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(view.FindProperty("maxHealth"), new GUIContent("Max Health"));
                    if (EditorGUI.EndChangeCheck())
                    {
                        int hp = Mathf.Max(1, view.FindProperty("maxHealth").intValue);
                        view.FindProperty("maxHealth").intValue = hp;
                        view.FindProperty("currentHealth").intValue = hp;
                    }
                }
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(view.FindProperty("currentHealth"), new GUIContent("Current Health"));
                if (view.ApplyModifiedProperties() && PrefabUtility.IsPartOfPrefabInstance(health))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(health);
                if (Application.isPlaying) EditorGUILayout.HelpBox("Set Max Health outside Play mode. Current Health shows live damage taken.", MessageType.None);
            }
            if (brain.profile == null) return;
            var profileView = Inline(brain.profile);
            showProfile = EditorGUILayout.Foldout(showProfile, "Enemy Profile Settings", true);
            if (showProfile)
            {
                EditorGUILayout.HelpBox("These fields edit the assigned profile asset. Enemies using the same profile share these settings. Health Loss Special Interval is HP lost before returning home and using a special.", MessageType.None);
                DrawPropertiesExcluding(profileView, "m_Script", "attacks");
            }
            showAttacks = EditorGUILayout.Foldout(showAttacks, "Attack Damage and Settings", true);
            if (showAttacks)
            {
                EditorGUILayout.HelpBox("Damage is outgoing damage dealt to the player. These edits update the attack assets shared by enemies using them. Expand an attack for timing, cooldown, animation, movement and follow-ups. A summon-only step deals no direct damage; its soldiers use their own Attack1/Attack2 assets.", MessageType.None);
                EditorGUILayout.PropertyField(profileView.FindProperty("attacks"), new GUIContent("Available Attack Assets"), true);
            }
            profileView.ApplyModifiedProperties();
            if (!showAttacks) return;
            drawnAttacks.Clear();
            if (brain.profile.attacks == null) return;
            foreach (var attack in brain.profile.attacks) DrawAttack(attack, false);
        }
        private void DrawAttack(EnemyAttackDefinition attack, bool followUp)
        {
            if (attack == null || !drawnAttacks.Add(attack)) return;
            var view = Inline(attack);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            bool expanded = EditorGUILayout.Foldout(expandedAttacks.Contains(attack),
                (followUp ? "Follow-up: " : "") + attack.name + (attack.available ? "" : " (disabled)"), true);
            if (expanded) expandedAttacks.Add(attack); else expandedAttacks.Remove(attack);
            EditorGUILayout.PropertyField(view.FindProperty("dealsDamage"));
            using (new EditorGUI.DisabledScope(!view.FindProperty("dealsDamage").boolValue && !(attack.animationAction is EnemyGroundSlamAction)))
                EditorGUILayout.PropertyField(view.FindProperty("damage"), new GUIContent("Damage to Player"));
            if (expanded)
            {
                EditorGUILayout.HelpBox("Playback Speed changes the whole clip. Jump Speed Multiplier changes only the movement frames; Windup Speed Multiplier also applies before impact. Cue Frame starts the red/yellow warning and optional protection until animation completion. Yellow attacks can still be interrupted by a successful parry. Apply Element To Player enables gem changes/reactions; leave it off for normal attacks.", MessageType.None);
                DrawPropertiesExcluding(view, "m_Script", "dealsDamage", "damage");
            }
            view.ApplyModifiedProperties();
            if (expanded && attack.animationAction is EnemySummonArmyAction summon)
            {
                EditorGUILayout.LabelField("Summon Army Settings", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Spawn Delay Frames spaces soldiers within this same cast. Each soldier inherits the summoner's Innate Element once at spawn, overriding the army prefab's element. Temporary changes to the boss never affect this choice or existing soldiers. Each soldier then uses its OWN elemental state, aura and reaction timers.", MessageType.None);
                var summonView = Inline(summon);
                DrawPropertiesExcluding(summonView, "m_Script");
                summonView.ApplyModifiedProperties();
            }
            else if (expanded && attack.animationAction != null)
            {
                EditorGUILayout.LabelField("Animation Action Settings", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Ground Slam uses Damage to Player above. Action Frame triggers the impact once; Movement Start/End Frame controls the jump. These action settings are shared by attacks using this asset.", MessageType.None);
                var actionView = Inline(attack.animationAction);
                DrawPropertiesExcluding(actionView, "m_Script");
                actionView.ApplyModifiedProperties();
            }
            EditorGUILayout.EndVertical();
            if (attack.followUpSteps != null)
                foreach (var step in attack.followUpSteps) DrawAttack(step, true);
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "debugRepeatSpecialAttacks", "debugBypassHealthMilestone", "debugIgnoreSpecialCooldown", "debugSpecialRepeatDelay", "debugSpecialAttacks");
            var brain = (EnemyBrain)target;
            showSpecialDebug = EditorGUILayout.Foldout(showSpecialDebug, "Enemy Special Attack Debug (this enemy only)", true);
            if (showSpecialDebug)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("debugRepeatSpecialAttacks"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("debugBypassHealthMilestone"), new GUIContent("Bypass Health Milestone"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("debugIgnoreSpecialCooldown"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("debugSpecialRepeatDelay"));
                var choices = serializedObject.FindProperty("debugSpecialAttacks");
                bool anyChecked = false;
                for (int i = 0; i < choices.arraySize; i++)
                {
                    var row = choices.GetArrayElementAtIndex(i);
                    anyChecked |= row.FindPropertyRelative("play").boolValue;
                    var attack = row.FindPropertyRelative("attack");
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(row.FindPropertyRelative("play"), GUIContent.none, GUILayout.Width(18));
                    EditorGUILayout.PropertyField(attack, new GUIContent("Special " + (i + 1)));
                    EditorGUILayout.EndHorizontal();
                    var definition = attack.objectReferenceValue as EnemyAttackDefinition;
                    if (row.FindPropertyRelative("play").boolValue && (definition == null || !definition.IsUsable))
                        EditorGUILayout.HelpBox("Assign a non-looping animation, enable Available, configure its action/movement frames, then rebuild the controller.", MessageType.Warning);
                }
                if (anyChecked && !serializedObject.FindProperty("debugRepeatSpecialAttacks").boolValue)
                    EditorGUILayout.HelpBox("Debug selection is OFF. Normal milestones randomly choose from all configured specials. Enable Debug Repeat Special Attacks to restrict the choice to the checked slots.", MessageType.Info);
                if (brain.profile != null && GUILayout.Button("Load Special Slots from Profile"))
                    foreach (var attack in brain.profile.attacks)
                    {
                        if (attack == null || attack.trigger != EnemyAttackTrigger.HealthLossSpecial) continue;
                        bool exists = false;
                        for (int i = 0; i < choices.arraySize; i++)
                            if (choices.GetArrayElementAtIndex(i).FindPropertyRelative("attack").objectReferenceValue == attack) exists = true;
                        if (exists) continue;
                        int index = choices.arraySize++;
                        choices.GetArrayElementAtIndex(index).FindPropertyRelative("play").boolValue = false;
                        choices.GetArrayElementAtIndex(index).FindPropertyRelative("attack").objectReferenceValue = attack;
                    }
                EditorGUILayout.HelpBox("Tick Special1 only to choose it at every health milestone. With Bypass Health Milestone OFF: lose HP, return to spawn, then use a checked special. Turn bypass ON only for immediate repeated playback. The five-soldier limit always applies. Release builds ignore debug settings.", MessageType.None);
            }
            serializedObject.ApplyModifiedProperties();
            DrawConfiguration(brain);
            EditorGUILayout.HelpBox("After changing animation clips or adding attack steps, rebuild this enemy's controller below.", MessageType.Info);
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
