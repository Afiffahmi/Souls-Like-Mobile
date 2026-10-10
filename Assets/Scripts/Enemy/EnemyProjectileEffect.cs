using UnityEngine;

namespace SoulsLike.Enemies
{
    [CreateAssetMenu(menuName = "Enemies/Effects/Projectile or Spell")]
    public sealed class EnemyProjectileEffect : EnemyAttackEffect
    {
        public EnemyProjectile prefab;
        [Min(.1f)] public float speed = 15;
        [Min(.1f)] public float lifetime = 5;
        public override void Execute(EnemyAttackContext context)
        {
            if (prefab == null || context.target == null) return;
            Vector3 direction = context.target.AimPosition - context.source.AttackPosition;
            var projectile = Instantiate(prefab, context.source.AttackPosition, Quaternion.LookRotation(direction));
            projectile.Launch(context.source, direction.normalized * speed, context.attack.damage, lifetime);
        }
    }
}
