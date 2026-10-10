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
    private static void Keep(MenuCommand command)
    {
        var visuals = (PlayerBowVisuals)command.context;
        var id = GlobalObjectId.GetGlobalObjectIdSlow(visuals);
        if (id.identifierType != 2 || id.targetObjectId == 0 ||
            string.IsNullOrEmpty(visuals.gameObject.scene.path))
        {
            Debug.LogWarning("[Bow] Keep adjustments requires a player in a saved scene that exists outside Play Mode.", visuals);
            return;
        }

        var pending = JsonUtility.FromJson<Placements>(SessionState.GetString(SessionKey, "{}"));
        pending.items.RemoveAll(item => item.id == id.ToString());
        pending.items.Add(new Placement
        {
            id = id.ToString(),
            handPosition = visuals.handPosition,
            handRotation = visuals.handRotation,
            backPosition = visuals.backPosition,
            backRotation = visuals.backRotation,
            modelScale = visuals.modelScale
        });
        SessionState.SetString(SessionKey, JsonUtility.ToJson(pending));
        Debug.Log("[Bow] Current hand/back offsets and scale captured. They will be restored when Play Mode stops. Run this command again if you make more adjustments.", visuals);
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
            PrefabUtility.RecordPrefabInstancePropertyModifications(visuals);
            EditorUtility.SetDirty(visuals);
            EditorSceneManager.MarkSceneDirty(visuals.gameObject.scene);
            Debug.Log("[Bow] Hand/back offsets and scale restored. Save the scene to keep them on disk.", visuals);
        }
    }

    private static void RefreshPausedPlacement()
    {
        if (!EditorApplication.isPlaying || !EditorApplication.isPaused) return;
        foreach (var visuals in UnityEngine.Object.FindObjectsByType<PlayerBowVisuals>(FindObjectsSortMode.None))
            if (visuals.isActiveAndEnabled) visuals.RefreshPlacement();
        SceneView.RepaintAll();
    }
}
