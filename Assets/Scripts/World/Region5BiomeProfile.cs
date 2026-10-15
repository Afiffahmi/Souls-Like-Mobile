using System;
using UnityEngine;

namespace Nusantara.World
{
    [Serializable]
    public sealed class Region5BiomeRule
    {
        public string name;
        public string[] props = Array.Empty<string>();
        [Min(0)] public int clustersPerChunk = 8;
        public Vector2 scaleRange = new Vector2(.7f, 1.4f);
        [Range(0, 15)] public float maxSlope = 15;
        public Vector2 heightRange = new Vector2(0, 250);
        public Vector2 riverDistance = new Vector2(45, 10000);
        public float pathClearance = 18;
        public float buildingClearance = 16;
        public float minimumSpacing = 4;
        public int seed = 5;
    }
    [CreateAssetMenu(menuName = "Nusantara/Region 5 Biome Profile")]
    public sealed class Region5BiomeProfile : ScriptableObject
    {
        public Region5BiomeRule[] biomes = Array.Empty<Region5BiomeRule>();
        [Min(0)] public float boundaryInset = 30;
        [Range(0, 32)] public int maximumIslands = 24;
        public int seed = 5012026;
    }
}
