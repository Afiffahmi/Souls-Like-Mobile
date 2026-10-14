using UnityEngine;

namespace Nusantara.World
{
    public sealed class WorldRegionPaintData : ScriptableObject
    {
        public Texture2D map;
        public Rect bounds;
        public float cellSize = 4;
        public Color[] colors = new Color[7];
        [HideInInspector] public byte[] savedPixels;
        [HideInInspector] public int revision;

        public void CapturePixels() { savedPixels = map.GetRawTextureData(); revision++; }
        public void RestorePixels()
        {
            if(map == null || savedPixels == null || savedPixels.Length != map.width * map.height * 4)return;
            map.LoadRawTextureData(savedPixels);map.Apply(false);
        }

        public int RegionAt(Vector2 p)
        {
            if (map == null || !bounds.Contains(p)) return 0;
            int x = Mathf.FloorToInt((p.x - bounds.xMin) / bounds.width * map.width);
            int z = Mathf.FloorToInt((p.y - bounds.yMin) / bounds.height * map.height);
            return Mathf.RoundToInt(map.GetPixel(x, z).a * 255);
        }

        public bool Inside(Vector2 p, int id, float inset = 0)
        {
            if (RegionAt(p) != id) return false;
            if (inset <= 0) return true;
            if (!bounds.Contains(p - Vector2.one * inset) || !bounds.Contains(p + Vector2.one * inset)) return false;
            int x0 = Mathf.FloorToInt((p.x - inset - bounds.xMin) / cellSize);
            int z0 = Mathf.FloorToInt((p.y - inset - bounds.yMin) / cellSize);
            int x1 = Mathf.FloorToInt((p.x + inset - bounds.xMin) / cellSize);
            int z1 = Mathf.FloorToInt((p.y + inset - bounds.yMin) / cellSize);
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
            {
                var rect = new Rect(bounds.xMin + x * cellSize, bounds.yMin + z * cellSize, cellSize, cellSize);
                var nearest = new Vector2(Mathf.Clamp(p.x, rect.xMin, rect.xMax), Mathf.Clamp(p.y, rect.yMin, rect.yMax));
                if ((nearest - p).sqrMagnitude <= inset * inset && Mathf.RoundToInt(map.GetPixel(x, z).a * 255) != id) return false;
            }
            return true;
        }

        public Color32 Pixel(int id)
        {
            if (id < 1 || id > colors.Length) return new Color32(0, 0, 0, 0);
            Color32 c = colors[id - 1].linear;
            c.a = (byte)id;
            return c;
        }
    }
}
