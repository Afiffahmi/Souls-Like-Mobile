using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nusantara.World
{
    public enum ChunkTier { Unloaded, Distant, Visual, Gameplay }

    [Serializable]
    public sealed class WorldRegion
    {
        [Range(1, 9)] public int id;
        public string name;
        public string boss;
        public bool detailedEnvironment;
        [Tooltip("Size of this region in chunks. Regions can use different dimensions, such as 6x6, 5x5 or 4x4.")]
        public Vector2Int chunkGridSize = new Vector2Int(6, 6);
        [Tooltip("Southwest chunk of this region in the combined world grid.")]
        [HideInInspector] public Vector2Int chunkOffset;
        [Tooltip("One continuous ground surface for this region. Ground colour is on its own material.")]
        public GameObject groundPrefab;
        public bool floatingIsland;
        public bool paintOnly;
        [HideInInspector] public string groundAssetFolder;
        [Tooltip("Height offset for a floating island relative to the mainland ground.")]
        public float groundElevation;
        public VegetationBrushStyle treeBrush;
        public VegetationBrushStyle grassBrush;
        [Tooltip("World X/Z coordinates traced from the reference. Empty until references arrive.")]
        public Vector2[] boundary = Array.Empty<Vector2>();
    }

    [Serializable]
    public sealed class WorldLocation
    {
        public string id;
        public string name;
        [Range(1, 9)] public int region = 5;
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
        [HideInInspector] public string assetFolder;
        [Range(1, 9)] public int region = 1;
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
        [HideInInspector] public bool authoredChunkLayout;
        [Tooltip("Flat foundation authored with brushes; procedural geography is disabled.")]
        public bool brushAuthoredTerrain;
        [Tooltip("Painted mainland ownership. The chunk grids below remain terrain storage; use Region Brush to change regions 1–7.")]
        public WorldRegionPaintData regionPaint;
        [HideInInspector] public bool regionalGroundPlanes;
        [HideInInspector] public string chunkAssetRoot = "Assets/NusantaraWorld/Generated";
        [Min(1)] public float chunkSize = 256;
        [Tooltip("Bounds of the combined chunk grid. Region dimensions and streaming detail are configured independently.")]
        public Vector2Int gridSize = new Vector2Int(48, 48);
        public Vector2 origin = new Vector2(-6144, -6144);
        public Vector3 playerStart;
        public bool playerStartConfirmed;
        [Range(0, 4)] public int gameplayRadius = 1;
        [Range(1, 8)] public int visualRadius = 3;
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
        public int WetlandRegionId => regions.Length == 9 ? 6 : 5;
        public int MainlandRegionCount => regions.Length == 9 ? 7 : 6;
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
            if (gameplayRadius < 0 || (authoredChunkLayout ? visualRadius < gameplayRadius : visualRadius <= gameplayRadius) || distantRadius < visualRadius || unloadRadius <= distantRadius)
                yield return "Require gameplay radius < visual radius <= distant radius < unload radius.";
            if (maxConcurrentLoads < 1 || planningInterval <= 0 || retryDelaySeconds <= 0)
                yield return "Load budget, planning interval and retry delay must be positive.";
            if (maxWalkableSlope <= 0 || maxWalkableSlope > 15 || agentRadius <= 0 || agentHeight < 2 * agentRadius)
                yield return "Walkability settings must respect the 15 degree limit and a valid character capsule.";
            var ids = new HashSet<int>();
            foreach (var region in regions)
            {
                if (region == null || region.id < 1 || region.id > 9 || !ids.Add(region.id))
                { yield return "Region IDs must be unique and within 1–9."; continue; }
                if (!authoredChunkLayout && region.detailedEnvironment && region.id != WetlandRegionId) yield return "Only Region 5 may have detailed environment content.";
                if (requireLayout && !authoredChunkLayout && (region.boundary == null || region.boundary.Length < 3))
                    yield return $"Region {region.id} is awaiting a reference boundary.";
                if (brushAuthoredTerrain && !region.paintOnly && (region.chunkGridSize.x < 1 || region.chunkGridSize.y < 1))
                    yield return $"Region {region.id} requires positive chunk dimensions.";
            }
            if (ids.Count != 6 && ids.Count != 8 && ids.Count != 9) yield return "Define six legacy mainland regions, eight legacy regions, or nine regions including the natural starting area.";
            var coordinates = new HashSet<Vector2Int>();
            foreach (var chunk in chunks)
            {
                if (chunk == null) { yield return "Null chunk record."; continue; }
                if (!Contains(chunk.coordinate) || !coordinates.Add(chunk.coordinate)) yield return $"Invalid or duplicate chunk {chunk.coordinate}.";
                if (!ids.Contains(chunk.region)) yield return $"Chunk {chunk.coordinate} references an unknown region.";
                if (brushAuthoredTerrain)
                {
                    var owner = Array.Find(regions, r => r != null && r.id == chunk.region);
                    if (owner != null && (chunk.coordinate.x < owner.chunkOffset.x || chunk.coordinate.y < owner.chunkOffset.y
                        || chunk.coordinate.x >= owner.chunkOffset.x + owner.chunkGridSize.x || chunk.coordinate.y >= owner.chunkOffset.y + owner.chunkGridSize.y))
                        yield return $"Chunk {chunk.coordinate} lies outside Region {chunk.region}'s grid.";
                }
                if (!regionalGroundPlanes&&string.IsNullOrWhiteSpace(chunk.essentialAddress)) yield return $"Chunk {chunk.coordinate} has no essential content address.";
                if (!regionalGroundPlanes&&string.IsNullOrWhiteSpace(chunk.distantAddress)) yield return $"Chunk {chunk.coordinate} has no distant terrain address.";
                if (!authoredChunkLayout && chunk.region != WetlandRegionId && (chunk.decorationAddresses.Length > 0 || chunk.gameplayAddresses.Length > 0))
                    yield return $"Region {chunk.region} must remain terrain-only.";
            }
            if (brushAuthoredTerrain)
                foreach (var region in regions)
                {
                    if (region == null || region.paintOnly) continue;
                    int count = 0;
                    foreach (var chunk in chunks) if (chunk != null && chunk.region == region.id) count++;
                    if (count != region.chunkGridSize.x * region.chunkGridSize.y)
                        yield return $"Region {region.id} does not contain its complete {region.chunkGridSize.x}x{region.chunkGridSize.y} grid.";
                }
            if(regionalGroundPlanes)foreach(var region in regions)
                if(region!=null&&!region.paintOnly&&(region.groundPrefab==null||region.groundPrefab.GetComponent<WorldRegionGround>()==null))yield return $"Region {region.id} needs its continuous ground prefab.";
            if (requireLayout)
            {
                if (!authoredChunkLayout && !layoutApproved) yield return "Reference layout has not been approved. Streaming is intentionally gated.";
                if (!playerStartConfirmed || !Contains(WorldToChunk(playerStart))) yield return "A confirmed player start inside the world is required.";
                bool startInRegionOne = false;
                foreach (var chunk in chunks)
                    if (chunk != null && chunk.coordinate == WorldToChunk(playerStart) && (authoredChunkLayout || chunk.region == 1)) startInRegionOne = true;
                if (!startInRegionOne) yield return authoredChunkLayout ? "The starting chunk must exist in the authored grid." : "The starting chunk must exist in Region 1.";
            }
        }
    }
}


