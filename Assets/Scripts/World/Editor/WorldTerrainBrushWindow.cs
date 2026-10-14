using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nusantara.World.Editor
{
    public sealed class WorldTerrainBrushWindow : EditorWindow
    {
        enum BrushMode { Raise, Lower, Uneven, Smooth, Flatten }
        [SerializeField] WorldGeographyProfile profile;
        [SerializeField] BrushMode mode;
        [SerializeField] float radius = 32, strength = .75f, hardness = .15f, amplitude = 2, featureSize = 48, blend = .35f;
        [SerializeField] int seed = 501;
        [SerializeField] bool painting, protect = true;
        string status = "Enable the brush, then drag on the ground in Scene view.";
        bool stroke, segment;
        int undoGroup, hotControl;
        float flattenHeight;
        Vector2 lastStamp;
        readonly HashSet<Mesh> recordedMeshes = new HashSet<Mesh>();
        readonly HashSet<Vector2Int> changedChunks = new HashSet<Vector2Int>();
        readonly Dictionary<Vector2Int, float> strokeStart = new Dictionary<Vector2Int, float>();
        Vector2 scroll;

        [MenuItem("Tools/Nusantara/Terrain Brush")]
        public static void Open()
        {
            var window = GetWindow<WorldTerrainBrushWindow>("Terrain Brush");
            window.minSize = new Vector2(360, 540);
            window.Show();
        }
        void OnEnable()
        {
            if (profile == null) profile = AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            SceneView.duringSceneGui += SceneGUI;
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.playModeStateChanged += OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload += EndStroke;
        }
        void OnDisable()
        {
            EndStroke(); painting = false;
            SceneView.duringSceneGui -= SceneGUI;
            Undo.undoRedoPerformed -= OnUndo;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload -= EndStroke;
            SceneView.RepaintAll();
        }
        void OnPlayMode(PlayModeStateChange state)
        {
            EndStroke(); painting = false;
            if (state == PlayModeStateChange.ExitingEditMode && profile != null && profile.sculptData != null && profile.sculptData.pendingChunks.Count > 0)
                Save();
            Repaint();
        }
        void OnUndo()
        {
            EndStroke();
            if (profile != null && profile.sculptData != null) profile.sculptData.InvalidateCache();
            WorldTerrainSculptAssets.InvalidateCache();
            WorldTerrainSculptAssets.RefreshColliders();
            status = "Undo / Redo applied. Save Terrain when finished.";
            SceneView.RepaintAll(); Repaint();
        }
        public void DisablePainting() { EndStroke(); painting = false; Repaint(); }
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(10);
            GUILayout.Label("LAND SCULPTING", EditorStyles.boldLabel);
            GUILayout.Label("Shape your world directly in Scene view.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(10);
            using (new EditorGUI.DisabledScope(stroke || painting))
                profile = (WorldGeographyProfile)EditorGUILayout.ObjectField("World geography", profile, typeof(WorldGeographyProfile), false);
            if (profile == null || profile.world == null)
            {
                EditorGUILayout.HelpBox("Assign the world's geography profile to begin.", MessageType.Info);
                EditorGUILayout.EndScrollView(); return;
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.Space(8);
                var oldColor = GUI.backgroundColor;
                GUI.backgroundColor = painting ? new Color(.48f, .83f, .59f) : oldColor;
                bool enabled = GUILayout.Toggle(painting, painting ? "BRUSH ON — drag in Scene view" : "Enable Terrain Brush", "Button", GUILayout.Height(34));
                GUI.backgroundColor = oldColor;
                if (enabled != painting)
                {
                    EndStroke();
                    if (enabled) foreach (var roadTool in Resources.FindObjectsOfTypeAll<WorldRoadBrushWindow>()) roadTool.DisablePainting(); if(enabled)foreach(var regionTool in Resources.FindObjectsOfTypeAll<WorldRegionBrushWindow>())regionTool.DisablePainting();
                    if (enabled) foreach (var aiTool in Resources.FindObjectsOfTypeAll<WorldAINoWalkBrushWindow>()) aiTool.DisablePainting();
                    if (enabled) foreach (var waterTool in Resources.FindObjectsOfTypeAll<WorldWaterBrushWindow>()) waterTool.DisablePainting();
                    try { if (enabled) WorldTerrainSculptAssets.Prepare(profile); painting = enabled; }
                    catch (Exception error) { painting = false; status = error.Message; Debug.LogException(error); }
                    SceneView.RepaintAll();
                }
                EditorGUILayout.Space(10);
                EditorGUI.BeginChangeCheck();
                mode = (BrushMode)GUILayout.SelectionGrid((int)mode, new[] { "Raise", "Lower", "Uneven", "Smooth", "Flatten" }, 3, GUILayout.Height(52));
                if (EditorGUI.EndChangeCheck()) { EndStroke(); SceneView.RepaintAll(); }
                EditorGUILayout.Space(6);
                string description = mode == BrushMode.Uneven ? "Natural land: adds small hills and hollows with a stable noise pattern."
                    : mode == BrushMode.Smooth ? "Clean up rough ground while preserving its overall shape."
                    : mode == BrushMode.Flatten ? "Level the brush area toward the height where your stroke starts."
                    : "Clean round brush with a soft edge. Shift reverses raise / lower.";
                EditorGUILayout.HelpBox(description, MessageType.None);
                float grid = profile.sculptData != null ? profile.sculptData.spacing : profile.world.chunkSize / Mathf.Max(1, profile.terrainIntervals);
                radius = EditorGUILayout.Slider("Radius (m)", radius, Mathf.Max(2, grid * 1.5f), 160);
                hardness = EditorGUILayout.Slider("Hardness", hardness, 0, .9f);
                if (mode == BrushMode.Raise || mode == BrushMode.Lower)
                    strength = EditorGUILayout.Slider("Height per dab (m)", strength, .05f, 5);
                else
                    blend = EditorGUILayout.Slider("Strength", blend, .02f, 1);
                if (mode == BrushMode.Uneven)
                {
                    amplitude = EditorGUILayout.Slider("Variation height (m)", amplitude, .1f, 8);
                    featureSize = EditorGUILayout.Slider("Feature size (m)", featureSize, grid * 4, 200);
                    seed = EditorGUILayout.IntField("Pattern seed", seed);
                }
                EditorGUILayout.Space(6);
                protect = EditorGUILayout.ToggleLeft("Protect roads and buildings", protect);
                EditorGUILayout.HelpBox("Ground detail spacing: " + grid.ToString("0.#") + " m. Existing trees and props stay at their placed heights.", MessageType.None);
                EditorGUILayout.Space(8);
                if (GUILayout.Button("Save Terrain", GUILayout.Height(30))) Save();
                int pending = profile.sculptData != null ? profile.sculptData.pendingChunks.Count : 0;
                GUILayout.Label(pending == 0 ? "All terrain changes saved." : pending + " chunk(s) need saving.", EditorStyles.miniLabel);
            }
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("Drag: paint  •  Shift: reverse raise / lower\nCtrl: smooth  •  [ / ]: brush size\nCtrl+Z: undo  •  Esc: disable brush\nAlt + mouse: orbit the scene", MessageType.Info);
            EditorGUILayout.HelpBox(status, MessageType.None);
            EditorGUILayout.EndScrollView();
        }
        void Save()
        {
            EndStroke();
            try { status = WorldTerrainSculptAssets.Save(profile); }
            catch (Exception error)
            {
                status = "Save needs attention: " + error.Message;
                Debug.LogException(error);
                if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
            }
            Repaint();
        }
        bool Ground(Ray ray, out RaycastHit hit)
        {
            hit = default; float nearest = float.MaxValue;
            foreach (var candidate in Physics.RaycastAll(ray, 20000, ~0, QueryTriggerInteraction.Ignore))
            {
                if (candidate.distance >= nearest || !(candidate.collider is MeshCollider collider) || !WorldTerrainSculptAssets.TryCoordinate(collider.sharedMesh, out _)) continue;
                if (SceneVisibilityManager.instance.IsHidden(collider.gameObject)) continue;
                hit = candidate; nearest = candidate.distance;
            }
            return nearest < float.MaxValue;
        }
        void SceneGUI(SceneView view)
        {
            var e = Event.current;
            if (stroke && (e.rawType == EventType.MouseUp || e.type == EventType.Ignore || e.type == EventType.MouseLeaveWindow)) EndStroke();
            if (!painting || profile == null || profile.sculptData == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape) { EndStroke(); painting = false; e.Use(); Repaint(); return; }
                if (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket)
                {
                    radius = Mathf.Clamp(radius * (e.keyCode == KeyCode.LeftBracket ? .85f : 1.15f), profile.sculptData.spacing * 1.5f, 160);
                    e.Use(); Repaint(); view.Repaint(); return;
                }
            }
            if (e.alt || Tools.viewToolActive) { if (stroke) EndStroke(); return; }
            int control = GUIUtility.GetControlID("NusantaraTerrainBrush".GetHashCode(), FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);
            if (!Ground(HandleUtility.GUIPointToWorldRay(e.mousePosition), out var hit)) { segment = false; return; }
            BrushMode active = e.control ? BrushMode.Smooth : mode;
            if (e.shift && active == BrushMode.Raise) active = BrushMode.Lower;
            else if (e.shift && active == BrushMode.Lower) active = BrushMode.Raise;
            Handles.color = active == BrushMode.Lower ? new Color(1, .58f, .30f) : active == BrushMode.Uneven ? new Color(.8f, .68f, 1) : new Color(.4f, 1, .65f);
            Handles.DrawWireDisc(hit.point + Vector3.up * .1f, Vector3.up, radius);
            Handles.DrawWireDisc(hit.point + Vector3.up * .1f, Vector3.up, radius * hardness);
            Handles.Label(hit.point + Vector3.up, active + "  •  " + radius.ToString("0") + " m");
            if (e.type == EventType.MouseMove) view.Repaint();
            if (e.button != 0 || (e.type != EventType.MouseDown && e.type != EventType.MouseDrag)) return;
            if (e.type == EventType.MouseDown && HandleUtility.nearestControl != control) return;
            if (!stroke && e.type != EventType.MouseDown) return;
            try
            {
                if (!stroke)
                {
                    Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Terrain " + active);
                    Undo.RegisterCompleteObjectUndo(profile.sculptData, "Terrain " + active);
                    recordedMeshes.Clear(); changedChunks.Clear(); strokeStart.Clear();
                    stroke = true; segment = false; flattenHeight = hit.point.y;
                    hotControl = control; GUIUtility.hotControl = control;
                }
                var point = new Vector2(hit.point.x, hit.point.z);
                float spacing = Mathf.Max(profile.sculptData.spacing * .5f, radius * .18f);
                if (!segment) { Stamp(point, active); lastStamp = point; segment = true; }
                else
                {
                    float distance = Vector2.Distance(lastStamp, point);
                    int count = Mathf.FloorToInt(distance / spacing);
                    Vector2 direction = distance > 0 ? (point - lastStamp) / distance : Vector2.zero;
                    for (int i = 0; i < count; i++) { lastStamp += direction * spacing; Stamp(lastStamp, active); }
                }
                e.Use(); view.Repaint(); Repaint();
            }
            catch (Exception error) { EndStroke(); painting = false; status = error.Message; Debug.LogException(error); }
        }
        float Protection(Vector2 p)
        {
            if (!protect) return 1;
            float grid = profile.sculptData.spacing;
            // Include a lattice halo: the rendered triangle can extend into a protected road.
            float mask = Mathf.SmoothStep(0, 1, Mathf.Clamp01((profile.RouteDistance(p) - grid * 2) / (grid * 2)));
            foreach (var foundation in profile.buildingFoundations)
                mask = Mathf.Min(mask, Mathf.SmoothStep(0, 1, Mathf.Clamp01((Vector2.Distance(p, foundation.center) - foundation.radius - grid) / Mathf.Max(grid * 2, foundation.blend))));
            foreach (var platform in profile.walkingPlatforms)
                mask = Mathf.Min(mask, WorldGeographyProfile.Inside(p, platform.boundary) ? 0 : Mathf.SmoothStep(0, 1, Mathf.Clamp01((WorldGeographyProfile.LineDistance(p, platform.boundary, true) - grid) / (grid * 2))));
            return mask;
        }
        void Stamp(Vector2 center, BrushMode active)
        {
            var data = profile.sculptData; float grid = data.spacing;
            int minX = Mathf.FloorToInt((center.x - radius - data.origin.x) / grid), maxX = Mathf.CeilToInt((center.x + radius - data.origin.x) / grid);
            int minZ = Mathf.FloorToInt((center.y - radius - data.origin.y) / grid), maxZ = Mathf.CeilToInt((center.y + radius - data.origin.y) / grid);
            var changes = new Dictionary<Vector2Int, float>();
            for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++)
            {
                var key = new Vector2Int(x, z); var p = data.Position(x, z);
                if (p.x < profile.world.origin.x || p.y < profile.world.origin.y || p.x > profile.world.origin.x + profile.world.gridSize.x * profile.world.chunkSize || p.y > profile.world.origin.y + profile.world.gridSize.y * profile.world.chunkSize) continue;
                float weight = WorldTerrainBrushMath.Falloff(Vector2.Distance(p, center), radius, hardness) * Protection(p);
                if (weight <= .00001f) continue;
                float current = data.Get(x, z), delta;
                if (active == BrushMode.Raise || active == BrushMode.Lower) delta = (active == BrushMode.Raise ? 1 : -1) * strength * weight;
                else if (active == BrushMode.Uneven)
                {
                    if (!strokeStart.TryGetValue(key, out float start)) { start = current; strokeStart.Add(key, start); }
                    float target = start + WorldTerrainBrushMath.Noise(p, featureSize, seed) * amplitude;
                    delta = (target - current) * blend * weight;
                }
                else
                {
                    float height = WorldTerrainSculptAssets.SampleGround(profile, p.x, p.y), target = flattenHeight;
                    if (active == BrushMode.Smooth)
                    {
                        target = 0;
                        // Read the complete old neighborhood before applying any new sample values.
                        for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                            target += WorldTerrainSculptAssets.SampleGround(profile, p.x + dx * grid, p.y + dz * grid) / 9;
                    }
                    delta = (target - height) * blend * weight;
                }
                if (Mathf.Abs(delta) > .000001f) changes.Add(key, delta);
            }
            if (changes.Count == 0) { status = "This area is protected. Move onto open land or turn off protection to sculpt here."; return; }
            float SampleDelta(float wx, float wz)
            {
                float gx = (wx - data.origin.x) / grid, gz = (wz - data.origin.y) / grid;
                int ix = Mathf.FloorToInt(gx), iz = Mathf.FloorToInt(gz);
                float Get(int a, int b) => changes.TryGetValue(new Vector2Int(a, b), out float value) ? value : 0;
                return Mathf.Lerp(Mathf.Lerp(Get(ix, iz), Get(ix + 1, iz), gx - ix), Mathf.Lerp(Get(ix, iz + 1), Get(ix + 1, iz + 1), gx - ix), gz - iz);
            }
            WorldTerrainSculptAssets.ApplyDelta(profile, Rect.MinMaxRect(center.x - radius - grid, center.y - radius - grid, center.x + radius + grid, center.y + radius + grid), SampleDelta, recordedMeshes, changedChunks);
            foreach (var change in changes) data.Set(change.Key.x, change.Key.y, data.Get(change.Key.x, change.Key.y) + change.Value);
            EditorUtility.SetDirty(data);
            status = active + " applied. " + changedChunks.Count + " chunk(s) touched. Ctrl+Z undoes this stroke.";
        }
        void EndStroke()
        {
            if (!stroke) return;
            stroke = false; segment = false;
            if (GUIUtility.hotControl == hotControl) GUIUtility.hotControl = 0;
            hotControl = 0;
            Undo.CollapseUndoOperations(undoGroup);
            if (profile != null && profile.sculptData != null) AssetDatabase.SaveAssetIfDirty(profile.sculptData);
            foreach (var mesh in recordedMeshes) if (mesh != null) AssetDatabase.SaveAssetIfDirty(mesh);
            WorldTerrainSculptAssets.RefreshColliders();
            SceneView.RepaintAll();
        }
    }
}
