using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Nusantara.World.Editor
{
    [CustomEditor(typeof(WorldDayNightCycle))]
    public sealed class WorldDayNightCycleEditor : UnityEditor.Editor
    {
        const string StylePath = "Assets/Scripts/World/Editor/WorldDayNightCycleEditor.uss";
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement(); root.AddToClassList("day-night-inspector");
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (style != null) root.styleSheets.Add(style);
            var clock = (WorldDayNightCycle)target;
            root.Add(new HelpBox("Day: 06:00–18:00. Night: 18:00–06:00. Enemy eye VFX appears only at night. Test buttons pause the clock; Resume starts it again.", HelpBoxMessageType.Info));
            var status = new Label(); status.AddToClassList("clock-status"); root.Add(status);
            var buttons = new VisualElement(); buttons.AddToClassList("clock-buttons");
            buttons.Add(new Button(() => Change(clock, () => clock.TestDay())) {text = "Test Day"});
            buttons.Add(new Button(() => Change(clock, () => clock.TestNight())) {text = "Test Night"});
            var pause = new Button(() => Change(clock, () => clock.runCycle = !clock.runCycle)); buttons.Add(pause); root.Add(buttons);
            var hour = new Slider("Test hour", 0, 24) {showInputField = true};
            hour.RegisterValueChangedCallback(e => Change(clock, () => clock.SetTestHour(e.newValue))); root.Add(hour);
            root.Add(new PropertyField(serializedObject.FindProperty("dayMinutes"), "Day duration (real minutes)"));
            root.Add(new PropertyField(serializedObject.FindProperty("nightMinutes"), "Night duration (real minutes)"));
            root.Add(new PropertyField(serializedObject.FindProperty("startingHour"), "Starting hour"));
            root.Add(new PropertyField(serializedObject.FindProperty("runCycle"), "Run cycle"));
            root.Add(new PropertyField(serializedObject.FindProperty("useUnscaledTime"), "Advance while paused"));
            var lighting = new Foldout {text = "Lighting", value = false};
            var property = serializedObject.GetIterator(); bool children = true;
            string[] controls = {"m_Script", "dayMinutes", "nightMinutes", "startingHour", "runCycle", "useUnscaledTime", "previewHour"};
            while (property.NextVisible(children))
            {
                children = false;
                if (!controls.Contains(property.name)) lighting.Add(new PropertyField(property.Copy()));
            }
            root.Add(lighting);
            void UpdateStatus()
            {
                if (clock == null) return;
                float current = Application.isPlaying ? clock.CurrentHour : clock.startingHour;
                int minutes = Mathf.FloorToInt(Mathf.Repeat(current, 24) * 60);
                bool day = current >= 6 && current < 18;
                status.text = $"{minutes / 60:00}:{minutes % 60:00} — {(day ? "Day" : "Night")} — {(clock.runCycle ? "Running" : "Paused")}";
                pause.text = clock.runCycle ? "Pause" : "Resume";
                hour.SetValueWithoutNotify(current);
            }
            UpdateStatus(); root.schedule.Execute(UpdateStatus).Every(250);
            return root;
        }
        static void Change(WorldDayNightCycle clock, Action action)
        {
            Undo.RecordObject(clock, "Test day/night clock"); action();
            EditorUtility.SetDirty(clock);
            if (PrefabUtility.IsPartOfPrefabInstance(clock)) PrefabUtility.RecordPrefabInstancePropertyModifications(clock);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(clock.gameObject.scene);
            SceneView.RepaintAll();
        }
        [MenuItem("Tools/Nusantara/Environment/Select Day Night Controls")]
        public static WorldDayNightCycle SelectControls()
        {
            var scene = SceneManager.GetActiveScene();
            var clock = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WorldDayNightCycle>(true)).FirstOrDefault();
            if (clock == null)
            {
                if (Application.isPlaying) throw new InvalidOperationException("Create the world clock in Edit mode first.");
                var root = new GameObject("Day Night Controller"); Undo.RegisterCreatedObjectUndo(root, "Create day/night clock");
                clock = Undo.AddComponent<WorldDayNightCycle>(root);
                clock.sun = RenderSettings.sun;
                if (clock.sun == null) clock.sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional && l.gameObject.scene == scene);
                var moon = new GameObject("Moonlight"); Undo.RegisterCreatedObjectUndo(moon, "Create moonlight"); moon.transform.SetParent(root.transform, false);
                clock.moon = Undo.AddComponent<Light>(moon); clock.moon.type = LightType.Directional; clock.moon.intensity = 0;
                clock.moon.color = new Color(.57f, .68f, 1); clock.moon.shadows = LightShadows.Hard;
                clock.skyMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Rendering/WorldCloudSky.mat");
                EditorUtility.SetDirty(clock); EditorSceneManager.MarkSceneDirty(scene);
            }
            Selection.activeGameObject = clock.gameObject; EditorGUIUtility.PingObject(clock.gameObject);
            return clock;
        }
    }
}
