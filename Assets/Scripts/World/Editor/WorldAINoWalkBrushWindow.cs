using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Nusantara.World.Editor
{
    public sealed class WorldAINoWalkBrushWindow : EditorWindow
    {
        [SerializeField] float radius = 8;
        [SerializeField] bool erase;
        bool painting, stroke, hasLast;
        int control, undoGroup;
        Vector3 last;
        Toggle brushToggle;
        HelpBox status;
        const string RootName = "AI No-Walk Areas";

        [MenuItem("Tools/Nusantara/AI No-Walk Brush")]
        public static void Open()
        {
            var window = GetWindow<WorldAINoWalkBrushWindow>("AI No-Walk Brush");
            window.minSize = new Vector2(360, 440); window.Show();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Add(new HelpBox("Paint red areas on the ground where enemies and NPCs must not walk.", HelpBoxMessageType.Info));
            root.Add(new Button(() =>
            {
                EndStroke();
                try { status.text = WorldAINavigationEditor.BuildGround(); }
                catch (System.Exception error) { status.text = error.Message; Debug.LogException(error); }
            }) { text = "1. Build AI Walking Ground" });
            brushToggle = new Toggle("Enable brush") { value = painting };
            brushToggle.RegisterValueChangedCallback(e =>
            {
                EndStroke(); painting = e.newValue;
                if (painting)
                    foreach (var terrainTool in Resources.FindObjectsOfTypeAll<WorldTerrainBrushWindow>()) terrainTool.DisablePainting();
                if (painting) foreach (var roadTool in Resources.FindObjectsOfTypeAll<WorldRoadBrushWindow>()) roadTool.DisablePainting(); if(painting)foreach(var regionTool in Resources.FindObjectsOfTypeAll<WorldRegionBrushWindow>())regionTool.DisablePainting();
                if (painting) foreach (var waterTool in Resources.FindObjectsOfTypeAll<WorldWaterBrushWindow>()) waterTool.DisablePainting();
                SceneView.RepaintAll();
            });
            root.Add(brushToggle);
            var mode = new DropdownField("Action", new System.Collections.Generic.List<string> { "Paint blocked area", "Erase painted areas" }, erase ? 1 : 0);
            mode.RegisterValueChangedCallback(e => { EndStroke(); erase = mode.index == 1; }); root.Add(mode);
            var size = new Slider("Brush radius (metres)", .5f, 64) { value = radius, showInputField = true };
            size.RegisterValueChangedCallback(e => radius = Mathf.Clamp(e.newValue, .5f, 64)); root.Add(size);
            root.Add(new HelpBox("Drag on ground to paint. Hold Shift to erase. Alt + mouse still moves the camera. Esc turns the brush off. Ctrl+Z undoes a whole stroke. Erase removes touched round patches.", HelpBoxMessageType.None));
            root.Add(new HelpBox("AI agents avoid painted areas during Play. No colliders are added. Zones affect navigation above and below the painted ground, so take care around stacked floors and bridges.", HelpBoxMessageType.None));
            root.Add(new Button(() =>
            {
                EndStroke(); var scene = SceneManager.GetActiveScene();
                if (scene.IsValid() && !EditorApplication.isPlaying)
                    status.text = EditorSceneManager.SaveScene(scene) ? "Scene and painted areas saved." : "Scene save cancelled.";
            }) { text = "Save Scene and Painted Areas" });
            status = new HelpBox("Build walking ground, enable the brush, then drag in Scene view.", HelpBoxMessageType.None); root.Add(status);
        }
        void OnEnable()
        {
            SceneView.duringSceneGui += SceneGUI;
            Undo.undoRedoPerformed += RepaintViews;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }
        void OnDisable()
        {
            EndStroke(); painting = false;
            SceneView.duringSceneGui -= SceneGUI;
            Undo.undoRedoPerformed -= RepaintViews;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            SceneView.RepaintAll();
        }
        void RepaintViews() => SceneView.RepaintAll();
        public void DisablePainting() { EndStroke(); painting = false; brushToggle?.SetValueWithoutNotify(false); }
        void OnPlayMode(PlayModeStateChange state) { EndStroke(); painting = false; brushToggle?.SetValueWithoutNotify(false); }
        void EndStroke()
        {
            if (stroke) Undo.CollapseUndoOperations(undoGroup);
            if (control != 0 && GUIUtility.hotControl == control) GUIUtility.hotControl = 0;
            stroke = false; hasLast = false; control = 0;
        }
        static WorldAINoWalkZone[] Zones(Scene scene) => Object.FindObjectsByType<WorldAINoWalkZone>(FindObjectsInactive.Include)
            .Where(z => z.gameObject.scene == scene).ToArray();

        public static bool PaintAt(Scene scene, Vector3 point, float brushRadius, bool erasing)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) return false;
            var zones = Zones(scene);
            if (erasing)
            {
                bool changed = false;
                foreach (var zone in zones)
                {
                    var delta = zone.transform.position - point;
                    if (Mathf.Abs(delta.y) > Mathf.Max(12, brushRadius * 2)) continue;
                    delta.y = 0;
                    if (delta.magnitude > brushRadius + zone.radius) continue;
                    Undo.DestroyObjectImmediate(zone.gameObject); changed = true;
                }
                if (changed) EditorSceneManager.MarkSceneDirty(scene);
                return changed;
            }
            brushRadius = Mathf.Clamp(brushRadius, .5f, 64);
            foreach (var zone in zones)
                if (zone.isActiveAndEnabled && Vector3.Distance(zone.transform.position, point) + brushRadius <= zone.radius * 1.05f) return false;
            if (zones.Length >= 2000) return false;
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (root == null)
            {
                root = new GameObject(RootName); SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Paint AI no-walk area");
            }
            var go = new GameObject("Blocked AI area"); SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetParent(root.transform, true); go.transform.position = point;
            var marker = go.AddComponent<WorldAINoWalkZone>(); marker.radius = brushRadius; marker.RefreshObstacle();
            Undo.RegisterCreatedObjectUndo(go, "Paint AI no-walk area");
            EditorSceneManager.MarkSceneDirty(scene); return true;
        }

        void SceneGUI(SceneView view)
        {
            var scene = SceneManager.GetActiveScene();
            var e = Event.current;
            if (e.type == EventType.Repaint)
                foreach (var zone in Zones(scene))
                {
                    if (!zone.isActiveAndEnabled) continue;
                    Handles.color = new Color(1, .12f, .12f, .15f);
                    Handles.DrawSolidDisc(zone.transform.position + Vector3.up * .12f, Vector3.up, zone.radius);
                    Handles.color = new Color(1, .2f, .15f, .8f);
                    Handles.DrawWireDisc(zone.transform.position + Vector3.up * .14f, Vector3.up, zone.radius);
                }
            if (!painting || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            { EndStroke(); painting = false; brushToggle.SetValueWithoutNotify(false); e.Use(); return; }
            int id = GUIUtility.GetControlID("NusantaraAINoWalkBrush".GetHashCode(), FocusType.Passive);
            if (e.type == EventType.MouseUp && e.button == 0 && stroke) { EndStroke(); e.Use(); return; }
            if (e.alt) { if (stroke) EndStroke(); return; }
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var hits = Physics.RaycastAll(ray, 100000, ~0, QueryTriggerInteraction.Ignore);
            var valid = hits.Where(h => h.collider.gameObject.scene == scene && !SceneVisibilityManager.instance.IsHidden(h.collider.gameObject) && h.normal.y > .35f && WorldAINavigation.IsGround(h.collider))
                .OrderBy(h => h.distance).ToArray();
            if (valid.Length == 0) { hasLast = false; return; }
            var hit = valid[0]; bool erasing = erase || e.shift;
            Handles.color = erasing ? new Color(.3f, 1, .55f) : new Color(1, .22f, .15f);
            Handles.DrawWireDisc(hit.point + Vector3.up * .15f, Vector3.up, radius);
            if (e.type == EventType.MouseMove) view.Repaint();
            if (e.type == EventType.MouseDown && e.button == 0 && !e.control && !e.command)
            {
                Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(erasing ? "Erase AI no-walk areas" : "Paint AI no-walk areas");
                stroke = true; hasLast = false; control = id; GUIUtility.hotControl = id;
            }
            else if (e.type != EventType.MouseDrag || !stroke || e.button != 0) return;
            float spacing = Mathf.Max(.25f, radius * .45f);
            if (!hasLast || Vector3.Distance(last, hit.point) >= spacing)
            {
                if (hasLast)
                {
                    int count = Mathf.Min(256, Mathf.CeilToInt(Vector3.Distance(last, hit.point) / spacing));
                    for (int i = 1; i <= count; i++)
                    {
                        var p = Vector3.Lerp(last, hit.point, (float)i / count);
                        var groundHits = Physics.RaycastAll(p + Vector3.up * 128, Vector3.down, 256, ~0, QueryTriggerInteraction.Ignore)
                            .Where(h => h.collider.gameObject.scene == scene && WorldAINavigation.IsGround(h.collider)).OrderBy(h => h.distance).ToArray();
                        if (groundHits.Length > 0) PaintAt(scene, groundHits[0].point, radius, erasing);
                    }
                }
                else PaintAt(scene, hit.point, radius, erasing);
                last = hit.point; hasLast = true;
                int total = Zones(scene).Length;
                status.text = total >= 2000 ? "Paint limit reached. Erase unused patches or use a larger brush." : "Painted patches: " + total + ". Save the scene when finished.";
            }
            e.Use(); view.Repaint();
        }
    }
}
