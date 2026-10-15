using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nusantara.World.Editor
{
    /// <summary>Evaluates a whole shape once, independent of point density or overlapping segments.</summary>
    public static class WorldTerrainShapeMath
    {
        public static List<Vector2> Path(WorldTerrainShape shape, float spacing)
        {
            var result = new List<Vector2>();
            var path = shape.kind == WorldTerrainShape.ShapeKind.Curve
                ? WorldRoadCurve.Sample(shape.points, spacing, shape.smooth) : shape.points;
            foreach (var p in path) result.Add(new Vector2(p.x, p.z));
            return result;
        }

        public static float Distance(Vector2 p, IReadOnlyList<Vector2> path, bool closed)
        {
            float nearest = float.MaxValue;
            int count = closed ? path.Count : path.Count - 1;
            for (int i = 0; i < count; i++)
            {
                var a = path[i]; var d = path[(i + 1) % path.Count] - a;
                var q = a + d * (d.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude) : 0);
                nearest = Mathf.Min(nearest, (p - q).sqrMagnitude);
            }
            return Mathf.Sqrt(nearest);
        }

        public static bool Inside(Vector2 p, IReadOnlyList<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i]; var b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        public static float Coverage(WorldTerrainShape shape, IReadOnlyList<Vector2> path, Vector2 p)
        {
            bool closed = shape.kind == WorldTerrainShape.ShapeKind.Outline;
            if (closed && Inside(p, path)) return 1;
            return WorldTerrainBrushMath.Falloff(Distance(p, path, closed), shape.radius, shape.hardness);
        }

        public static bool Valid(WorldTerrainShape shape, out string reason)
        {
            reason = "Use finite, positive width and feature size, and valid strength settings.";
            if (!float.IsFinite(shape.radius) || shape.radius <= 0 || !float.IsFinite(shape.strength) || shape.strength < 0
                || !float.IsFinite(shape.hardness) || shape.hardness < 0 || shape.hardness > 1
                || !float.IsFinite(shape.amplitude) || shape.amplitude < 0 || !float.IsFinite(shape.featureSize) || shape.featureSize <= 0
                || !float.IsFinite(shape.blend) || shape.blend < 0 || shape.blend > 1 || !float.IsFinite(shape.targetHeight)) return false;
            bool closed = shape.kind == WorldTerrainShape.ShapeKind.Outline;
            int minimum = closed ? 3 : 2;
            reason = $"Place at least {minimum} points.";
            if (shape.points.Count < minimum) return false;
            if (shape.points.Count > 128) { reason = "Use up to 128 points per shape."; return false; }
            foreach (var p in shape.points)
                if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z)) { reason = "Points must have finite coordinates."; return false; }
            for (int i = 1; i < shape.points.Count; i++)
                if (Vector2.Distance(new Vector2(shape.points[i].x, shape.points[i].z), new Vector2(shape.points[i - 1].x, shape.points[i - 1].z)) < .1f)
                { reason = "Move neighboring points apart."; return false; }
            if (closed)
            {
                var path = Path(shape, 1);
                double area = 0;
                for (int i = 0; i < path.Count; i++)
                {
                    var a = path[i]; var b = path[(i + 1) % path.Count];
                    area += (double)a.x * b.y - (double)b.x * a.y;
                    if ((b - a).sqrMagnitude < .01f) { reason = "The outline closes automatically. Remove the repeated endpoint."; return false; }
                    for (int j = i + 2; j < path.Count; j++)
                    {
                        if (i == 0 && j == path.Count - 1) continue;
                        if (Intersects(a, b, path[j], path[(j + 1) % path.Count]))
                        { reason = "Outline edges cross or touch. Move the points to form a simple boundary."; return false; }
                    }
                }
                if (Math.Abs(area) < .01) { reason = "The outline needs an area. Move the points apart."; return false; }
            }
            reason = "Ready to apply."; return true;
        }

        static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
            float abC = Cross(b - a, c - a), abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c), cdB = Cross(d - c, b - c);
            if (Mathf.Max(a.x, b.x) < Mathf.Min(c.x, d.x) || Mathf.Max(c.x, d.x) < Mathf.Min(a.x, b.x)
                || Mathf.Max(a.y, b.y) < Mathf.Min(c.y, d.y) || Mathf.Max(c.y, d.y) < Mathf.Min(a.y, b.y)) return false;
            return abC * abD <= 0 && cdA * cdB <= 0;
        }

        public static Dictionary<Vector2Int, float> Contributions(WorldTerrainShape shape, WorldTerrainSculptData data,
            Rect worldBounds, Func<float, float, float> baselineHeight, Func<Vector2, float> protection)
            => Contributions(shape, data.origin, data.spacing, worldBounds, baselineHeight, protection);

        public static Dictionary<Vector2Int, float> Contributions(WorldTerrainShape shape, Vector2 origin, float spacing,
            Rect worldBounds, Func<float, float, float> baselineHeight, Func<Vector2, float> protection)
        {
            if (!Valid(shape, out string reason)) throw new InvalidOperationException(reason);
            if (!float.IsFinite(spacing) || spacing <= 0) throw new InvalidOperationException("Terrain sample spacing must be positive.");
            var path = Path(shape, Mathf.Max(.5f, spacing * .5f));
            var min = path[0]; var max = min;
            foreach (var p in path) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            min = Vector2.Max(min - Vector2.one * shape.radius, worldBounds.min);
            max = Vector2.Min(max + Vector2.one * shape.radius, worldBounds.max);
            var result = new Dictionary<Vector2Int, float>();
            if (max.x < min.x || max.y < min.y) return result;
            int x0 = Mathf.CeilToInt((min.x - origin.x) / spacing), x1 = Mathf.FloorToInt((max.x - origin.x) / spacing);
            int z0 = Mathf.CeilToInt((min.y - origin.y) / spacing), z1 = Mathf.FloorToInt((max.y - origin.y) / spacing);
            if ((long)(x1 - x0 + 1) * (z1 - z0 + 1) > 250000 || path.Count > 16384)
                throw new InvalidOperationException("This shape is too large for one edit. Split it into smaller shapes.");
            var curve = shape.kind == WorldTerrainShape.ShapeKind.Curve ? new CurveField(path, shape.radius, spacing) : null;
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2((float)(origin.x + (double)x * spacing), (float)(origin.y + (double)z * spacing));
                float coverage = curve != null ? WorldTerrainBrushMath.Falloff(curve.Distance(p), shape.radius, shape.hardness) : Coverage(shape, path, p);
                float weight = coverage * (shape.protect ? protection(p) : 1);
                if (weight <= .00001f) continue;
                float delta;
                switch (shape.operation)
                {
                    case WorldTerrainShape.Operation.Raise: delta = shape.strength * weight; break;
                    case WorldTerrainShape.Operation.Lower: delta = -shape.strength * weight; break;
                    case WorldTerrainShape.Operation.Uneven:
                        delta = WorldTerrainBrushMath.Noise(p, shape.featureSize, shape.seed) * shape.amplitude * shape.blend * weight; break;
                    default:
                        float height = baselineHeight(p.x, p.y), target = shape.targetHeight;
                        if (shape.operation == WorldTerrainShape.Operation.Smooth)
                        {
                            target = 0;
                            for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                                target += baselineHeight(p.x + dx * spacing, p.y + dz * spacing) / 9;
                        }
                        delta = (target - height) * shape.blend * weight; break;
                }
                if (Mathf.Abs(delta) > .000001f) result[new Vector2Int(x, z)] = delta;
            }
            return result;
        }

        // Query only nearby curve segments, so long paths don't multiply the cost of every lattice sample.
        sealed class CurveField
        {
            readonly IReadOnlyList<Vector2> path;
            readonly float cell;
            readonly Dictionary<Vector2Int, List<int>> buckets = new Dictionary<Vector2Int, List<int>>();
            public CurveField(IReadOnlyList<Vector2> path, float radius, float grid)
            {
                this.path = path; cell = Mathf.Max(radius, grid * 4);
                for (int i = 1; i < path.Count; i++)
                {
                    var min = Bucket(Vector2.Min(path[i - 1], path[i]) - Vector2.one * radius);
                    var max = Bucket(Vector2.Max(path[i - 1], path[i]) + Vector2.one * radius);
                    for (int z = min.y; z <= max.y; z++) for (int x = min.x; x <= max.x; x++)
                    {
                        var key = new Vector2Int(x, z);
                        if (!buckets.TryGetValue(key, out var indices)) { indices = new List<int>(); buckets.Add(key, indices); }
                        indices.Add(i);
                    }
                }
            }
            Vector2Int Bucket(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell));
            public float Distance(Vector2 p)
            {
                if (!buckets.TryGetValue(Bucket(p), out var indices)) return float.MaxValue;
                float nearest = float.MaxValue;
                foreach (int i in indices)
                {
                    var a = path[i - 1]; var d = path[i] - a;
                    var q = a + d * (d.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude) : 0);
                    nearest = Mathf.Min(nearest, (p - q).sqrMagnitude);
                }
                return Mathf.Sqrt(nearest);
            }
        }

        public static float Sample(IReadOnlyDictionary<Vector2Int, float> values, WorldTerrainSculptData data, float x, float z)
            => Sample(values, data.origin, data.spacing, x, z);

        public static float Sample(IReadOnlyDictionary<Vector2Int, float> values, Vector2 origin, float spacing, float x, float z)
        {
            float gx = (x - origin.x) / spacing, gz = (z - origin.y) / spacing;
            int ix = Mathf.FloorToInt(gx), iz = Mathf.FloorToInt(gz);
            float Get(int a, int b) => values.TryGetValue(new Vector2Int(a, b), out float v) ? v : 0;
            return Mathf.Lerp(Mathf.Lerp(Get(ix, iz), Get(ix + 1, iz), gx - ix), Mathf.Lerp(Get(ix, iz + 1), Get(ix + 1, iz + 1), gx - ix), gz - iz);
        }

        public static Dictionary<Vector2Int, float> Difference(IReadOnlyDictionary<Vector2Int, float> next, IReadOnlyDictionary<Vector2Int, float> previous)
        {
            var result = new Dictionary<Vector2Int, float>();
            foreach (var p in previous) result[p.Key] = -p.Value;
            foreach (var p in next) result[p.Key] = p.Value + (result.TryGetValue(p.Key, out float old) ? old : 0);
            return result;
        }
    }
}
