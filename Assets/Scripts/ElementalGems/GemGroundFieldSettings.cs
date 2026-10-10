using UnityEngine;

namespace ElementalGems
{
    [CreateAssetMenu(menuName = "Combat/Gems/Bow Ground Field Settings")]
    public sealed class GemGroundFieldSettings : ScriptableObject
    {
        [Header("Lingering damage after the heavy arrow's impact burst")]
        [Min(.1f)] public float duration = 4;
        [Min(0)] public float damagePerSecond = 12;
        [Min(.1f)] public float tickInterval = .5f;
        [Tooltip("Height above the impact surface that enemies can occupy.")]
        [Min(.1f)] public float damageHeight = 2.5f;
        public LayerMask enemyLayers = ~0;
        public bool requireLineOfSight = true;
        [Tooltip("Solid geometry that blocks field damage. Character colliders are ignored.")]
        public LayerMask obstacleLayers = ~0;
        [Range(1, 12)] public int maxFieldsPerPlayer = 4;
        [Tooltip("Nearby fields of the same element refresh instead of multiplying damage from a volley.")]
        [Range(0, 1)] public float mergeDistanceFraction = .35f;
        [Tooltip("Index order: Normal, Fire, Water, Nature, Earth, Lightning, Wind, Darkness.")]
        public GemGroundField[] elementPrefabs = new GemGroundField[8];

        public GroundFieldSnapshot Capture(ElementType element, float radius, float damageMultiplier = 1f, float knockbackDurationMultiplier = 1f) => new GroundFieldSnapshot(this, element, radius, damageMultiplier, knockbackDurationMultiplier);
    }

    /// <summary>Immutable launch-time settings; gem switches cannot retint or change a flying heavy arrow.</summary>
    public sealed class GroundFieldSnapshot
    {
        public readonly float radius, duration, damagePerSecond, tickInterval, height, mergeFraction;
        public readonly float knockbackDurationMultiplier;
        public readonly int enemyLayers, obstacleLayers, maxFields;
        public readonly bool lineOfSight;
        public readonly GemGroundField prefab;
        public GroundFieldSnapshot(GemGroundFieldSettings settings, ElementType element, float radius, float damageMultiplier = 1f, float knockbackDurationMultiplier = 1f)
        {
            this.knockbackDurationMultiplier = Mathf.Clamp(WeaponStatModifier.Finite(knockbackDurationMultiplier, 1), 0, 4);
            this.radius = Mathf.Max(.1f, radius);
            duration = Mathf.Max(.1f, settings.duration);
            damagePerSecond = Mathf.Max(0, settings.damagePerSecond) * Mathf.Clamp(WeaponStatModifier.Finite(damageMultiplier, 1), 0, 100);
            tickInterval = Mathf.Max(.1f, settings.tickInterval);
            height = Mathf.Max(.1f, settings.damageHeight);
            mergeFraction = Mathf.Clamp01(settings.mergeDistanceFraction);
            enemyLayers = settings.enemyLayers; obstacleLayers = settings.obstacleLayers;
            maxFields = Mathf.Max(1, settings.maxFieldsPerPlayer); lineOfSight = settings.requireLineOfSight;
            int index = (int)element;
            prefab = settings.elementPrefabs != null && index < settings.elementPrefabs.Length ? settings.elementPrefabs[index] : null;
        }
    }
}
