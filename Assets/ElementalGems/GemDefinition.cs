using System;
using UnityEngine;

namespace ElementalGems
{
    public enum ElementType { Normal, Fire, Water, Nature, Earth, Lightning, Wind, Darkness }
    public enum StatusKind { Burn, Slow, Root, Poison, Stagger, Stun, Void }
    [Serializable] public struct ElementMatchup { public ElementType defender; [Min(0)] public float multiplier; }
    [Serializable] public struct StatusSpec
    {
        public StatusKind kind;
        [Range(0, 1)] public float chance;
        [Min(0)] public float duration;
        [Tooltip("Damage per second for Burn/Poison/Void; speed reduction (0–1) for Slow.")]
        [Min(0)] public float magnitude;
    }
    [CreateAssetMenu(menuName = "Combat/Gems/Gem Definition")]
    public sealed class GemDefinition : ScriptableObject
    {
        public ElementType element;
        public string displayName;
        [TextArea(3, 6)] public string description;
        public Sprite icon;
        [ColorUsage(true, true)] public Color color = Color.white;
        [Header("Shared sword and bow damage")]
        [Min(0)] public float damageScale = 1;
        public ElementMatchup[] matchups = Array.Empty<ElementMatchup>();
        public StatusSpec[] statuses = Array.Empty<StatusSpec>();
        [Min(0)] public float knockback = 0;
        [Header("Shared abilities")]
        [Range(0, 0.9f)] public float armorReduction;
        [Min(0.1f)] public float movementScale = 1;
        [Min(0.1f)] public float projectileSpeedScale = 1;
        [Min(0)] public int healingOnHit;
        [Range(0, 1)] public float lifeSteal;
        [Min(0)] public float comboDamagePerHit;
        [Min(1)] public int maxComboStacks = 5;
        [Min(0.1f)] public float comboTimeout = 2;
        [Range(0, 1)] public float executeHealthThreshold;
        [Min(1)] public float executeDamageScale = 1;
        [Header("Reusable VFX prefabs")]
        public GameObject swordAura;
        public GameObject bowAura;
        public GameObject impact;
        public Material trailMaterial;
        [Min(0.01f)] public float swordTrailWidth = 0.25f;
        [Min(0.01f)] public float arrowTrailWidth = 0.07f;
        [Min(0.01f)] public float trailLifetime = 0.25f;
        public float MultiplierAgainst(ElementType defender)
        {
            if (element == ElementType.Normal) return 1;
            foreach (var row in matchups) if (row.defender == defender) return Mathf.Max(0, row.multiplier);
            return 1;
        }
    }
    /// <summary>Values copied at release: later gem changes never mutate an in-flight attack.</summary>
    public sealed class GemAttack
    {
        public readonly ElementType element;
        public readonly float damageScale, knockback, lifeSteal, executeThreshold, executeScale, projectileSpeed;
        public readonly int healing;
        public readonly StatusSpec[] statuses;
        public readonly ElementMatchup[] matchups;
        public readonly GameObject impact;
        public readonly Material trailMaterial;
        public readonly Color color;
        public readonly float trailWidth, trailLifetime;
        public GemAttack(GemDefinition gem, float comboScale = 1)
        {
            element = gem != null ? gem.element : ElementType.Normal;
            bool normal = element == ElementType.Normal;
            damageScale = normal ? 1 : Mathf.Max(0, gem.damageScale * comboScale);
            knockback = normal ? 0 : gem.knockback;
            healing = normal ? 0 : gem.healingOnHit;
            lifeSteal = normal ? 0 : gem.lifeSteal;
            executeThreshold = normal ? 0 : gem.executeHealthThreshold;
            executeScale = normal ? 1 : gem.executeDamageScale;
            projectileSpeed = normal ? 1 : Mathf.Max(0.1f, gem.projectileSpeedScale);
            statuses = normal ? Array.Empty<StatusSpec>() : (StatusSpec[])gem.statuses.Clone();
            matchups = normal ? Array.Empty<ElementMatchup>() : (ElementMatchup[])gem.matchups.Clone();
            impact = normal ? null : gem.impact;
            trailMaterial = normal ? null : gem.trailMaterial;
            color = normal ? Color.white : gem.color;
            trailWidth = normal ? 0 : gem.arrowTrailWidth;
            trailLifetime = normal ? 0 : gem.trailLifetime;
        }
        public float MultiplierAgainst(ElementType defender)
        {
            foreach (var row in matchups) if (row.defender == defender) return Mathf.Max(0, row.multiplier);
            return 1;
        }
    }
}
