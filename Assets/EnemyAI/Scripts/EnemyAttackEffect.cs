using UnityEngine;

namespace SoulsLike.Enemies
{
    public readonly struct EnemyAttackContext
    {
        public readonly EnemyBrain source;
        public readonly EnemyTarget target;
        public readonly EnemyAttackDefinition attack;
        public EnemyAttackContext(EnemyBrain source, EnemyTarget target, EnemyAttackDefinition attack)
        { this.source = source; this.target = target; this.attack = attack; }
    }
    public abstract class EnemyAttackEffect : ScriptableObject
    {
        // Called at most once per attack; shared assets must never keep per-enemy state.
        public abstract void Execute(EnemyAttackContext context);
    }
}
