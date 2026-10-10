using UnityEngine;

namespace ElementalGems
{
    public static class ElementalDamage
    {
        public static int Calculate(GemAttack attack, float baseDamage, ElementType defender, float resistance = 1, float healthRatio = 1)
        {
            float execute = attack.executeThreshold > 0 && healthRatio <= attack.executeThreshold ? attack.executeScale : 1;
            return Mathf.Max(0, Mathf.RoundToInt(baseDamage * attack.damageScale * attack.MultiplierAgainst(defender) * Mathf.Max(0, resistance) * execute));
        }
        public static int Hit(GemAttack attack, float baseDamage, GemManager source, Enemy enemy, Vector3 point, Vector3 direction, bool? requestHitReaction = null, bool allowControlEffects = true, float knockbackDurationMultiplier = 1f)
        {
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled) return 0;
            var elemental = enemy.GetComponent<ElementalEnemy>();
            var defender = elemental != null ? elemental.element : ElementType.Normal;
            float resistance = elemental != null ? elemental.Resistance(attack.element) : 1;
            int damage = Calculate(attack, baseDamage, defender, resistance, (float)enemy.CurrentHealth / enemy.MaxHealth);
            int before = enemy.CurrentHealth;
            // Explicit combo permission wins; other impacts interrupt only when they knock back.
            bool knockback = allowControlEffects && WeaponStatModifier.Finite(knockbackDurationMultiplier, 1) > 0 && attack.knockback > (elemental != null ? elemental.knockbackResistance : 0);
            enemy.TakeDamage(damage, requestHitReaction ?? knockback);
            int dealt = before - enemy.CurrentHealth;
            if (dealt > 0)
            {
                if (elemental != null && !enemy.IsDead) elemental.Apply(attack, direction, allowControlEffects, knockbackDurationMultiplier);
                if (source != null) source.RegisterHit(attack, dealt);
                if (attack.MultiplierAgainst(defender) > 1 && resistance > 0) GemFeedback.Show(point, "SUPER EFFECTIVE", attack.color);
            }
            return dealt;
        }
        public static void Impact(GemAttack attack, Vector3 point, Vector3 normal)
        {
            if (attack.impact == null) return;
            var effect = Object.Instantiate(attack.impact, point, Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up));
            Object.Destroy(effect, 4);
        }
        /// <summary>Shared feedback for a confirmed knockback/reaction, including damage-free parries.</summary>
        public static GameObject KnockbackImpact(GemAttack attack, Enemy enemy, Vector3 point, Vector3 direction,
            bool causesKnockback, GameObject fallback = null)
        {
            if (!causesKnockback || enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled) return null;
            GameObject prefab = attack != null && attack.impact != null ? attack.impact : fallback;
            if (prefab == null) return null;
            var effect = Object.Instantiate(prefab, point,
                Quaternion.LookRotation(direction.sqrMagnitude > .001f ? direction : Vector3.up));
            Object.Destroy(effect, 4);
            return effect;
        }
    }
}
