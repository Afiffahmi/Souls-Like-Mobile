using UnityEngine;

namespace SoulsLike.Enemies
{
    [RequireComponent(typeof(ElementalGems.ElementalEnemy))]
    public sealed class ElementalEnemyControl : EnemyControlStatus
    {
        private ElementalGems.ElementalEnemy status;
        private void Awake() => status = GetComponent<ElementalGems.ElementalEnemy>();
        public override bool CannotAct => status != null && status.IsStunned;
        public override float MovementMultiplier => status != null ? status.MovementMultiplier : 1;
        public override void ClearOnDeath() { if (status != null) status.ClearStatuses(); }
    }
}
