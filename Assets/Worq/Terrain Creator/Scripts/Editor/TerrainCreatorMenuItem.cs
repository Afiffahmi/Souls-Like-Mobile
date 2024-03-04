using UnityEditor;
using UnityEngine;

namespace Terrain_Generator.Editor
{
    public class TerrainCreatorMenuItem : EditorWindow
    {
        [MenuItem("Tools/Worq/Terrain Creator/Create Terrain")]
        public static void CreateTerrain()
        {
            var go = new GameObject("New Terrain");

            var terrainCreator = go.AddComponent<TerrainCreator>();

            terrainCreator.TerrainSystem.Interpolation = 0.6f;
            terrainCreator.SetDefaultBiotypes();

            terrainCreator.TerrainSystem.SetHeightMap(terrainCreator.TerrainSystem.CreateHeightMap(
                terrainCreator.Seed, terrainCreator.Scale, terrainCreator.Octaves, terrainCreator.Persistance,
                terrainCreator.Lacunarity, terrainCreator.FalloffStrength, terrainCreator.FalloffRamp,
                terrainCreator.FalloffRange, terrainCreator.Offset, terrainCreator.HeightMultiplier,
                terrainCreator.HeightCurve
            ));

            terrainCreator.TerrainSystem.SetColorMap(terrainCreator.TerrainSystem.CreateColorMap());
        }
    }
}