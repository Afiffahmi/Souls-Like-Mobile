using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nusantara.World.Editor
{
    public sealed class NusantaraWorldWindow : EditorWindow
    {
        const string Folder = "Assets/Scripts/World/Editor/";
        ObjectField definitionField, areaField;
        Label status;
        IEnumerator validation;
        WalkabilityReport report;
        [MenuItem("Tools/Nusantara/World Foundation")]
        public static void Open() => GetWindow<NusantaraWorldWindow>("Nusantara World");
        public void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder + "NusantaraWorldWindow.uxml");
            if (tree == null) return;
            tree.CloneTree(rootVisualElement);
            definitionField = rootVisualElement.Q<ObjectField>("definition");
            definitionField.objectType = typeof(WorldDefinition); definitionField.allowSceneObjects = false;
            definitionField.value = AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
            areaField = rootVisualElement.Q<ObjectField>("area");
            areaField.objectType = typeof(WorldWalkabilityArea); areaField.allowSceneObjects = true;
            status = rootVisualElement.Q<Label>("status");
            rootVisualElement.Q<Button>("setup").clicked += () =>
            {
                string result = WorldFoundationSetup.Create();
                definitionField.value = AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
                status.text = result;
            };
            rootVisualElement.Q<Button>("checkConfig").clicked += () =>
            {
                var world = definitionField.value as WorldDefinition;
                status.text = world == null ? "Assign a world definition." : string.Join("\n", world.ConfigurationErrors(true).DefaultIfEmpty("Configuration and reference readiness passed."));
            };
            rootVisualElement.Q<Button>("checks").clicked += () =>
            {
                try { status.text = WorldFoundationChecks.Run(); }
                catch (Exception error) { status.text = error.ToString(); UnityEngine.Debug.LogException(error); }
            };
            rootVisualElement.Q<Button>("validate").clicked += StartValidation;
            rootVisualElement.Q<Button>("cancel").clicked += () => { Cancel(); status.text = "Validation cancelled; report is incomplete."; };
            rootVisualElement.Q<Button>("report").clicked += () =>
            {
                if (report == null) { status.text = "Run a validation first."; return; }
                Directory.CreateDirectory("Library/Nusantara");
                File.WriteAllText("Library/Nusantara/WalkabilityReport.json", JsonUtility.ToJson(report, true));
                status.text = "Saved Library/Nusantara/WalkabilityReport.json. Finished: " + report.finished;
            };
            SceneView.duringSceneGui += DrawIssues;
        }
        void StartValidation()
        {
            Cancel();
            var world = definitionField.value as WorldDefinition;
            var area = areaField.value as WorldWalkabilityArea;
            if (world == null || area == null) { status.text = "Assign a world definition and a loaded WorldWalkabilityArea."; return; }
            report = new WalkabilityReport();
            validation = WorldWalkabilityValidator.Validate(world, area, report);
            EditorApplication.update += Advance;
        }
        void Advance()
        {
            try
            {
                var watch = Stopwatch.StartNew();
                while (validation != null && watch.ElapsedMilliseconds < 4)
                {
                    if (validation.MoveNext()) continue;
                    Cancel();
                    status.text = $"Finished: {report.sampledPoints:N0} samples; {report.issueCount:N0} issues. This sampling check does not replace controller playtesting.";
                    SceneView.RepaintAll();
                    return;
                }
                status.text = $"Checking: {report.sampledPoints:N0} samples; {report.issueCount:N0} issues.";
            }
            catch (Exception error) { Cancel(); status.text = error.Message; UnityEngine.Debug.LogException(error); }
        }
        void Cancel()
        {
            EditorApplication.update -= Advance;
            (validation as IDisposable)?.Dispose();
            validation = null;
        }
        void DrawIssues(SceneView view)
        {
            if (report == null) return;
            Handles.color = new Color(1, .3f, .1f);
            foreach (var issue in report.issues)
                Handles.SphereHandleCap(0, issue.position, Quaternion.identity, HandleUtility.GetHandleSize(issue.position) * .045f, EventType.Repaint);
        }
        void OnDisable() { Cancel(); SceneView.duringSceneGui -= DrawIssues; }
    }
}
