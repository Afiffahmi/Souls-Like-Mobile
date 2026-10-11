using UnityEngine;

namespace SoulsLike.Enemies
{
    /// <summary>Shared, stateless behavior triggered by an attack's animation frame.</summary>
    public abstract class EnemyAnimationAction : ScriptableObject
    {
        public abstract bool IsConfigured { get; }
        public virtual bool CanStart(EnemyBrain source) => IsConfigured && source != null &&
            source.isActiveAndEnabled && source.Health != null && !source.Health.IsDead;
        public abstract void Execute(EnemyAttackContext context);
    }
}
