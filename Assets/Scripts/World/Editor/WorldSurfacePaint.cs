using System;
using UnityEditor;
using UnityEngine;

namespace Nusantara.World.Editor
{
    public static class WorldSurfacePaint
    {
        public const string Root = "Assets/NusantaraWorld/SurfacePaint";
        public const string ShaderName = "Nusantara/Painted Regional Ground";
        public const string PalettePath = Root + "/NusantaraSurfaces.asset";

        public static WorldSurfacePalette EnsurePalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<WorldSurfacePalette>(PalettePath);
            if (palette != null) return palette;
            Region5EnvironmentAssets.EnsureFolder(Root);
            palette = ScriptableObject.CreateInstance<WorldSurfacePalette>();
            void Add(string label, string hex, float variation, float size)
            {
                ColorUtility.TryParseHtmlString(hex, out var color);
                palette.styles.Add(new WorldSurfaceStyle { name = label, color = color, variation = variation, patternSize = size });
            }
            Add("Travel road - packed earth", "#9D8059", .14f, 3);
            Add("Sand - coast and riverbank", "#C8B485", .10f, 2);
            Add("Soil - forest floor", "#795338", .23f, 3);
            Add("Mud - Bujang Senang wetlands", "#4C4833", .18f, 4);
            Add("Gravel - mountain trail", "#858078", .35f, 1.5f);
            Add("Moss - ancient forest", "#526438", .28f, 4);
            Add("Stone - ruins and courtyards", "#92938A", .17f, 5);
            Add("Volcanic ash - scorched earth", "#484348", .25f, 2);
            Add("Red earth - Johor trail", "#9C583D", .22f, 3);
            Add("Dark soil - corrupted land", "#43354F", .23f, 4);
            AssetDatabase.CreateAsset(palette, PalettePath);
            AssetDatabase.SaveAssetIfDirty(palette);
            return palette;
        }

        public static Color SurfaceColor(WorldSurfaceStyle style, Vector2 point)
        {
            float size = Mathf.Max(.1f, style.patternSize);
            Color color = style.color;
            if (style.texture != null)
            {
                if (!style.texture.isReadable) throw new InvalidOperationException("Enable Read/Write on the custom surface texture, then Apply its import settings.");
                color *= style.texture.GetPixelBilinear(Mathf.Repeat(point.x / size, 1), Mathf.Repeat(point.y / size, 1));
            }
            float noise = .7f * Mathf.PerlinNoise(point.x / size, point.y / size)
                + .3f * Mathf.PerlinNoise(point.x / size * 4 + 31, point.y / size * 4 + 17);
            color *= 1 + (noise * 2 - 1) * Mathf.Clamp(style.variation, 0, .6f);
            color.a = 1;
            return color;
        }

        public static bool Stamp(Texture2D map, Rect bounds, Vector2 center, float radius,
            float strength, float hardness, WorldSurfaceStyle style, bool erase)
        {
            if (map == null || !map.isReadable || radius <= 0 || strength <= 0 || bounds.width <= 0 || bounds.height <= 0) return false;
            if (!erase && style == null) throw new ArgumentNullException(nameof(style));
            int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x-radius-bounds.xMin)/bounds.width*map.width), 0, map.width);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((center.y-radius-bounds.yMin)/bounds.height*map.height), 0, map.height);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x+radius-bounds.xMin)/bounds.width*map.width), 0, map.width);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((center.y+radius-bounds.yMin)/bounds.height*map.height), 0, map.height);
            int width = x1-x0, height = z1-z0;
            if (width <= 0 || height <= 0) return false;
            var pixels = map.GetPixels(x0, z0, width, height);
            bool changed = false;
            for (int z=0; z<height; z++) for (int x=0; x<width; x++)
            {
                var p = new Vector2(bounds.xMin+(x+x0+.5f)*bounds.width/map.width, bounds.yMin+(z+z0+.5f)*bounds.height/map.height);
                float amount = Mathf.Clamp01(strength) * WorldTerrainBrushMath.Falloff(Vector2.Distance(p, center), radius, hardness);
                if (amount <= 0) continue;
                int index = z*width+x;
                // The map stores linear premultiplied RGB, with coverage in A.
                var target = erase ? Color.clear : SurfaceColor(style,p).linear;
                pixels[index] = Color.Lerp(pixels[index], target, amount);
                changed = true;
            }
            if (changed) map.SetPixels(x0,z0,width,height,pixels);
            return changed;
        }

        public static Rect Bounds(MeshCollider collider)
        {
            var b = collider.bounds;
            return new Rect(b.min.x, b.min.z, b.size.x, b.size.z);
        }

        public static Texture2D Prepare(Material material, Rect bounds, int region)
        {
            if (material == null || material.shader == null) throw new InvalidOperationException("The regional ground needs a material.");
            if (material.shader.name != "Standard" && material.shader.name != "Nusantara/Environment/Toon Standard" && material.shader.name != "Nusantara/World Scale Road" && material.shader.name != ShaderName)
                throw new InvalidOperationException("This ground uses an unsupported shader. The brush supports the regional Standard and Nusantara Toon ground materials.");
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("The painted ground shader is unavailable. Check the Console.");
            Region5EnvironmentAssets.EnsureFolder(Root);
            string path = Root + "/Region" + region + "Surfaces.asset";
            var map = material.HasProperty("_SurfacePaint") ? material.GetTexture("_SurfacePaint") as Texture2D : null;
            if (map == null) map = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (map == null)
            {
                map = new Texture2D(1024, 1024, TextureFormat.RGBA32, true, true)
                { name = "Region " + region + " surfaces", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 2 };
                map.SetPixels32(new Color32[1024*1024]); map.Apply();
                AssetDatabase.CreateAsset(map,path);
            }
            if (!map.isReadable) throw new InvalidOperationException("The surface paint map must remain readable for editing.");
            var rect = new Vector4(bounds.x,bounds.y,bounds.width,bounds.height);
            if (material.shader != shader || material.GetTexture("_SurfacePaint") != map || material.GetVector("_SurfaceRect") != rect)
            {
                Undo.RegisterCompleteObjectUndo(material,"Enable ground painting");
                material.shader = shader;
                material.SetTexture("_SurfacePaint",map);
                material.SetVector("_SurfaceRect",rect);
                EditorUtility.SetDirty(material);
            }
            return map;
        }
    }
}
