using System.Collections.Generic;
using UnityEngine;

namespace Nusantara.World
{
    public static class WorldStreamingPlan
    {
        public static int Distance(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        public static ChunkTier DesiredTier(WorldDefinition world, Vector2Int chunk, Vector3 player, Vector3 velocity)
        {
            if (!world.Contains(chunk)) return ChunkTier.Unloaded;
            int distance = Distance(chunk, world.WorldToChunk(player));
            if (distance <= world.gameplayRadius) return ChunkTier.Gameplay;
            velocity.y = 0;
            Vector3 ahead = player + Vector3.ClampMagnitude(velocity * world.preloadSeconds, world.maxLookAheadMetres);
            if (distance <= world.visualRadius || Distance(chunk, world.WorldToChunk(ahead)) <= world.gameplayRadius)
                return ChunkTier.Visual;
            return distance <= world.distantRadius ? ChunkTier.Distant : ChunkTier.Unloaded;
        }

        public static ChunkTier RetainedTier(WorldDefinition world, Vector2Int chunk, ChunkTier previous, Vector3 player, Vector3 velocity)
        {
            if (!world.Contains(chunk)) return ChunkTier.Unloaded;
            var desired = DesiredTier(world, chunk, player, velocity);
            int distance = Distance(chunk, world.WorldToChunk(player));
            // Separate retention bands prevent both outer unload thrashing and detail churn.
            if (desired == ChunkTier.Unloaded && previous != ChunkTier.Unloaded && distance <= world.unloadRadius)
                return ChunkTier.Distant;
            if (desired < ChunkTier.Visual && previous >= ChunkTier.Visual && distance <= world.visualRadius + 1)
                return ChunkTier.Visual;
            return desired;
        }

        public static void GetCandidates(WorldDefinition world, Vector3 player, HashSet<Vector2Int> output)
        {
            output.Clear();
            var center = world.WorldToChunk(player);
            int radius = Mathf.Max(world.distantRadius, world.gameplayRadius + Mathf.CeilToInt(world.maxLookAheadMetres / world.chunkSize) + 1);
            for (int z = -radius; z <= radius; z++)
                for (int x = -radius; x <= radius; x++)
                {
                    var coordinate = center + new Vector2Int(x, z);
                    if (world.Contains(coordinate)) output.Add(coordinate);
                }
        }
    }
}
