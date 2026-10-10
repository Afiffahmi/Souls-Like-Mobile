using System.Collections.Generic;
using UnityEngine;

namespace ElementalGems
{
    [DisallowMultipleComponent, RequireComponent(typeof(GemManager)), DefaultExecutionOrder(180)]
    public sealed class GemSwordCombat : MonoBehaviour
    {
        // Retain bindings for existing scenes and setup tools; they no longer determine hits.
        [HideInInspector] public Transform blade;
        [HideInInspector] public Vector3 bladeStart = new Vector3(-0.35f, 0.1f, 0);
        [HideInInspector] public Vector3 bladeEnd = new Vector3(0.48f, 0.1f, 0);
        [HideInInspector] public float hitRadius = 0.18f;
        [Header("Slash hitbox")]
        [Tooltip("Radius of the filled slash area. Full Circle slashes cover 360 degrees; others cover the front half. Scales with VFX size.")]
        [Min(.1f)] public float slashReach = 3.2f;
        [Tooltip("Total hitbox height around the slash centre, in world metres.")]
        [Min(.1f)] public float slashHitHeight = 2.5f;
        public LayerMask enemyLayers = ~0;
        [Header("Damage and hit window; animation clips remain unchanged")]
        [HideInInspector, Min(0)] public float lightDamage = 18, heavyDamage = 30, specialDamage = 45; // Legacy migration only.
        [Range(0, 1)] public float hitStart = 0.2f, hitEnd = 0.72f;
        [Header("Combo finisher")]
        [Tooltip("Restrict heavy combo hit reactions to Hit Reaction Attack Number. Light attacks always require the uninterrupted third combo attack.")]
        public bool finisherHitReactionOnly;
        [InspectorName("Hit Reaction Attack Number"), Min(1)] public int knockbackAttackNumber = 3;
        private GemManager manager;
        private PlayerStateManager player;
        private GemLightSlashEffects effects;
        private readonly HashSet<Enemy> hit = new HashSet<Enemy>();
        private int stateHash;
        private bool tracking, sampled;
        private CombatAttackInput input;
        private int comboAttackNumber;
        private bool lightFinisher;
        private GemAttack attack;
        private float capturedDamage;
        private float capturedKnockbackMultiplier = 1f;
        private float capturedMinimumKnockback;
        private bool phasedSwordHeavy;
        private float capturedKnockbackDuration = 1f;
        private GemCrescentSlash previousSlash;
        private Vector3 previousPosition, previousScale;
        private Quaternion previousRotation;
        private Collider[] contacts = new Collider[32];
        public bool TrailActive => tracking && effects != null && effects.TrailActive;
        private void Awake()
        {
            manager = GetComponent<GemManager>(); player = GetComponent<PlayerStateManager>();
            effects = GetComponent<GemLightSlashEffects>();
        }
        public void Begin(int hash, CombatAttackInput kind, int attackNumber = -1, bool completedLightCombo = false,
            float damageMultiplier = 1f, float knockbackMultiplier = 1f, float minimumKnockback = 0f)
        {
            if (manager == null) manager = GetComponent<GemManager>();
            if (player == null) player = GetComponent<PlayerStateManager>();
            comboAttackNumber = attackNumber >= 0 ? attackNumber : player != null ? player.CurrentAttackNumber : 0;
            // Permission comes from this animation's continuous combo, never a previous hit count.
            lightFinisher = kind == CombatAttackInput.LightAttack && attackNumber == 3 && completedLightCombo;
            stateHash = hash; input = kind; tracking = true; sampled = false; previousSlash = null; hit.Clear();
            capturedKnockbackMultiplier = Mathf.Max(0, WeaponStatModifier.Finite(knockbackMultiplier, 1));
            capturedMinimumKnockback = Mathf.Max(0, WeaponStatModifier.Finite(minimumKnockback));
            phasedSwordHeavy = kind == CombatAttackInput.HeavyAttack && player != null && player.IsSwordHeavyAttacking;
            attack = manager.Capture().WithKnockbackMultiplier(capturedKnockbackMultiplier, capturedMinimumKnockback);
            var defaults = player != null ? player.CurrentAttackDefaults ?? player.CaptureAttackDefaults(PlayerCombatMode.Sword,kind) : null;
            float baseDamage = defaults != null ? defaults.damage : kind == CombatAttackInput.HeavyAttack ? heavyDamage : kind == CombatAttackInput.SpecialAttack ? specialDamage : lightDamage;
            capturedKnockbackDuration = player != null ? player.CurrentWeaponAttackStats.KnockbackDurationMultiplier * defaults.knockbackDurationScale : 1f;
            capturedDamage = (player != null ? player.CurrentWeaponAttackStats.Damage(baseDamage) : baseDamage) *
                Mathf.Max(0, WeaponStatModifier.Finite(damageMultiplier, 1));
        }
        public void End(int hash) { if (stateHash == hash) { tracking = false; sampled = false; lightFinisher = false; comboAttackNumber = 0; } }
        public void SampleSlash(GemCrescentSlash slash)
        {
            if (!isActiveAndEnabled || !tracking || player == null || !player.IsAttacking ||
                player.CombatMode != PlayerCombatMode.Sword || effects == null || !effects.isActiveAndEnabled ||
                slash == null || effects.ActiveSlash != slash || !slash.HasDrivenGeometry)
            { sampled = false; return; }

            var current = manager.Capture();
            if (current.element != attack.element) attack = current.WithKnockbackMultiplier(capturedKnockbackMultiplier, capturedMinimumKnockback);
            if (previousSlash != slash) sampled = false;
            Vector3 position = slash.transform.position, scale = slash.transform.lossyScale;
            Quaternion rotation = slash.HitFacing;
            float radius = Mathf.Max(.01f, hitRadius);
            // Full Circle uses a filled disc. Other swings retain their frontal half-disc.
            float reach = Mathf.Max(.1f, slashReach) * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float travel = sampled ? Vector3.Distance(previousPosition, position) +
                Quaternion.Angle(previousRotation, rotation) * Mathf.Deg2Rad * reach +
                Vector3.Distance(previousScale, scale) * slashReach : 0;
            int frames = Mathf.Clamp(Mathf.CeilToInt(travel / radius), 1, 48);
            for (int f = 1; f <= frames; f++)
            {
                float t = (float)f / frames;
                var sampledScale = sampled ? Vector3.Lerp(previousScale, scale, t) : scale;
                Query(sampled ? Vector3.Lerp(previousPosition, position, t) : position,
                    sampled ? Quaternion.Slerp(previousRotation, rotation, t) : rotation,
                    Mathf.Max(.1f, slashReach) * Mathf.Max(Mathf.Abs(sampledScale.x), Mathf.Abs(sampledScale.z)), slash.FullCircle);
            }
            previousPosition = position; previousRotation = rotation; previousScale = scale;
            previousSlash = slash; sampled = true;
        }
        private void Query(Vector3 origin, Quaternion facing, float reach, bool fullCircle)
        {
            Vector3 forward = facing * Vector3.forward;
            Vector3 center = fullCircle ? origin : origin + forward * (reach * .5f);
            Vector3 halfExtents = new Vector3(reach, Mathf.Max(.1f, slashHitHeight) * .5f, fullCircle ? reach : reach * .5f);
            int count;
            // Grow only on overflow; don't silently miss enemies in dense groups.
            while ((count = Physics.OverlapBoxNonAlloc(center, halfExtents, contacts, facing, enemyLayers, QueryTriggerInteraction.Collide)) == contacts.Length)
                System.Array.Resize(ref contacts, contacts.Length * 2);
            for (int i = 0; i < count; i++)
            {
                var collider = contacts[i];
                var enemy = collider.GetComponentInParent<Enemy>();
                if (enemy == null || enemy.transform.IsChildOf(transform) || !enemy.isActiveAndEnabled || enemy.IsDead || hit.Contains(enemy)) continue;
                // Reject enemies behind the slash, even if a large collider overlaps the front box.
                Vector3 toEnemy = Vector3.ProjectOnPlane(enemy.transform.position - origin, Vector3.up);
                if (!fullCircle && Vector3.Dot(toEnemy, forward) < 0f) continue;
                Vector3 probe = origin;
                probe.y = Mathf.Clamp(collider.bounds.center.y, origin.y - halfExtents.y, origin.y + halfExtents.y);
                Vector3 point = collider.ClosestPoint(probe);
                Vector3 offset = Vector3.ProjectOnPlane(point - origin, Vector3.up);
                if (offset.sqrMagnitude > reach * reach) continue;
                hit.Add(enemy);
                float damage = capturedDamage;
                bool chainAttack = input != CombatAttackInput.SpecialAttack && comboAttackNumber > 0;
                bool finisher = chainAttack && comboAttackNumber == knockbackAttackNumber;
                bool light = input == CombatAttackInput.LightAttack;
                // Every low/high heavy window is a committed impact, not a light-combo step.
                bool react = light ? lightFinisher : phasedSwordHeavy || !finisherHitReactionOnly || !chainAttack || finisher;
                Vector3 direction = enemy.transform.position - transform.position;
                int dealt = ElementalDamage.Hit(attack, damage, manager, enemy, point, direction, react,
                    allowControlEffects: !light || lightFinisher, knockbackDurationMultiplier: capturedKnockbackDuration);
                var elemental = enemy.GetComponent<ElementalEnemy>();
                bool pushed = capturedKnockbackDuration > 0 && (!light || lightFinisher) && elemental != null && elemental.isActiveAndEnabled &&
                    attack.knockback > elemental.knockbackResistance;
                manager.ShowKnockbackImpact(attack, enemy, point, direction, dealt > 0 && (react || pushed));
            }
        }
        private void OnDisable() { tracking = false; sampled = false; lightFinisher = false; comboAttackNumber = 0; hit.Clear(); }
        private void OnDrawGizmosSelected()
        {
            var slash = effects != null ? effects.ActiveSlash : null;
            if (slash == null) return;
            Gizmos.color = Color.cyan;
            float reach = slashReach * Mathf.Max(Mathf.Abs(slash.transform.lossyScale.x), Mathf.Abs(slash.transform.lossyScale.z));
            Vector3 origin = slash.transform.position;
            Vector3 up = Vector3.up * (slashHitHeight * .5f);
            Vector3 previous = origin + slash.HitFacing * Vector3.left * reach;
            Vector3 first = previous;
            for (int i = 1; i <= 24; i++)
            {
                float angle = Mathf.Lerp(-90, slash.FullCircle ? 270 : 90, i / 24f) * Mathf.Deg2Rad;
                Vector3 next = origin + slash.HitFacing * new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * reach;
                Gizmos.DrawLine(previous + up, next + up);
                Gizmos.DrawLine(previous - up, next - up);
                if (i % 6 == 0) Gizmos.DrawLine(next - up, next + up);
                previous = next;
            }
            Gizmos.DrawLine(first + up, previous + up);
            Gizmos.DrawLine(first - up, previous - up);
            Gizmos.DrawLine(first - up, first + up);
        }
    }
}
