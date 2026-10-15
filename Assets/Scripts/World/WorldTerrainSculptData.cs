using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nusantara.World
{
    /// <summary>Authored height offsets on one world-space lattice, shared by every chunk and LOD.</summary>
    [CreateAssetMenu(menuName = "Nusantara/Terrain Sculpt Data")]
    public sealed class WorldTerrainSculptData : ScriptableObject, ISerializationCallbackReceiver
    {
        public const int TileSize = 16;
        public Vector2 origin;
        [Min(.01f)] public float spacing = 8;
        [Tooltip("Chunks whose generated terrain needs to be refreshed after an edit.")]
        public List<Vector2Int> pendingChunks = new List<Vector2Int>();
        [HideInInspector] public List<WorldTerrainShape> shapes = new List<WorldTerrainShape>();

        [Serializable]
        sealed class Tile
        {
            public Vector2Int coordinate;
            public float[] samples = new float[TileSize * TileSize];
        }

        [SerializeField, HideInInspector] List<Tile> tiles = new List<Tile>();
        [NonSerialized] Dictionary<Vector2Int, Tile> lookup;

        public int TileCount { get { EnsureCache(); return lookup.Count; } }

        public float Get(int x, int z)
        {
            EnsureCache();
            var key = new Vector2Int(TileCoordinate(x), TileCoordinate(z));
            if (!lookup.TryGetValue(key, out var tile)) return 0;
            float value = tile.samples[LocalCoordinate(z) * TileSize + LocalCoordinate(x)];
            return IsFinite(value) ? value : 0;
        }

        public void Set(int x, int z, float value)
        {
            EnsureCache();
            if (!IsFinite(value)) value = 0;
            var key = new Vector2Int(TileCoordinate(x), TileCoordinate(z));
            if (!lookup.TryGetValue(key, out var tile))
            {
                if (value == 0) return;
                tile = new Tile { coordinate = key };
                tiles.Add(tile);
                lookup.Add(key, tile);
            }
            int index = LocalCoordinate(z) * TileSize + LocalCoordinate(x);
            if (tile.samples[index] == value) return;
            tile.samples[index] = value;
            // Erasing the last sample releases the tile instead of growing the asset forever.
            if (value != 0) return;
            for (int i = 0; i < tile.samples.Length; i++)
                if (tile.samples[i] != 0 && IsFinite(tile.samples[i])) return;
            lookup.Remove(key);
            tiles.Remove(tile);
        }

        public float Sample(float worldX, float worldZ)
        {
            if (!IsFinite(worldX) || !IsFinite(worldZ)) return 0;
            double step = ValidSpacing;
            double x = ((double)worldX - ValidOriginX) / step;
            double z = ((double)worldZ - ValidOriginZ) / step;
            double floorX = Math.Floor(x), floorZ = Math.Floor(z);
            // Leave space for the right/top interpolation neighbours, and avoid overflow.
            if (floorX < int.MinValue || floorX >= int.MaxValue || floorZ < int.MinValue || floorZ >= int.MaxValue) return 0;
            int ix = (int)floorX, iz = (int)floorZ;
            double tx = x - floorX, tz = z - floorZ;
            double lower = Get(ix, iz) * (1 - tx) + Get(ix + 1, iz) * tx;
            double upper = Get(ix, iz + 1) * (1 - tx) + Get(ix + 1, iz + 1) * tx;
            return (float)(lower * (1 - tz) + upper * tz);
        }

        public Vector2 Position(int x, int z) => new Vector2(
            FinitePosition(ValidOriginX + (double)x * ValidSpacing),
            FinitePosition(ValidOriginZ + (double)z * ValidSpacing));

        // Call after Undo/Redo or replacing serialized data: cached Tile instances may be stale.
        public void InvalidateCache() => lookup = null;
        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() => InvalidateCache();

        void OnEnable() { ValidateConfiguration(); InvalidateCache(); }
        void OnValidate() { ValidateConfiguration(); InvalidateCache(); }

        void ValidateConfiguration()
        {
            spacing = ValidSpacing;
            origin = new Vector2(ValidOriginX, ValidOriginZ);
            if (pendingChunks == null) pendingChunks = new List<Vector2Int>();
            if (shapes == null) shapes = new List<WorldTerrainShape>();
        }

        void EnsureCache()
        {
            if (lookup != null) return;
            lookup = new Dictionary<Vector2Int, Tile>();
            if (tiles == null) tiles = new List<Tile>();
            foreach (var tile in tiles)
            {
                if (tile == null) continue;
                if (tile.samples == null || tile.samples.Length != TileSize * TileSize)
                {
                    var repaired = new float[TileSize * TileSize];
                    if (tile.samples != null) Array.Copy(tile.samples, repaired, Math.Min(tile.samples.Length, repaired.Length));
                    tile.samples = repaired;
                }
                lookup[tile.coordinate] = tile;
            }
        }

        float ValidSpacing => IsFinite(spacing) && spacing > 0 ? Mathf.Max(.01f, spacing) : 8;
        float ValidOriginX => IsFinite(origin.x) ? origin.x : 0;
        float ValidOriginZ => IsFinite(origin.y) ? origin.y : 0;
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static float FinitePosition(double value) => (float)Math.Max(-float.MaxValue, Math.Min(float.MaxValue, value));
        static int TileCoordinate(int value) => value / TileSize - (value % TileSize < 0 ? 1 : 0);
        static int LocalCoordinate(int value) { int remainder = value % TileSize; return remainder < 0 ? remainder + TileSize : remainder; }
    }
}
