using System;
using UnityEngine;

namespace Nusantara.World
{
    public static class WorldRegionGridLayout
    {
        // Three regions per row. Each row uses its tallest region; widths are independent.
        public static Vector2Int Pack(WorldRegion[] regions)
        {
            if (regions == null || (regions.Length != 6 && regions.Length != 8)) throw new ArgumentException("Six mainland regions or eight total regions are required.");
            int width = 0, z = 0;
            for (int row = 0; row < 2; row++)
            {
                int x = 0, height = 0;
                for (int column = 0; column < 3; column++)
                {
                    int id = row * 3 + column + 1;
                    var region = Array.Find(regions, r => r != null && r.id == id);
                    if (region == null || region.chunkGridSize.x < 1 || region.chunkGridSize.y < 1)
                        throw new ArgumentException($"Region {id} requires positive chunk dimensions.");
                    region.chunkOffset = new Vector2Int(x, z);
                    x += region.chunkGridSize.x;
                    height = Mathf.Max(height, region.chunkGridSize.y);
                }
                width = Mathf.Max(width, x);
                z += height;
            }
            if (regions.Length == 8)
            {
                int x = 0, height = 0; z += 4; // Open air separates the islands from the mainland.
                for (int id = 7; id <= 8; id++)
                {
                    var region = Array.Find(regions, r => r != null && r.id == id);
                    if (region == null || region.chunkGridSize.x < 1 || region.chunkGridSize.y < 1) throw new ArgumentException("Island dimensions must be positive.");
                    region.chunkOffset = new Vector2Int(x, z); x += region.chunkGridSize.x + 2; height = Mathf.Max(height, region.chunkGridSize.y);
                }
                width = Mathf.Max(width, x - 2); z += height;
            }
            return new Vector2Int(width, z);
        }
    }
}
