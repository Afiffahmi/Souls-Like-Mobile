using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Nusantara.World.Editor
{
    [CustomEditor(typeof(WorldAINavigation))]
    public sealed class WorldAINavigationEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("Manage AI walking areas with the brush. Loaded ground connects automatically; no chunk coordinates or edge arrays are needed.", HelpBoxMessageType.Info));
            root.Add(new Button(WorldAINoWalkBrushWindow.Open) { text = "Open AI No-Walk Brush" });
            return root;
        }
        public static string BuildGround()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Exit Play mode before building walking ground.";
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path)) return "Save the scene once before building AI walking ground.";
            WorldAINavigation nav = null;
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<WorldAINavigation>())
                if (candidate.gameObject.scene == scene) { nav = candidate; break; }
            if (nav == null)
            {
                var go = new GameObject("AI Walking Ground"); SceneManager.MoveGameObjectToScene(go, scene);
                nav = go.AddComponent<WorldAINavigation>(); Undo.RegisterCreatedObjectUndo(go, "Set up AI walking ground");
            }
            nav.world = AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
            Physics.SyncTransforms();
            var sources = nav.CollectGround(out var bounds);
            if (sources.Count == 0) return "Load a terrain area first; no walking ground was found.";
            var data = NavMeshBuilder.BuildNavMeshData(nav.Settings(), sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) throw new InvalidOperationException("AI walking ground could not be built.");
            const string folder = "Assets/NusantaraWorld/AINavigation";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/NusantaraWorld", "AINavigation");
            string path = folder + "/" + AssetDatabase.AssetPathToGUID(scene.path) + ".asset";
            var stored = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (stored != null) { EditorUtility.CopySerialized(data, stored); UnityEngine.Object.DestroyImmediate(data); EditorUtility.SetDirty(stored); AssetDatabase.SaveAssetIfDirty(stored); }
            else { stored = data; AssetDatabase.CreateAsset(stored, path); }
            Undo.RecordObject(nav, "Build AI walking ground"); nav.walkingGround = stored; nav.ReloadGround();
            EditorUtility.SetDirty(nav); EditorSceneManager.MarkSceneDirty(scene);
            return "Walking ground built. Paint blocked areas, then save the scene. Loaded terrain updates automatically during Play.";
        }
    }
}
