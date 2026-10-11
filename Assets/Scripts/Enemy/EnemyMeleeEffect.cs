using UnityEngine;

namespace SoulsLike.Enemies
{
    [CreateAssetMenu(menuName = "Enemies/Effects/Melee")]
    public sealed class EnemyMeleeEffect : EnemyAttackEffect
    {
        public override void Execute(EnemyAttackContext context)
        {
            if (context.target == null || !context.source.CanHit(context.target, context.attack)) return;
            bool parry = context.attack.canBeParried && !context.attack.damageAtWindowEnd;
            var elemental = context.source.GetComponent<ElementalGems.ElementalEnemy>();
            if (context.attack.applyElementToPlayer && elemental != null)
                context.target.ReceiveElementalDamage(context.attack.damage, context.source, elemental.CurrentElement, parry);
            else context.target.ReceiveDamage(context.attack.damage, context.source, parry);
        }
    }
}
