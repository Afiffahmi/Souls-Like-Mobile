using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nusantara.World
{
    public enum ChunkTier { Unloaded, Distant, Visual, Gameplay }

    [Serializable]
    public sealed class WorldRegion
    {
        [Range(1, 6)] public int id;
        public string name;
        public string boss;
        public bool detailedEnvironment;
        [Tooltip("World X/Z coordinates traced from the reference. Empty until references arrive.")]
        public Vector2[] boundary = Array.Empty<Vector2>();
    }

    [Serializable]
    public sealed class WorldLocation
    {
        public string id;
        public string name;
        [Range(1, 6)] public int region = 5;
        public bool positionConfirmed;
        public Vector3 position;
        [Min(1)] public float clearRadius = 20;
        [Tooltip("Optional authored world X/Z footprint. Empty keeps the existing circular clearing.")]
        public Vector2[] footprint = Array.Empty<Vector2>();
    }

    [Serializable]
    public sealed class WorldChunkRecord
    {
        public Vector2Int coordinate;
        [Range(1, 6)] public int region = 1;
        [Tooltip("Prefab with terrain, essential collision, water barriers and baked navigation. Local origin is the chunk's southwest corner.")]
        public string essentialAddress;
        [Tooltip("Simplified terrain, without gameplay or collision.")]
        public string distantAddress;
        [Tooltip("Small prefab batches of decorative objects. Essential barriers belong in the essential prefab.")]
        public string[] decorationAddresses = Array.Empty<string>();
        [Tooltip("Spawn zones, interactables and AI. Loaded only near the player, after essentials.")]
        public string[] gameplayAddresses = Array.Empty<string>();
    }

    [CreateAssetMenu(menuName = "Nusantara/World Definition")]
    public sealed class WorldDefinition : ScriptableObject
    {
        public string worldId = "nusantara-quest";
        public int seed = 5012026;
        [Tooltip("Remains false until the six region boundaries and start point match the supplied references.")]
        public bool layoutApproved;
        [Min(1)] public float chunkSize = 256;
        [Tooltip("48 x 48 chunks = 12.288 km square. Configurable independently of content density.")]
        public Vector2Int gridSize = new Vector2Int(48, 48);
        public Vector2 origin = new Vector2(-6144, -6144);
        public Vector3 playerStart;
        public bool playerStartConfirmed;
        [Range(0, 4)] public int gameplayRadius = 1;
        [Range(1, 6)] public int visualRadius = 3;
        [Range(2, 10)] public int distantRadius = 5;
        [Range(3, 12)] public int unloadRadius = 6;
        [Min(0)] public float preloadSeconds = 4;
        [Min(0)] public float maxLookAheadMetres = 256;
        [Range(1, 4)] public int maxConcurrentLoads = 2;
        [Min(.05f)] public float planningInterval = .25f;
        [Min(1)] public float retryDelaySeconds = 10;
        [Range(1, 15)] public float maxWalkableSlope = 15;
        [Range(.01f, .3f)] public float maxStepHeight = .2f;
        [Min(.01f)] public float seamTolerance = .005f;
        [Min(.1f)] public float agentRadius = .5f;
        [Min(.2f)] public float agentHeight = 2;
        public WorldRegion[] regions = Array.Empty<WorldRegion>();
        public WorldLocation[] locations = Array.Empty<WorldLocation>();
        public WorldChunkRecord[] chunks = Array.Empty<WorldChunkRecord>();

        public Vector2Int WorldToChunk(Vector3 position) => new Vector2Int(
            Mathf.FloorToInt((position.x - origin.x) / chunkSize),
            Mathf.FloorToInt((position.z - origin.y) / chunkSize));

        public Vector3 ChunkOrigin(Vector2Int coordinate) => new Vector3(
            origin.x + coordinate.x * chunkSize, 0, origin.y + coordinate.y * chunkSize);

        public bool Contains(Vector2Int coordinate) => coordinate.x >= 0 && coordinate.y >= 0
            && coordinate.x < gridSize.x && coordinate.y < gridSize.y;

        public IEnumerable<string> ConfigurationErrors(bool requireLayout)
        {
            if (string.IsNullOrWhiteSpace(worldId)) yield return "A stable world ID is required for save data.";
            if (!float.IsFinite(chunkSize) || chunkSize <= 0 || gridSize.x <= 0 || gridSize.y <= 0)
                yield return "Chunk size and grid dimensions must be positive.";
            if (gameplayRadius < 0 || visualRadius <= gameplayRadius || distantRadius < visualRadius || unloadRadius <= distantRadius)
                yield return "Require gameplay radius < visual radius <= distant radius < unload radius.";
            if (maxConcurrentLoads < 1 || planningInterval <= 0 || retryDelaySeconds <= 0)
                yield return "Load budget, planning interval and retry delay must be positive.";
            if (maxWalkableSlope <= 0 || maxWalkableSlope > 15 || agentRadius <= 0 || agentHeight < 2 * agentRadius)
                yield return "Walkability settings must respect the 15 degree limit and a valid character capsule.";
            var ids = new HashSet<int>();
            foreach (var region in regions)
            {
                if (region == null || region.id < 1 || region.id > 6 || !ids.Add(region.id))
                { yield return "Region IDs must be unique and within 1–6."; continue; }
                if (region.detailedEnvironment && region.id != 5) yield return "Only Region 5 may have detailed environment content.";
                if (requireLayout && (region.boundary == null || region.boundary.Length < 3))
                    yield return $"Region {region.id} is awaiting a reference boundary.";
            }
            if (ids.Count != 6) yield return "Exactly six region definitions are required; Regions 7 and 8 are excluded.";
            var coordinates = new HashSet<Vector2Int>();
            foreach (var chunk in chunks)
            {
                if (chunk == null) { yield return "Null chunk record."; continue; }
                if (!Contains(chunk.coordinate) || !coordinates.Add(chunk.coordinate)) yield return $"Invalid or duplicate chunk {chunk.coordinate}.";
                if (!ids.Contains(chunk.region)) yield return $"Chunk {chunk.coordinate} references an unknown region.";
                if (string.IsNullOrWhiteSpace(chunk.essentialAddress)) yield return $"Chunk {chunk.coordinate} has no essential content address.";
                if (string.IsNullOrWhiteSpace(chunk.distantAddress)) yield return $"Chunk {chunk.coordinate} has no distant terrain address.";
                if (chunk.region != 5 && (chunk.decorationAddresses.Length > 0 || chunk.gameplayAddresses.Length > 0))
                    yield return $"Region {chunk.region} must remain terrain-only.";
            }
            if (requireLayout)
            {
                if (!layoutApproved) yield return "Reference layout has not been approved. Streaming is intentionally gated.";
                if (!playerStartConfirmed || !Contains(WorldToChunk(playerStart))) yield return "A confirmed player start inside the world is required.";
                bool startInRegionOne = false;
                foreach (var chunk in chunks)
                    if (chunk != null && chunk.coordinate == WorldToChunk(playerStart) && chunk.region == 1) startInRegionOne = true;
                if (!startInRegionOne) yield return "The starting chunk must exist in Region 1.";
            }
        }
    }
}
