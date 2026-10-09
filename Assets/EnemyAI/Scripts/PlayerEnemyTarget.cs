using UnityEngine;

namespace SoulsLike.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerOverall))]
    public sealed class PlayerEnemyTarget : EnemyTarget
    {
        public bool evadeDuringRoll = true;
        public bool parryEnabled = true;
        public Vector2 parryWindow = new Vector2(.1f, .55f);
        private PlayerOverall health;
        private PlayerStateManager player;
        private CharacterController body;
        private void Awake() { health = GetComponent<PlayerOverall>(); player = GetComponent<PlayerStateManager>(); body = GetComponent<CharacterController>(); }
        public override bool IsAlive => isActiveAndEnabled && health != null && health.currentHealth > 0;
        public override Vector3 AimPosition => body != null && body.enabled ? body.bounds.center : transform.position + Vector3.up;
        public override Vector3 GroundPosition => body != null && body.enabled ? new Vector3(body.bounds.center.x, body.bounds.min.y, body.bounds.center.z) : transform.position;
        public override bool ReceiveDamage(int damage, EnemyBrain source)
        {
            if (!IsAlive || damage <= 0 || (evadeDuringRoll && player != null && player.IsRolling)) return false;
            if (parryEnabled && player != null && player.IsParrying && player.ParryNormalizedTime >= parryWindow.x && player.ParryNormalizedTime <= parryWindow.y)
            { if (source != null) source.Interrupt(); return false; }
            int before = health.currentHealth;
            health.TakeDamage(damage);
            return health.currentHealth < before;
        }
    }
}
