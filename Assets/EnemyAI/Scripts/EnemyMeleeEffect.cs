using UnityEngine;

namespace SoulsLike.Enemies
{
    [CreateAssetMenu(menuName = "Enemies/Effects/Melee")]
    public sealed class EnemyMeleeEffect : EnemyAttackEffect
    {
        public override void Execute(EnemyAttackContext context)
        {
            if (context.target != null && context.source.CanHit(context.target, context.attack))
                context.target.ReceiveDamage(context.attack.damage, context.source, !context.attack.damageAtWindowEnd);
        }
    }
}
