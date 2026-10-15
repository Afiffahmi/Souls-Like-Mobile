using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nusantara.World.Editor
{
    public sealed partial class WorldTerrainBrushWindow
    {
        enum ShapeTool { Freehand, Curve, Outline }
        [SerializeField] ShapeTool shapeTool;
        [SerializeField] WorldTerrainShape shapeDraft = new WorldTerrainShape();
        [SerializeField] int selectedPoint;

        WorldTerrainShape SavedShape => profile != null && profile.sculptData != null
            ? profile.sculptData.shapes.Find(s => s.id == shapeDraft.id) : null;

        static WorldTerrainShape CopyShape(WorldTerrainShape shape) => JsonUtility.FromJson<WorldTerrainShape>(JsonUtility.ToJson(shape));

        void ResetShape()
        {
            shapeDraft = new WorldTerrainShape { kind = shapeTool == ShapeTool.Outline ? WorldTerrainShape.ShapeKind.Outline : WorldTerrainShape.ShapeKind.Curve };
            selectedPoint = 0;
            SceneView.RepaintAll(); Repaint();
        }

        void DrawToolMode()
        {
            var next = (ShapeTool)GUILayout.Toolbar((int)shapeTool, new[] { "Freehand", "Line / Curve", "Outline / Fill" });
            if (next == shapeTool) return;
            EndStroke(); Undo.RecordObject(this, "Change terrain tool"); shapeTool = next;
            if (next != ShapeTool.Freehand) shapeDraft.kind = next == ShapeTool.Outline ? WorldTerrainShape.ShapeKind.Outline : WorldTerrainShape.ShapeKind.Curve;
            status = next == ShapeTool.Freehand ? "Drag on the ground to sculpt." : "Click ground to place points. Adjust the preview, then Apply shape.";
            SceneView.RepaintAll();
        }

        void DrawShapeControls()
        {
            EditorGUILayout.Space(8);
            var data = profile.sculptData;
            var labels = new List<string> { "New shape / current preview" };
            int current = 0;
            if (data != null) for (int i = 0; i < data.shapes.Count; i++)
            {
                var shape = data.shapes[i]; labels.Add($"{shape.name} ({shape.kind}, {shape.operation})");
                if (shape.id == shapeDraft.id) current = i + 1;
            }
            int picked = EditorGUILayout.Popup("Saved shape", current, labels.ToArray());
            if (picked != current)
            {
                Undo.RecordObject(this, "Select terrain shape");
                if (picked == 0) ResetShape();
                else
                {
                    shapeDraft = CopyShape(data.shapes[picked - 1]);
                    shapeDraft.samples.Clear();
                    shapeTool = shapeDraft.kind == WorldTerrainShape.ShapeKind.Curve ? ShapeTool.Curve : ShapeTool.Outline;
                    selectedPoint = 0; SceneView.RepaintAll();
                }
            }

            // The window is serialized too: point placement, settings and Apply all participate in Undo.
            var serialized = new SerializedObject(this);
            serialized.Update();
            var draft = serialized.FindProperty("shapeDraft");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(draft.FindPropertyRelative("name"), new GUIContent("Shape name"));
            EditorGUILayout.PropertyField(draft.FindPropertyRelative("operation"), new GUIContent("Operation"));
            bool outline = shapeDraft.kind == WorldTerrainShape.ShapeKind.Outline;
            if (!outline) EditorGUILayout.PropertyField(draft.FindPropertyRelative("smooth"), new GUIContent("Smooth curve"));
            float grid = data != null ? data.spacing : profile.world.chunkSize / Mathf.Max(1, profile.terrainIntervals);
            EditorGUILayout.Slider(draft.FindPropertyRelative("radius"), Mathf.Max(2, grid * 1.5f), 160,
                new GUIContent(outline ? "Edge blend (m)" : "Half width (m)"));
            EditorGUILayout.Slider(draft.FindPropertyRelative("hardness"), 0, .9f, new GUIContent("Hardness"));
            var operation = (WorldTerrainShape.Operation)draft.FindPropertyRelative("operation").enumValueIndex;
            if (operation == WorldTerrainShape.Operation.Raise || operation == WorldTerrainShape.Operation.Lower)
                EditorGUILayout.Slider(draft.FindPropertyRelative("strength"), .05f, 100, new GUIContent("Height change (m)"));
            else
                EditorGUILayout.Slider(draft.FindPropertyRelative("blend"), .02f, 1, new GUIContent("Strength"));
            if (operation == WorldTerrainShape.Operation.Flatten)
                EditorGUILayout.PropertyField(draft.FindPropertyRelative("targetHeight"), new GUIContent("Target height (m)"));
            if (operation == WorldTerrainShape.Operation.Uneven)
            {
                EditorGUILayout.Slider(draft.FindPropertyRelative("amplitude"), .1f, 30, new GUIContent("Variation height (m)"));
                EditorGUILayout.Slider(draft.FindPropertyRelative("featureSize"), grid * 4, 400, new GUIContent("Feature size (m)"));
                EditorGUILayout.PropertyField(draft.FindPropertyRelative("seed"), new GUIContent("Pattern seed"));
            }
            EditorGUILayout.PropertyField(draft.FindPropertyRelative("protect"), new GUIContent("Protect roads and buildings"));
            bool changed = EditorGUI.EndChangeCheck();
            serialized.ApplyModifiedProperties();
            if (changed) SceneView.RepaintAll();

            EditorGUILayout.HelpBox(outline
                ? "Click around the area. The outline closes automatically; the whole interior is affected, with a soft blend outside the boundary."
                : $"Place two points for a line, or more for a curve. Full width: {shapeDraft.radius * 2:0.#} m. The height change is applied once, including bends and crossings.", MessageType.None);
            GUILayout.Label($"{shapeDraft.points.Count} control points", EditorStyles.boldLabel);
            if (shapeDraft.points.Count > 0)
            {
                selectedPoint = Mathf.Clamp(EditorGUILayout.IntSlider("Selected point", selectedPoint + 1, 1, shapeDraft.points.Count) - 1, 0, shapeDraft.points.Count - 1);
                var p = shapeDraft.points[selectedPoint];
                EditorGUI.BeginChangeCheck();
                var moved = EditorGUILayout.Vector2Field("Position (X, Z)", new Vector2(p.x, p.z));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Move terrain point");
                    shapeDraft.points[selectedPoint] = OnShapeGround(new Vector3(moved.x, p.y, moved.y)); SceneView.RepaintAll();
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(shapeDraft.points.Count == 0))
                    if (GUILayout.Button("Remove point")) RemoveShapePoint();
                using (new EditorGUI.DisabledScope(shapeDraft.points.Count < 2 || shapeDraft.points.Count >= 128))
                    if (GUILayout.Button("Insert midpoint"))
                    {
                        Undo.RecordObject(this, "Insert terrain point");
                        int a = Mathf.Min(selectedPoint, shapeDraft.points.Count - 2);
                        shapeDraft.points.Insert(a + 1, OnShapeGround((shapeDraft.points[a] + shapeDraft.points[a + 1]) * .5f));
                        selectedPoint = a + 1; SceneView.RepaintAll();
                    }
            }
            bool valid = WorldTerrainShapeMath.Valid(shapeDraft, out string reason);
            if (!valid) EditorGUILayout.HelpBox(reason, MessageType.Info);
            using (new EditorGUI.DisabledScope(!valid || data == null))
                if (GUILayout.Button(SavedShape == null ? "Apply shape" : "Update shape", GUILayout.Height(32))) ApplyShape(false);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New shape / clear preview")) { Undo.RecordObject(this, "New terrain shape"); ResetShape(); }
                using (new EditorGUI.DisabledScope(SavedShape == null))
                    if (GUILayout.Button("Delete saved shape")) ApplyShape(true);
            }
            EditorGUILayout.HelpBox("Applied shapes stay in Saved shape. Select one, move its points or change settings, then Update shape. New shape clears only the preview. Delete removes that shape's height contribution. Save Terrain when finished.", MessageType.None);
        }

        Vector3 OnShapeGround(Vector3 point)
        {
            if (profile != null && profile.sculptData != null) point.y = WorldTerrainSculptAssets.SampleGround(profile, point.x, point.z);
            return point;
        }

        void RemoveShapePoint()
        {
            if (shapeDraft.points.Count == 0) return;
            Undo.RecordObject(this, "Remove terrain point");
            shapeDraft.points.RemoveAt(Mathf.Clamp(selectedPoint, 0, shapeDraft.points.Count - 1));
            selectedPoint = Mathf.Max(0, selectedPoint - 1); SceneView.RepaintAll(); Repaint();
        }

        void ShapeSceneGUI(SceneView view)
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape) { DisablePainting(); e.Use(); return; }
                if (e.keyCode == KeyCode.Backspace) { RemoveShapePoint(); e.Use(); return; }
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { ApplyShape(false); e.Use(); return; }
            }
            if (e.alt || Tools.viewToolActive) return;
            int control = GUIUtility.GetControlID("NusantaraTerrainShape".GetHashCode(), FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);
            for (int i = 0; i < shapeDraft.points.Count; i++)
            {
                var point = OnShapeGround(shapeDraft.points[i]) + Vector3.up * .25f;
                Handles.color = i == selectedPoint ? Color.yellow : new Color(.4f, 1, .65f);
                EditorGUI.BeginChangeCheck();
                var moved = Handles.FreeMoveHandle(point, HandleUtility.GetHandleSize(point) * .09f, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Move terrain shape point"); selectedPoint = i;
                    shapeDraft.points[i] = OnShapeGround(moved - Vector3.up * .25f); view.Repaint(); Repaint();
                }
                Handles.Label(point + Vector3.up * HandleUtility.GetHandleSize(point) * .15f, (i + 1).ToString());
            }
            if (e.type == EventType.Repaint) DrawShapePreview();
            if (e.type == EventType.MouseMove) view.Repaint();
            if (e.type != EventType.MouseDown || e.button != 0 || HandleUtility.nearestControl != control) return;
            if (!Ground(HandleUtility.GUIPointToWorldRay(e.mousePosition), out var hit)) return;
            if (shapeDraft.points.Count >= 128) { status = "Use up to 128 points per shape."; Repaint(); e.Use(); return; }
            Undo.RecordObject(this, "Add terrain shape point");
            if (shapeDraft.points.Count == 0 && SavedShape == null) shapeDraft.targetHeight = hit.point.y;
            shapeDraft.points.Add(hit.point); selectedPoint = shapeDraft.points.Count - 1;
            status = "Drag numbered points or edit their X/Z position. Press Enter or Apply shape when ready.";
            e.Use(); view.Repaint(); Repaint();
        }

        void DrawShapePreview()
        {
            if (shapeDraft.points.Count < 2) return;
            float length = 0;
            for (int i = 1; i < shapeDraft.points.Count; i++) length += Vector3.Distance(shapeDraft.points[i - 1], shapeDraft.points[i]);
            bool closed = shapeDraft.kind == WorldTerrainShape.ShapeKind.Outline;
            float spacing = Mathf.Max(1, length / 256);
            var path = WorldTerrainShapeMath.Path(shapeDraft, spacing);
            if (closed)
            {
                var boundary = new List<Vector3>(shapeDraft.points) { shapeDraft.points[0] };
                path.Clear();
                foreach (var p in WorldRoadCurve.Sample(boundary, spacing, false)) path.Add(new Vector2(p.x, p.z));
            }
            int count = path.Count;
            var center = new Vector3[count]; var left = new Vector3[count]; var right = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var p = path[i % path.Count];
                center[i] = OnShapeGround(new Vector3(p.x, 0, p.y)) + Vector3.up * .2f;
                if (closed) continue;
                var d = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                var side = new Vector3(d.y, 0, -d.x) * shapeDraft.radius;
                left[i] = OnShapeGround(center[i] + side) + Vector3.up * .2f;
                right[i] = OnShapeGround(center[i] - side) + Vector3.up * .2f;
            }
            Handles.color = new Color(.4f, 1, .65f);
            Handles.DrawAAPolyLine(3, center);
            if (closed)
                Handles.Label(center[0] + Vector3.up, $"Filled area • {shapeDraft.operation} • edge blend {shapeDraft.radius:0.#} m");
            else
            {
                Handles.color = new Color(1, .85f, .45f, .8f);
                Handles.DrawAAPolyLine(2, left); Handles.DrawAAPolyLine(2, right);
                Handles.DrawWireDisc(center[0], Vector3.up, shapeDraft.radius);
                Handles.DrawWireDisc(center[count - 1], Vector3.up, shapeDraft.radius);
            }
        }

        void ApplyShape(bool delete)
        {
            if (profile == null || profile.sculptData == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!delete && !WorldTerrainShapeMath.Valid(shapeDraft, out status)) { Repaint(); return; }
            EndStroke();
            int group = -1;
            try
            {
                var data = profile.sculptData; var saved = SavedShape;
                int savedIndex = saved != null ? data.shapes.IndexOf(saved) : -1;
                if (delete && saved == null) return;
                var previous = new Dictionary<Vector2Int, float>();
                if (saved != null) foreach (var sample in saved.samples) previous[sample.coordinate] = sample.delta;
                float Baseline(float x, float z) => WorldTerrainSculptAssets.SampleGround(profile, x, z) - WorldTerrainShapeMath.Sample(previous, data, x, z);
                var world = profile.world;
                var next = delete ? new Dictionary<Vector2Int, float>() : WorldTerrainShapeMath.Contributions(shapeDraft, data,
                    new Rect(world.origin, new Vector2(world.gridSize.x * world.chunkSize, world.gridSize.y * world.chunkSize)), Baseline, p => Protection(p, true));
                var difference = WorldTerrainShapeMath.Difference(next, previous);
                if (!delete && next.Count == 0 && previous.Count == 0)
                { status = "No terrain samples are affected. Check the points, strength and road/building protection."; Repaint(); return; }

                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(delete ? "Delete terrain shape" : "Apply terrain shape");
                Undo.RegisterCompleteObjectUndo(data, "Terrain shape"); Undo.RegisterCompleteObjectUndo(this, "Terrain shape preview");
                recordedMeshes.Clear(); changedChunks.Clear();
                if (difference.Count > 0)
                {
                    Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
                    foreach (var pair in difference) { var p = data.Position(pair.Key.x, pair.Key.y); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
                    var pad = Vector2.one * data.spacing;
                    WorldTerrainSculptAssets.ApplyDelta(profile, Rect.MinMaxRect(min.x - pad.x, min.y - pad.y, max.x + pad.x, max.y + pad.y),
                        (x, z) => WorldTerrainShapeMath.Sample(difference, data, x, z), recordedMeshes, changedChunks);
                    foreach (var pair in difference) data.Set(pair.Key.x, pair.Key.y, data.Get(pair.Key.x, pair.Key.y) + pair.Value);
                }
                if (saved != null) data.shapes.Remove(saved);
                if (delete) ResetShape();
                else
                {
                    if (saved == null)
                    {
                        shapeDraft.id = Guid.NewGuid().ToString("N");
                        if (string.IsNullOrWhiteSpace(shapeDraft.name) || shapeDraft.name == "Terrain shape")
                            shapeDraft.name = $"{shapeDraft.kind} {data.shapes.Count + 1}";
                    }
                    var applied = CopyShape(shapeDraft); applied.samples.Clear();
                    foreach (var pair in next) applied.samples.Add(new WorldTerrainShape.Sample(pair.Key, pair.Value));
                    // Preserve the saved-shape ordering when updating an existing shape.
                    int index = saved != null ? Mathf.Min(data.shapes.Count, savedIndex) : data.shapes.Count;
                    data.shapes.Insert(index, applied);
                    shapeDraft.samples.Clear();
                }
                EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
                foreach (var mesh in recordedMeshes) if (mesh != null) AssetDatabase.SaveAssetIfDirty(mesh);
                status = delete ? "Shape removed. Other height contributions remain. Save Terrain when finished."
                    : "Shape applied and kept editable. Select it in Saved shape to update later. Save Terrain when finished.";
            }
            catch (Exception error)
            {
                if (group >= 0) Undo.RevertAllDownToGroup(group);
                profile.sculptData.InvalidateCache(); WorldTerrainSculptAssets.InvalidateCache(); WorldTerrainSculptAssets.RefreshColliders();
                status = "Shape could not be applied: " + error.Message; Debug.LogException(error);
            }
            finally
            {
                if (group >= 0) Undo.CollapseUndoOperations(group);
                SceneView.RepaintAll(); Repaint();
            }
        }
    }
}
