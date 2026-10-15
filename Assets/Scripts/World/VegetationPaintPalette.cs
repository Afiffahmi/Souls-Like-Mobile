using UnityEngine;
namespace Nusantara.World
{
    [CreateAssetMenu(menuName="Nusantara/Vegetation Paint Palette")]
    public sealed class VegetationPaintPalette : ScriptableObject
    {
        public GameObject treePrefab;
        public GameObject grassPrefab;
        public Material leafMaterial;
        public VegetationBrushStyle[] treeBrushes = System.Array.Empty<VegetationBrushStyle>();
        public VegetationBrushStyle[] grassBrushes = System.Array.Empty<VegetationBrushStyle>();
        public Vector2 treeHeight = new Vector2(8,18);
        public Vector2 grassHeight = new Vector2(.15f,.5f);
        [Min(.1f)] public float treeSpacing = 5;
        [Min(.1f)] public float grassSpacing = .55f;
        [Min(.1f)] public float treeCollisionRadius = .35f;
    }
}
