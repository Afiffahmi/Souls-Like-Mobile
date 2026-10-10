using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerStateManager))]
public sealed class PlayerStateManagerEditor : Editor
{
    bool sword=true,bow,magic,other;
    public override void OnInspectorGUI()
    {
        var player=(PlayerStateManager)target;
        if(!serializedObject.FindProperty("attackDefaultsInitialized").boolValue) {
            Undo.RecordObject(player,"Initialize default attack tuning");player.InitializeAttackDefaults();EditorUtility.SetDirty(player);
        }
        if(!serializedObject.FindProperty("bowHeavyTargetingInitialized").boolValue && player.GetComponent<PlayerBowShooter>() != null) {
            Undo.RecordObject(player,"Move bow heavy targeting settings");
            player.InitializeBowHeavyTargeting();
            EditorUtility.SetDirty(player);
            if(PrefabUtility.IsPartOfPrefabInstance(player))PrefabUtility.RecordPrefabInstancePropertyModifications(player);
        }
        serializedObject.Update();
        EditorGUILayout.HelpBox("Default combat balance lives here. Equipment upgrades and accessories apply their own bonuses afterward. Changes affect the next attack; configuration assets keep clips, hit frames and combo routing.",MessageType.Info);
        sword=EditorGUILayout.Foldout(sword,"SWORD — default attacks",true);
        if(sword){Weapon("swordDefaults",false,false);
            Fields("swordComboWindupSpeed","swordLightRecoverySpeed","swordHeavyBaseKnockback","swordHeavyLowDamageMultiplier","swordHeavyHighDamageMultiplier","swordHeavyLowPushMultiplier","swordHeavyHighPushMultiplier","swordHeavyHeldLowPasses");}
        bow=EditorGUILayout.Foldout(bow,"BOW — default attacks",true);
        if(bow){Weapon("bowDefaults",true,false);Fields("bowDefaultDrawSpeed");
            EditorGUILayout.HelpBox("Light and Heavy charge times are divided by their attack Speed and equipment SPD from upgrades and accessories. Special auto-fires charged low arrows at frames 17, 27 and 35, then a piercing force wave at 63. Low arrows spread across live enemies within Special Range (3 enemies: 1 each; 2: 2/1; 1: all 3). Unlocked low shots turn the player toward their target. High aims at the current lock-on target, or fires along the player's facing when unlocked. Damage and speed use Special equipment bonuses. Area width scales with Bow upgrade level.",MessageType.None);}
        magic=EditorGUILayout.Foldout(magic,"MAGIC — defaults for future spells",true);
        if(magic){Weapon("magicDefaults",false,true);EditorGUILayout.HelpBox("These defaults are available to future spells through CaptureAttackDefaults. No magic casting implementation exists yet.",MessageType.Info);}
        other=EditorGUILayout.Foldout(other,"Movement, equipment and other player settings",true);
        if(other)DrawPropertiesExcluding(serializedObject,"m_Script","attackDefaultsInitialized","bowHeavyTargetingInitialized","bowHeavyTargeting","bowSpecial","swordDefaults","bowDefaults","magicDefaults",
            "swordComboWindupSpeed","swordLightRecoverySpeed","swordHeavyBaseKnockback","swordHeavyLowDamageMultiplier","swordHeavyHighDamageMultiplier",
            "swordHeavyLowPushMultiplier","swordHeavyHighPushMultiplier","swordHeavyHeldLowPasses","swordHeavyChargeSecondsPerStage","bowLightChargeSeconds","bowDefaultDrawSpeed","bowAttackSpeed","bowDrawSpeed");
        if(serializedObject.ApplyModifiedProperties()){
            if(PrefabUtility.IsPartOfPrefabInstance(player))PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            var ui=Object.FindFirstObjectByType<WeaponEquipmentUI>();if(ui!=null && ui.equipment==player.GetComponent<PlayerWeaponEquipment>())ui.Refresh();
        }
    }
    void Weapon(string name,bool bow,bool magic)
    {
        var weapon=serializedObject.FindProperty(name);
        foreach(var kind in new[]{"light","heavy","special"}){
            var attack=weapon.FindPropertyRelative(kind);
            attack.isExpanded=EditorGUILayout.Foldout(attack.isExpanded,ObjectNames.NicifyVariableName(kind)+" attack",true);
            if(!attack.isExpanded)continue;
            EditorGUI.indentLevel++;
            foreach(var field in new[]{"damage","speed","knockbackDurationScale","recoverySeconds"})EditorGUILayout.PropertyField(attack.FindPropertyRelative(field));
            if(magic || (bow && kind=="heavy"))EditorGUILayout.PropertyField(attack.FindPropertyRelative("castSeconds"),new GUIContent("Base Cast / Charge Seconds"));
            if(bow && kind=="light")EditorGUILayout.PropertyField(serializedObject.FindProperty("bowLightChargeSeconds"),new GUIContent("Base Charge Seconds"));
            if(!bow && !magic && kind=="heavy") {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("swordHeavyChargeSecondsPerStage"),new GUIContent("Base Seconds Per Charge Tier"));
                EditorGUILayout.HelpBox("Charge tiers occur at 1x, 2x and 3x this time, divided by Heavy Speed and equipment SPD from upgrades and accessories. Tier two adds one loop; tier three adds two. Hold stays at frame 12 until release.",MessageType.None);
            }
            if(bow && kind=="heavy") {
                var targeting=serializedObject.FindProperty("bowHeavyTargeting");
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Ground targeting",EditorStyles.boldLabel);
                foreach(var field in new[]{"attackRadius","impactRadius","groundField"})
                    EditorGUILayout.PropertyField(targeting.FindPropertyRelative(field));
                EditorGUILayout.LabelField("Charged volley scatter",EditorStyles.boldLabel);
                foreach(var field in new[]{"minOffset","maxOffset","enemyClearance","pointSeparation"})
                    EditorGUILayout.PropertyField(targeting.FindPropertyRelative(field));
                EditorGUILayout.LabelField("Ground detection",EditorStyles.boldLabel);
                foreach(var field in new[]{"groundLayers","groundProbeHeight","groundProbeDepth"})
                    EditorGUILayout.PropertyField(targeting.FindPropertyRelative(field));
            }
            if(bow && kind=="special")EditorGUILayout.PropertyField(serializedObject.FindProperty("bowSpecial"),true);
            if(kind=="special")EditorGUILayout.PropertyField(attack.FindPropertyRelative("cooldownSeconds"));
            EditorGUI.indentLevel--;
        }
    }
    void Fields(params string[] names){foreach(var name in names)EditorGUILayout.PropertyField(serializedObject.FindProperty(name));}
}
