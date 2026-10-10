using UnityEngine;
namespace ElementalGems
{
    [CreateAssetMenu(menuName="Combat/Gems/Visual Style")]
    public sealed class GemVfxStyle : ScriptableObject
    {
        public ElementType element;
        public GameObject swordAura, bowAura, arrowheadAura, impact;
        public Material trailMaterial;
        [ColorUsage(true,true)] public Color trailColor=Color.white;
        [Min(.01f)] public float swordTrailWidth=.3f, arrowTrailWidth=.1f, trailDuration=.32f;
        [Header("Global visual tuning (particle modules remain editable in each prefab)")]
        [Range(.1f,3)] public float size=1;
        [Range(.1f,3)] public float speed=1;
        [Range(.1f,3)] public float intensity=1;
        [Range(.1f,3)] public float duration=1;
    }
}
