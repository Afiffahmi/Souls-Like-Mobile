using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nusantara.World.Editor
{
    /// <summary>In-memory fixtures only: no scene objects or project assets are changed.</summary>
    public static class WorldTerrainSculptChecks
    {
        [MenuItem("Tools/Nusantara/Run Terrain Brush Checks")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            var passed = new List<string>();
            var data = ScriptableObject.CreateInstance<WorldTerrainSculptData>();
            var world = ScriptableObject.CreateInstance<WorldDefinition>();
            var meshes = new List<Mesh>();
            void Check(bool condition, string name)
            {
                if (!condition) throw new InvalidOperationException("FAILED: " + name);
                passed.Add(name);
            }
            bool Near(float a, float b, float tolerance = .00001f) => Mathf.Abs(a - b) <= tolerance;
            try
            {
                Check(data.Get(-50000, 50000) == 0 && data.TileCount == 0, "Untouched space is zero without allocating a world grid");
                data.Set(90000, -90000, 0);
                Check(data.TileCount == 0, "Zero writes do not allocate tiles");
                data.Set(-1, -1, 4); data.Set(-16, -16, 5); data.Set(-17, -17, 6); data.Set(0, 0, 7);
                Check(data.Get(-1, -1) == 4 && data.Get(-16, -16) == 5 && data.Get(-17, -17) == 6 && data.Get(0, 0) == 7 && data.TileCount == 3,
                    "Negative coordinates floor-divide across 16-sample tile boundaries");
                data.Set(int.MinValue, int.MinValue, 8); data.Set(int.MaxValue, int.MaxValue, 9);
                Check(data.Get(int.MinValue, int.MinValue) == 8 && data.Get(int.MaxValue, int.MaxValue) == 9 && data.TileCount == 5,
                    "Distant integer samples remain sparse without coordinate overflow");
                data.Set(int.MinValue, int.MinValue, 0); data.Set(int.MaxValue, int.MaxValue, 0);
                Check(data.TileCount == 3, "Erasing a tile's last height offset releases its storage");
                data.origin = new Vector2(3, -7); data.spacing = 8;
                data.Set(-1, -1, 2); data.Set(0, -1, 4); data.Set(-1, 0, 6); data.Set(0, 0, 8);
                Check(data.Position(-1, -1) == new Vector2(-5, -15) && Near(data.Sample(-1, -11), 5),
                    "Bilinear interpolation crosses both negative tile boundaries with a translated origin");
                var exact = data.Position(-1, -1);
                Check(Near(data.Sample(exact.x, exact.y), 2), "Lattice positions reproduce exact authored heights");
                data.pendingChunks.Add(new Vector2Int(4, 5));
                string snapshot = EditorJsonUtility.ToJson(data);
                data.Set(-1, -1, 999);
                EditorJsonUtility.FromJsonOverwrite(snapshot, data);
                data.InvalidateCache();
                Check(data.Get(-1, -1) == 2 && data.pendingChunks.Contains(new Vector2Int(4, 5)),
                    "Serialized restore and explicit cache invalidation restore height data and pending chunks");
                data.Set(0, 0, float.NaN);
                Check(data.Get(0, 0) == 0 && data.Sample(float.NaN, 0) == 0 && data.Sample(float.PositiveInfinity, 0) == 0,
                    "Non-finite height and position inputs cannot contaminate the mesh");
                data.spacing = 0;
                Check(Near(data.Sample(exact.x, exact.y), 2), "Invalid sample spacing uses a finite positive fallback");

                Check(WorldTerrainBrushMath.Falloff(0, 10, 0) == 1 && WorldTerrainBrushMath.Falloff(10, 10, .5f) == 0 && WorldTerrainBrushMath.Falloff(20, 10, 1) == 0,
                    "Brush support is exactly zero at and beyond its radius");
                Check(WorldTerrainBrushMath.Falloff(4, 10, .5f) == 1 && Near(WorldTerrainBrushMath.Falloff(7.5f, 10, .5f), .5f),
                    "Hardness sets the inner plateau with a smooth outer fade");
                Check(WorldTerrainBrushMath.Falloff(5, 10, 0) > WorldTerrainBrushMath.Falloff(8, 10, 0)
                    && WorldTerrainBrushMath.Falloff(9, 10, 1) == 1 && WorldTerrainBrushMath.Falloff(0, 0, 0) == 0
                    && WorldTerrainBrushMath.Falloff(float.NaN, 10, 0) == 0,
                    "Falloff decreases outwards and handles hard brushes and invalid radii");
                bool bounded = true, repeatable = true, variedSeed = false;
                float lowest = 1, highest = -1;
                for (int z = -20; z <= 20; z++)
                    for (int x = -20; x <= 20; x++)
                    {
                        var p = new Vector2(x * 7.3f, z * 5.9f);
                        float n = WorldTerrainBrushMath.Noise(p, 24, 827);
                        bounded &= !float.IsNaN(n) && n >= -1 && n <= 1;
                        repeatable &= n == WorldTerrainBrushMath.Noise(p, 24, 827);
                        variedSeed |= Mathf.Abs(n - WorldTerrainBrushMath.Noise(p, 24, 828)) > .05f;
                        lowest = Mathf.Min(lowest, n); highest = Mathf.Max(highest, n);
                    }
                Check(bounded && repeatable && variedSeed, "Natural brush noise is deterministic, bounded and seed-dependent");
                Check(lowest < -.1f && highest > .1f, "Natural brush adds both raised and lowered variation");
                Check(Mathf.Abs(WorldTerrainBrushMath.Noise(new Vector2(-.001f, 24), 24, 827)
                    - WorldTerrainBrushMath.Noise(new Vector2(.001f, 24), 24, 827)) < .001f,
                    "Natural noise is coherent across the negative-to-positive lattice boundary");

                world.origin = new Vector2(-13, 29); world.chunkSize = 200.3f;
                data.origin = world.origin; data.spacing = 12;
                for (int z = -2; z <= 20; z++)
                    for (int x = -2; x <= 36; x++)
                    {
                        var p = data.Position(x, z);
                        data.Set(x, z, WorldTerrainBrushMath.Noise(p, 35, 17) * 5);
                    }
                float Height(float x, float z) => x * .03f + z * .01f + data.Sample(x, z);
                const int intervals = 32;
                var left = WorldTerrainMesher.Build(world, Vector2Int.zero, intervals, Height); meshes.Add(left);
                var right = WorldTerrainMesher.Build(world, Vector2Int.right, intervals, Height); meshes.Add(right);
                var far = WorldTerrainMesher.Build(world, Vector2Int.right, 8, Height); meshes.Add(far);
                var lv = left.vertices; var rv = right.vertices; var ln = left.normals; var rn = right.normals;
                var fv = far.vertices; var fn = far.normals;
                bool heightsMatch = true, normalsMatch = true, lodMatch = true;
                for (int z = 0; z <= intervals; z++)
                {
                    int l = z * (intervals + 1) + intervals, r = z * (intervals + 1);
                    heightsMatch &= Near(lv[l].y, rv[r].y);
                    normalsMatch &= Vector3.Distance(ln[l], rn[r]) < .00001f;
                    if (z % 4 == 0)
                    {
                        int f = z / 4 * 9;
                        lodMatch &= Near(lv[l].y, fv[f].y) && Vector3.Distance(ln[l], fn[f]) < .00001f;
                    }
                }
                Check(heightsMatch && normalsMatch, "Authored offsets preserve shared chunk-edge heights and normals");
                Check(lodMatch, "Near and distant terrain share the same authored world-space samples");
                int originalVertices = far.vertexCount, originalIndices = far.triangles.Length;
                WorldGenerationGeometry.AddSkirt(far, 8, 12);
                var skirt = far.vertices;
                bool skirtDepth = true;
                for (int i = originalVertices; i < skirt.Length; i++)
                {
                    bool matched = false;
                    for (int j = 0; j < originalVertices; j++)
                        if (Near(skirt[i].x, skirt[j].x) && Near(skirt[i].z, skirt[j].z) && Near(skirt[i].y + 12, skirt[j].y)) { matched = true; break; }
                    skirtDepth &= matched;
                }
                Check(far.vertexCount == originalVertices + 8 * 8 && far.triangles.Length == originalIndices + 8 * 4 * 6 && skirtDepth,
                    "Distant terrain retains a 12-metre skirt below its sculpted surface");
                var originalUv = far.uv; var originalTriangles = far.triangles;
                var colors = new Color[far.vertexCount]; var tangents = new Vector4[far.vertexCount];
                for (int i = 0; i < colors.Length; i++) { colors[i] = new Color(.1f, .2f, .3f, .4f); tangents[i] = new Vector4(1, 0, 0, -1); }
                far.colors = colors; far.tangents = tangents;
                var before = far.vertices;
                float Delta(float x, float z) => Mathf.Sin(x * .015f) * 3 + Mathf.Cos(z * .018f) * 2;
                int records = 0;
                Check(WorldTerrainSculptAssets.ApplyMeshDelta(far, world.ChunkOrigin(Vector2Int.right), Delta, () => records++) && records == 1,
                    "Incremental sculpt registers its mesh once before changing it");
                var after = far.vertices;
                bool deltaPreserved = true, attributesPreserved = true;
                var actualUv = far.uv; var actualTriangles = far.triangles; var actualColors = far.colors; var actualTangents = far.tangents;
                var farOrigin = world.ChunkOrigin(Vector2Int.right);
                for (int i = 0; i < after.Length; i++)
                {
                    deltaPreserved &= Near(after[i].y, before[i].y + Delta(farOrigin.x + before[i].x, farOrigin.z + before[i].z));
                    attributesPreserved &= actualUv[i] == originalUv[i] && actualColors[i] == colors[i] && actualTangents[i] == tangents[i];
                }
                for (int i = 0; i < actualTriangles.Length; i++) attributesPreserved &= actualTriangles[i] == originalTriangles[i];
                Check(deltaPreserved && attributesPreserved && after.Length == before.Length,
                    "Incremental edits preserve authored terrain, skirt depth, UVs, colors, tangents and topology");
                WorldTerrainSculptAssets.ApplyMeshDelta(left, world.ChunkOrigin(Vector2Int.zero), Delta);
                WorldTerrainSculptAssets.ApplyMeshDelta(right, world.ChunkOrigin(Vector2Int.right), Delta);
                lv = left.vertices; rv = right.vertices; ln = left.normals; rn = right.normals;
                bool incrementalSeams = true;
                for (int z = 0; z <= intervals; z++)
                {
                    int l = z * (intervals + 1) + intervals, r = z * (intervals + 1);
                    incrementalSeams &= Near(lv[l].y, rv[r].y, .0001f) && Vector3.Distance(ln[l], rn[r]) < .0001f;
                }
                Check(incrementalSeams, "Incremental sculpt keeps neighboring mesh heights and lighting normals aligned");
                Check(!WorldTerrainSculptAssets.ApplyMeshDelta(left, Vector3.zero, (x, z) => 0), "Empty edits leave the mesh unchanged");
                return $"{passed.Count} terrain brush checks passed.\n" + string.Join("\n", passed);
            }
            finally
            {
                foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(data);
                UnityEngine.Object.DestroyImmediate(world);
            }
        }
    }
}
