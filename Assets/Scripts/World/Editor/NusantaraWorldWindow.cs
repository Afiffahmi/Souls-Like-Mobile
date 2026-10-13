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
            rootVisualElement.Clear();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder + "NusantaraWorldWindow.uxml");
            if (tree == null) return;
            tree.CloneTree(rootVisualElement);
            definitionField = rootVisualElement.Q<ObjectField>("definition");
            definitionField.objectType = typeof(WorldDefinition); definitionField.allowSceneObjects = false;
            definitionField.value = AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
            areaField = rootVisualElement.Q<ObjectField>("area");
            areaField.objectType = typeof(WorldWalkabilityArea); areaField.allowSceneObjects = true;
            status = rootVisualElement.Q<Label>("status");
            void Navigate(Func<string> action)
            {try{status.text=action();}catch(Exception error){status.text=error.Message;}}
            rootVisualElement.Q<Button>("show-world").clicked+=()=>Navigate(WorldSceneBookmarks.ShowWholeWorld);
            var navigation=rootVisualElement.Q<VisualElement>("region-navigation");
            var definition=definitionField.value as WorldDefinition;
            var gridToggle=rootVisualElement.Q<Toggle>("region-grid");gridToggle.SetValueWithoutNotify(WorldRegionAuthoringGrid.Enabled);
            gridToggle.RegisterValueChangedCallback(e=>WorldRegionAuthoringGrid.Enabled=e.newValue);
            if(definition!=null)
            {
                int cells=definition.regions.Sum(r=>r.chunkGridSize.x*r.chunkGridSize.y);
                rootVisualElement.Q<Label>("region-size-summary").text=$"{definition.regions.Length} independent regions · {cells} cells total. Each cell covers {definition.chunkSize:0} m × {definition.chunkSize:0} m.";
            }
            if(definition!=null)foreach(var region in definition.regions.OrderBy(r=>r.id))
            {
                int id=region.id;
                var button=new Button(()=>Navigate(()=>WorldSceneBookmarks.ShowRegionForEditing(id))){name="edit-region-"+id,text=$"Edit Region {id} · {region.chunkGridSize.x}×{region.chunkGridSize.y} · {region.boss}"};
                button.AddToClassList("world-region-button");navigation.Add(button);
            }
            rootVisualElement.Q<Button>("terrain-brush").clicked+=WorldTerrainBrushWindow.Open;
            rootVisualElement.Q<Button>("vegetation-brush").clicked+=VegetationPainterWindow.Open;
            rootVisualElement.Q<Button>("water-brush").clicked+=WorldWaterBrushWindow.Open;
            rootVisualElement.Q<Button>("setup").clicked += () =>
            {
                string result = WorldFoundationSetup.Create();
                definitionField.value = AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
                status.text = result;
            };
            rootVisualElement.Q<Button>("checkConfig").clicked += () =>
            {
                var world = definitionField.value as WorldDefinition;
                status.text = world == null ? "Assign a world definition." : string.Join("\n", world.ConfigurationErrors(true).DefaultIfEmpty("World configuration passed."));
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
            SceneView.duringSceneGui -= DrawIssues;
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
