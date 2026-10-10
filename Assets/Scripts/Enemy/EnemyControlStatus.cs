using UnityEngine;

namespace SoulsLike.Enemies
{
    public abstract class EnemyControlStatus : MonoBehaviour
    {
        public abstract bool CannotAct { get; }
        public abstract float MovementMultiplier { get; }
        public virtual void ClearOnDeath() { }
    }
}
