using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nusantara.World
{
    public static class WorldTerrainMesher
    {
        // Sample the same WORLD lattice on both sides of a boundary. Do not use per-chunk random noise.
        public static Vector2 SamplePosition(WorldDefinition world, Vector2Int chunk, int x, int z, int intervals)
        {
            return new Vector2(
                (float)(world.origin.x + ((double)chunk.x * intervals + x) * world.chunkSize / intervals),
                (float)(world.origin.y + ((double)chunk.y * intervals + z) * world.chunkSize / intervals));
        }
        public static Mesh Build(WorldDefinition world, Vector2Int chunk, int intervals, Func<float, float, float> height)
        {
            if (world == null || height == null || intervals < 1 || intervals > 256) throw new ArgumentException("Invalid terrain mesh inputs.");
            int side = intervals + 1;
            var vertices = new Vector3[side * side];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[intervals * intervals * 6];
            var origin = world.ChunkOrigin(chunk);
            // A fixed derivative spacing makes edge normals identical even across different resolutions.
            const float derivativeStep = .5f;
            for (int z = 0; z < side; z++)
                for (int x = 0; x < side; x++)
                {
                    int index = z * side + x;
                    var sample = SamplePosition(world, chunk, x, z, intervals);
                    vertices[index] = new Vector3(sample.x - origin.x, height(sample.x, sample.y), sample.y - origin.z);
                    float dx = height(sample.x + derivativeStep, sample.y) - height(sample.x - derivativeStep, sample.y);
                    float dz = height(sample.x, sample.y + derivativeStep) - height(sample.x, sample.y - derivativeStep);
                    normals[index] = new Vector3(-dx, 2 * derivativeStep, -dz).normalized;
                    uv[index] = sample / 32f;
                    if (x == intervals || z == intervals) continue;
                    int t = (z * intervals + x) * 6;
                    triangles[t] = index; triangles[t + 1] = index + side; triangles[t + 2] = index + 1;
                    triangles[t + 3] = index + 1; triangles[t + 4] = index + side; triangles[t + 5] = index + side + 1;
                }
            var mesh = new Mesh { name = $"Terrain_{chunk.x}_{chunk.y}_{intervals}", indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
