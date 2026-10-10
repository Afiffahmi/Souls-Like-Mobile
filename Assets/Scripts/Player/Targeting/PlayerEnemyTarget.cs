using UnityEngine;

namespace SoulsLike.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerOverall))]
    public sealed class PlayerEnemyTarget : EnemyTarget
    {
        public bool evadeDuringRoll = true;
        public bool parryEnabled = true;
        [Tooltip("A held guard pose blocks incoming melee damage even after the timed parry window expires.")]
        public bool blockWhileHoldingGuard = true;
        // Retain old serialized data; PlayerStateManager owns the actual timed window.
        [HideInInspector] public Vector2 parryWindow = new Vector2(.1f, .55f);
        [Tooltip("Push strength applied to the attacker after a successful timed parry. Enemy knockback resistance still applies.")]
        [Min(0)] public float successfulParryKnockback = 5;
        private PlayerOverall health;
        private PlayerStateManager player;
        private CharacterController body;
        private void Awake() { health = GetComponent<PlayerOverall>(); player = GetComponent<PlayerStateManager>(); body = GetComponent<CharacterController>(); }
        public override bool IsAlive => isActiveAndEnabled && health != null && health.currentHealth > 0;
        public override Vector3 AimPosition => body != null && body.enabled ? body.bounds.center : transform.position + Vector3.up;
        public override Vector3 GroundPosition => body != null && body.enabled ? new Vector3(body.bounds.center.x, body.bounds.min.y, body.bounds.center.z) : transform.position;
        public override bool ReceiveDamage(int damage, EnemyBrain source) => ReceiveDamage(damage, source, true);
        public override bool ReceiveDamage(int damage, EnemyBrain source, bool allowParry)
        {
            if (!IsAlive || health.IsInvulnerable || damage <= 0 || (evadeDuringRoll && player != null && player.IsRolling)) return false;
            if (allowParry && TryParryAttack(source)) return false;
            if (blockWhileHoldingGuard && player != null && player.IsDefending) return false;
            int before = health.currentHealth;
            health.TakeDamage(damage);
            return health.currentHealth < before;
        }
        public override bool TryParryAttack(EnemyBrain source)
        {
            if (!IsAlive || health.IsInvulnerable || (evadeDuringRoll && player != null && player.IsRolling)) return false;
            // Confirm success at actual impact, using the same window as the held pose.
            // This releases the hold frame and lets the player's full follow-through play.
            if (parryEnabled && player != null && player.TryReceiveParryableHit())
            {
                ElementalGems.GemFeedback.Show(AimPosition, "PARRY!", new Color(.35f, .85f, 1f));
                if (source != null && !source.Health.IsDead)
                {
                    source.Interrupt();
                    var elemental = source.GetComponent<ElementalGems.ElementalEnemy>();
                    Vector3 away = Vector3.ProjectOnPlane(source.transform.position - GroundPosition, Vector3.up);
                    if (away.sqrMagnitude < .0001f) away = transform.forward;
                    if (elemental != null) elemental.ApplyKnockback(away, successfulParryKnockback);
                    var gems = GetComponent<ElementalGems.GemManager>();
                    if (gems != null)
                    {
                        var collider = source.GetComponentInChildren<Collider>();
                        Vector3 point = collider != null ? collider.bounds.center : source.AttackPosition;
                        gems.ShowKnockbackImpact(gems.Capture(), source.Health, point, away,
                            source.State == EnemyState.TakingDamage);
                    }
                }
                return true;
            }
            return false;
        }
    }
}
