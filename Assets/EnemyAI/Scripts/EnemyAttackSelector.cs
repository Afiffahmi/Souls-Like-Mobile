using UnityEngine;

namespace SoulsLike.Enemies
{
    // Override for boss sequences/phase logic. Keep runtime counters on the brain or a component.
    public abstract class EnemyAttackSelector : ScriptableObject
    {
        public abstract int Select(EnemyBrain enemy, EnemyTarget target);
    }
}
