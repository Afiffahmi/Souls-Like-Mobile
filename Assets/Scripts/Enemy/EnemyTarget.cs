using System.Collections.Generic;
using UnityEngine;

namespace SoulsLike.Enemies
{
    public abstract class EnemyTarget : MonoBehaviour
    {
        private static readonly HashSet<EnemyTarget> active = new HashSet<EnemyTarget>();
        public static IEnumerable<EnemyTarget> Active => active;
        public abstract bool IsAlive { get; }
        public abstract Vector3 AimPosition { get; }
        public abstract Vector3 GroundPosition { get; }
        public abstract bool ReceiveDamage(int damage, EnemyBrain source);
        public virtual bool TryParryAttack(EnemyBrain source) => false;
        public virtual bool ReceiveDamage(int damage, EnemyBrain source, bool allowParry) => ReceiveDamage(damage, source);
        // Elemental actions carry a snapshot so damage cannot silently re-read a different source state.
        public virtual bool ReceiveElementalDamage(int damage, EnemyBrain source, ElementalGems.ElementType element, bool allowParry) =>
            ReceiveDamage(damage, source, allowParry);
        protected virtual void OnEnable() => active.Add(this);
        protected virtual void OnDisable() => active.Remove(this);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();
    }
}
