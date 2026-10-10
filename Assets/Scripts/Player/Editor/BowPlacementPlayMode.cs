using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicitly retain only bow tuning values, never runtime socket references.</summary>
[InitializeOnLoad]
public static class BowPlacementPlayMode
{
    private const string SessionKey = "BowPlacementPlayMode.Pending";

    [Serializable]
    private sealed class Placement
    {
        public string id;
        public Vector3 handPosition, handRotation, backPosition, backRotation, modelScale;
        public bool hasStringSettings;
        public Vector3 stringPosition;
        public float stringSize, stringLength;
    }

    [Serializable]
    private sealed class Placements
    {
        public List<Placement> items = new List<Placement>();
    }

    static BowPlacementPlayMode()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += RefreshPausedPlacement;
    }

    [MenuItem("CONTEXT/PlayerBowVisuals/Keep Bow Adjustments After Play Mode")]
    private static void Keep(MenuCommand command) => Capture((PlayerBowVisuals)command.context);

    public static void Capture(PlayerBowVisuals visuals)
    {
        if (!EditorApplication.isPlaying || visuals == null) return;
        var id = GlobalObjectId.GetGlobalObjectIdSlow(visuals);
        if (id.identifierType != 2 || id.targetObjectId == 0 ||
            string.IsNullOrEmpty(visuals.gameObject.scene.path))
        {
            Debug.LogWarning("[Bow] Keep adjustments requires a player in a saved scene that exists outside Play Mode.", visuals);
            return;
        }

        var pending = JsonUtility.FromJson<Placements>(SessionState.GetString(SessionKey, "{}"));
        pending.items.RemoveAll(item => item.id == id.ToString());
        var effects = visuals.GetComponent<ElementalGems.GemWeaponEffects>();
        pending.items.Add(new Placement
        {
            id = id.ToString(),
            handPosition = visuals.handPosition,
            handRotation = visuals.handRotation,
            backPosition = visuals.backPosition,
            backRotation = visuals.backRotation,
            modelScale = visuals.modelScale,
            hasStringSettings = effects != null,
            stringPosition = effects != null ? effects.bowAuraPosition : Vector3.zero,
            stringSize = effects != null ? effects.bowStringSize : 0f,
            stringLength = effects != null ? effects.bowStringLength : 0f
        });
        SessionState.SetString(SessionKey, JsonUtility.ToJson(pending));
        Debug.Log("[Bow] Current hand/back placement, scale and string settings captured. They will be restored when Play Mode stops. Run this command again if you make more adjustments.", visuals);
    }

    [MenuItem("CONTEXT/PlayerBowVisuals/Keep Bow Adjustments After Play Mode", true)]
    private static bool CanKeep(MenuCommand command) =>
        EditorApplication.isPlaying && command.context is PlayerBowVisuals;

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            SessionState.EraseString(SessionKey);
        if (state != PlayModeStateChange.EnteredEditMode) return;

        var json = SessionState.GetString(SessionKey, "");
        if (string.IsNullOrEmpty(json)) return;
        SessionState.EraseString(SessionKey);
        foreach (var item in JsonUtility.FromJson<Placements>(json).items)
        {
            if (!GlobalObjectId.TryParse(item.id, out var id)) continue;
            var visuals = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as PlayerBowVisuals;
            if (visuals == null)
            {
                Debug.LogWarning("[Bow] Could not restore adjustments: the original scene player was not found.");
                continue;
            }
            Undo.RecordObject(visuals, "Keep bow adjustments");
            visuals.handPosition = item.handPosition;
            visuals.handRotation = item.handRotation;
            visuals.backPosition = item.backPosition;
            visuals.backRotation = item.backRotation;
            visuals.modelScale = item.modelScale;
            ApplyInspectorPlacement(visuals);
            var effects = visuals.GetComponent<ElementalGems.GemWeaponEffects>();
            if (item.hasStringSettings && effects != null)
            {
                Undo.RecordObject(effects, "Keep bow string adjustments");
                effects.bowAuraPosition = item.stringPosition;
                effects.bowStringSize = item.stringSize;
                effects.bowStringLength = item.stringLength;
                PrefabUtility.RecordPrefabInstancePropertyModifications(effects);
                EditorUtility.SetDirty(effects);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(visuals);
            EditorUtility.SetDirty(visuals);
            EditorSceneManager.MarkSceneDirty(visuals.gameObject.scene);
            Debug.Log("[Bow] Bow placement, scale and string settings restored. Save the scene to keep them on disk.", visuals);
        }
    }

    private static void RefreshPausedPlacement()
    {
        if (!EditorApplication.isPlaying || !EditorApplication.isPaused) return;
        foreach (var visuals in UnityEngine.Object.FindObjectsByType<PlayerBowVisuals>(FindObjectsSortMode.None))
            if (visuals.isActiveAndEnabled)
            {
                visuals.RefreshPlacement();
                visuals.GetComponent<ElementalGems.GemWeaponEffects>()?.RefreshBowPlacement();
            }
        SceneView.RepaintAll();
    }

    public static void ApplyInspectorPlacement(PlayerBowVisuals visuals)
    {
        if (!EditorApplication.isPlaying)
        {
            if (visuals.handSocket != null) Undo.RecordObject(visuals.handSocket, "Adjust bow hand placement");
            if (visuals.backSocket != null) Undo.RecordObject(visuals.backSocket, "Adjust bow back placement");
        }
        visuals.RefreshPlacement();
        visuals.GetComponent<ElementalGems.GemWeaponEffects>()?.RefreshBowPlacement();
        if (!EditorApplication.isPlaying)
        {
            foreach (var socket in new[] { visuals.handSocket, visuals.backSocket })
            {
                if (socket == null) continue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
                EditorUtility.SetDirty(socket);
            }
            if (visuals.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(visuals.gameObject.scene);
        }
        SceneView.RepaintAll();
    }
}

[CustomEditor(typeof(PlayerBowVisuals))]
public sealed class PlayerBowVisualsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        bool placementChanged = DrawDefaultInspector();
        var visuals = (PlayerBowVisuals)target;
        if (placementChanged) BowPlacementPlayMode.ApplyInspectorPlacement(visuals);
        var effects = visuals.GetComponent<ElementalGems.GemWeaponEffects>();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Bow String Effect", EditorStyles.boldLabel);
        if (effects != null)
        {
            // Edit the existing settings directly, so both component inspectors stay in sync.
            using (var settings = new SerializedObject(effects))
            {
                settings.Update();
                EditorGUILayout.PropertyField(settings.FindProperty("bowAuraPosition"),
                    new GUIContent("String Position", "X / Y / Z offset in the bow model's local axes. Moves both string halves together."));
                EditorGUILayout.PropertyField(settings.FindProperty("bowStringSize"), new GUIContent("String Size"));
                EditorGUILayout.PropertyField(settings.FindProperty("bowStringLength"), new GUIContent("String Length"));
                if (settings.ApplyModifiedProperties() && EditorApplication.isPlaying)
                {
                    visuals.RefreshPlacement();
                    effects.RefreshBowPlacement();
                    SceneView.RepaintAll();
                }
            }
        }
        else EditorGUILayout.HelpBox("String effect controls require Gem Weapon Effects on this player.", MessageType.Info);

        if (EditorApplication.isPlaying && GUILayout.Button("Keep Bow Adjustments After Play Mode"))
            BowPlacementPlayMode.Capture(visuals);
    }
}
