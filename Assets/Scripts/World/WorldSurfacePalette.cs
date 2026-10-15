using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nusantara.World
{
    [CreateAssetMenu(menuName = "Nusantara/Surface Palette")]
    public sealed class WorldSurfacePalette : ScriptableObject
    {
        public List<WorldSurfaceStyle> styles = new List<WorldSurfaceStyle>();
    }

    [Serializable]
    public sealed class WorldSurfaceStyle
    {
        [Tooltip("0 = Common; 1 Natural start, 2 Water, 3 Fire, 4 Nature, 5 Earth, 6 Nature + Water, 7 Lightning, 8 Wind, 9 Darkness.")]
        [Range(0, 9)] public int region;
        [HideInInspector] public string assetId;
        public string name = "Custom surface";
        public Color color = new Color(.45f, .32f, .20f);
        [Range(0, .6f)] public float variation = .16f;
        [Min(.1f)] public float patternSize = 3;
        [Tooltip("Optional albedo repeated in world metres. Enable Read/Write on its texture importer for the editor's detail library.")]
        public Texture2D texture;
    }
}
