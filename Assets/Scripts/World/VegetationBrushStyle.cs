using UnityEngine;
namespace Nusantara.World
{
    [CreateAssetMenu(menuName="Nusantara/Vegetation Brush Style")]
    public sealed class VegetationBrushStyle : ScriptableObject
    {
        public string displayName;
        public VegetationPaintKind kind;
        public GameObject prefab;
        [Tooltip("Optional grass colour material. Trees use the materials in their chosen prefab.")]
        public Material grassMaterial;
        [Tooltip("Add the optional particle leaves only when this tree prefab is a bare trunk.")]
        public bool addParticleCanopy;
        public Vector2 height = new Vector2(.32f,.65f);
        [Min(.1f)] public float spacing = .32f;
        [Min(.1f)] public float trunkRadius = .8f;
        public string Label => string.IsNullOrWhiteSpace(displayName)?name:displayName;
    }
}
