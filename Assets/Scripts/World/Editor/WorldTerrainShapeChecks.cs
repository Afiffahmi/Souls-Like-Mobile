using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nusantara.World.Editor
{
    public static class WorldTerrainShapeChecks
    {
        [MenuItem("Tools/Nusantara/Run Terrain Shape Checks")]
        public static void RunMenu() => Debug.Log(Run());

        public static string RunMath()
        {
            int passed = 0;
            void Check(bool condition, string name)
            {
                if (!condition) throw new InvalidOperationException("FAILED: " + name);
                passed++;
            }
            bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;
            float Get(IReadOnlyDictionary<Vector2Int, float> samples, int x, int z) => samples.TryGetValue(new Vector2Int(x, z), out var v) ? v : 0;
            var bounds = new Rect(-100, -100, 200, 200);
            var curve = new WorldTerrainShape { radius = 4, strength = 3, hardness = 0, smooth = false, protect = false };
            curve.points.Add(new Vector3(-20, 0, 0)); curve.points.Add(new Vector3(20, 0, 0));
            Dictionary<Vector2Int, float> Evaluate(WorldTerrainShape shape, Func<float, float, float> height = null, Func<Vector2, float> protection = null)
                => WorldTerrainShapeMath.Contributions(shape, Vector2.zero, 1, bounds, height ?? ((x, z) => 0), protection ?? (p => 1));
            var sparse = Evaluate(curve);
            Check(Near(Get(sparse, 0, 0), 3) && Near(Get(sparse, 0, 2), 1.5f) && Get(sparse, 0, 4) == 0,
                "Constant width and soft edge along a straight path");
            curve.points.Insert(1, Vector3.zero);
            var dense = Evaluate(curve);
            bool identical = sparse.Count == dense.Count;
            foreach (var pair in sparse) identical &= Near(pair.Value, Get(dense, pair.Key.x, pair.Key.y));
            Check(identical, "Adding collinear control points does not change strength");
            curve.points.Add(new Vector3(-20, 0, 0));
            var overlap = Evaluate(curve);
            Check(Near(Get(overlap, 0, 0), 3), "Retraced segments do not stack their height");
            curve.points = new List<Vector3> { new Vector3(-20, 0, -20), new Vector3(20, 0, 20), new Vector3(-20, 0, 20), new Vector3(20, 0, -20) };
            Check(Near(Get(Evaluate(curve), 0, 0), 3), "A crossing gets one application");
            curve.smooth = true;
            var smoothed = Evaluate(curve);
            var path = WorldTerrainShapeMath.Path(curve, .5f);
            bool indexedMatches = true;
            for (int z = -30; z <= 30; z++) for (int x = -30; x <= 30; x++)
                indexedMatches &= Near(Get(smoothed, x, z), WorldTerrainShapeMath.Coverage(curve, path, new Vector2(x, z)) * 3);
            Check(indexedMatches, "Indexed curve evaluation matches full-distance coverage, including negative coordinates");

            var outline = new WorldTerrainShape { kind = WorldTerrainShape.ShapeKind.Outline, radius = 4, strength = 3, hardness = 0, protect = false };
            outline.points = new List<Vector3> { new Vector3(-10, 0, -10), new Vector3(10, 0, -10), new Vector3(10, 0, 10), new Vector3(-10, 0, 10) };
            var filled = Evaluate(outline);
            Check(Near(Get(filled, 0, 0), 3) && Near(Get(filled, 10, 0), 3) && Near(Get(filled, 12, 0), 1.5f) && Get(filled, 14, 0) == 0,
                "Outline fills its interior and blends outside its boundary");
            outline.points = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 4), new Vector3(4, 0, 4), new Vector3(4, 0, 10), new Vector3(0, 0, 10) };
            var concave = Evaluate(outline);
            Check(WorldTerrainShapeMath.Valid(outline, out _) && Near(Get(concave, 2, 8), 3) && Get(concave, 9, 9) == 0, "Concave outlines preserve their excluded area");
            outline.points = new List<Vector3> { new Vector3(-10, 0, -10), new Vector3(10, 0, 10), new Vector3(-10, 0, 10), new Vector3(10, 0, -10) };
            Check(!WorldTerrainShapeMath.Valid(outline, out _), "Crossing outline edges are rejected");
            outline.points = new List<Vector3> { Vector3.zero, Vector3.right, Vector3.right * 2 };
            Check(!WorldTerrainShapeMath.Valid(outline, out _), "Zero-area outlines are rejected");

            curve.smooth = false; curve.points = new List<Vector3> { new Vector3(-20, 0, 0), new Vector3(20, 0, 0) };
            var old = Evaluate(curve);
            var unchanged = WorldTerrainShapeMath.Difference(Evaluate(curve), old);
            bool noStack = true;
            foreach (var pair in unchanged) noStack &= Near(pair.Value, 0);
            Check(noStack, "Updating an unchanged shape does not accumulate height");
            curve.points = new List<Vector3> { new Vector3(-20, 0, 20), new Vector3(20, 0, 20) };
            var moved = Evaluate(curve); var difference = WorldTerrainShapeMath.Difference(moved, old);
            Check(Near(Get(old, 0, 0) + 7 + Get(difference, 0, 0), 7) && Near(Get(difference, 0, 20), 3),
                "Moving a shape removes its old footprint and preserves an independent later edit");
            var removed = WorldTerrainShapeMath.Difference(new Dictionary<Vector2Int, float>(), moved);
            Check(Near(Get(moved, 0, 20) + 7 + Get(removed, 0, 20), 7), "Deleting a shape preserves other height contributions");

            curve.protect = true;
            Check(Get(Evaluate(curve, protection: p => 0), 0, 20) == 0, "Protected areas receive no contribution");
            curve.protect = false; curve.operation = WorldTerrainShape.Operation.Lower;
            Check(Near(Get(Evaluate(curve), 0, 20), -3), "Lower has a consistent negative height");
            curve.operation = WorldTerrainShape.Operation.Flatten; curve.targetHeight = 12; curve.blend = 1;
            float Slope(float x, float z) => x * .5f + 2;
            var flatten = Evaluate(curve, Slope);
            Check(Near(Slope(5, 20) + Get(flatten, 5, 20), 12), "Flatten uses an explicit world height on sloped ground");
            var repeatedFlatten = Evaluate(curve, (x, z) => Slope(x, z) + WorldTerrainShapeMath.Sample(flatten, Vector2.zero, 1, x, z)
                - WorldTerrainShapeMath.Sample(flatten, Vector2.zero, 1, x, z));
            Check(Near(Get(repeatedFlatten, 5, 20), Get(flatten, 5, 20)), "Flatten recomputes against the baseline without stacking");
            curve.operation = WorldTerrainShape.Operation.Smooth;
            Check(Near(Get(Evaluate(curve, Slope), 5, 20), 0), "Smoothing preserves a planar slope");
            curve.operation = WorldTerrainShape.Operation.Uneven;
            var noiseA = Evaluate(curve); var noiseB = Evaluate(curve);
            Check(Near(Get(noiseA, 5, 20), Get(noiseB, 5, 20)), "Uneven shapes are repeatable");
            var samples = new Dictionary<Vector2Int, float> { [new Vector2Int(-1, -1)] = 2, [new Vector2Int(0, -1)] = 4, [new Vector2Int(-1, 0)] = 6, [Vector2Int.zero] = 8 };
            Check(Near(WorldTerrainShapeMath.Sample(samples, new Vector2(3, -7), 8, -1, -11), 5), "Contribution interpolation crosses negative lattice coordinates");
            curve.radius = float.NaN;
            Check(!WorldTerrainShapeMath.Valid(curve, out _), "Non-finite settings are rejected before editing terrain");
            return $"{passed} terrain shape math checks passed.";
        }

        public static string Run()
        {
            string math = RunMath();
            var data = ScriptableObject.CreateInstance<WorldTerrainSculptData>();
            try
            {
                var shape = new WorldTerrainShape { id = "fixture", name = "Saved ridge" };
                shape.points.Add(Vector3.zero); shape.points.Add(Vector3.right * 20);
                shape.samples.Add(new WorldTerrainShape.Sample(Vector2Int.zero, 3));
                data.shapes.Add(shape); data.Set(0, 0, 3);
                string json = EditorJsonUtility.ToJson(data);
                data.shapes.Clear(); data.Set(0, 0, 0);
                EditorJsonUtility.FromJsonOverwrite(json, data); data.InvalidateCache();
                if (data.shapes.Count != 1 || data.shapes[0].id != "fixture" || data.shapes[0].points.Count != 2
                    || data.shapes[0].samples.Count != 1 || data.shapes[0].samples[0].delta != 3 || data.Get(0, 0) != 3)
                    throw new InvalidOperationException("Saved shape settings and height contributions did not survive serialization.");
                return math + " Saved shape serialization passed.";
            }
            finally { UnityEngine.Object.DestroyImmediate(data); }
        }
    }
}
