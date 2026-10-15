using ElementalGems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulsLike.Enemies
{
    [CreateAssetMenu(menuName = "Enemies/Animation Actions/Elemental Ground Slam")]
    public sealed class EnemyGroundSlamAction : EnemyAnimationAction
    {
        [Tooltip("Radius at impact. Damage comes from the attack definition's Damage to Player.")]
        [Min(.1f)] public float radius = 4;
        [Min(.1f)] public float heightTolerance = 1.5f;
        public Vector3 localImpactOffset;
        public LayerMask groundAndObstacles = ~0;
        public bool requireLineOfSight = true;
        [Header("Elemental ground visual")]
        [Tooltip("Use the boss's original element for this special, even during a temporary Normal/link-up state. The same element drives both the ground VFX and the player hit. Disable to deliberately use the current state instead.")]
        public bool useInnateElement = true;
        public GemDefinition[] elementPalette = System.Array.Empty<GemDefinition>();
        public Material groundMaterial;
        public Material particleMaterial;
        [Tooltip("Material for branching lightning arcs, roots and wind strands.")]
        public Material energyMaterial;
        [Min(.2f)] public float effectLifetime = 2.5f;
        [Tooltip("Speed of glow, electricity, particles and ground appearance. Does not change jump speed or damage timing.")]
        [Min(.05f)] public float effectSpeed = 1;
        [Tooltip("Uneven outer edge. The damage radius remains the Radius above.")]
        [Range(0, .35f)] public float edgeRoughness = .2f;
        [Min(0)] public float crackGlow = .65f;
        [Tooltip("Transparency of the cracks. The original ground remains visible between and beneath them.")]
        [Range(0, 1)] public float crackOpacity = .32f;
        [Tooltip("Width of the main fissures in world units; small branches taper to a point.")]
        [Range(.005f, .15f)] public float crackWidth = .035f;
        [Tooltip("Radius of the irregular elemental centre, as a fraction of the full AoE radius.")]
        [Range(.05f, .45f)] public float coreRadiusRatio = .22f;
        [Tooltip("Visibility of the elemental pool at the impact centre; surrounding ground stays uncovered.")]
        [Range(0, 1)] public float coreOpacity = .85f;
        [UnityEngine.Serialization.FormerlySerializedAs("rubbleRings")]
        [Range(1, 5)] public int crackDetail = 4;
        [Range(0, 80)] public int particleCount = 18;
        public override bool IsConfigured => radius > 0 && groundMaterial != null;
        public override bool DealsDamage => true;
        public override void Execute(EnemyAttackContext context)
        {
            if (!CanStart(context.source) || context.attack == null) return;
            var source = context.source;
            var motor = source.GetComponent<EnemyNavMeshMotor>();
            // Agent.Move is applied earlier this tick; Transform synchronization may happen later.
            Vector3 center = motor != null && motor.Ready ? motor.Agent.nextPosition : source.transform.position;
            center += source.transform.TransformDirection(localImpactOffset);
            var physics = source.gameObject.scene.GetPhysicsScene();
            var hits = new RaycastHit[32];
            int count = physics.Raycast(center + Vector3.up * 2, Vector3.down, hits, 5, groundAndObstacles, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Vector3 normal = Vector3.up;
            for (int i = 0; i < count; i++)
            {
                if (BowArrowProjectile.IsCharacterCollider(hits[i].collider) || hits[i].normal.y < .45f || hits[i].distance >= nearest) continue;
                nearest = hits[i].distance; center = hits[i].point; normal = hits[i].normal;
            }
            var element = source.GetComponent<ElementalEnemy>();
            ElementType type = element != null ? (useInnateElement ? element.element : element.CurrentElement) : ElementType.Normal;
            var gem = System.Array.Find(elementPalette, g => g != null && g.element == type);
            Color tint = gem != null ? gem.color : ElementalReactionVfx.Tint(type);
            CombatCameraShake.EnemyAttack(center, true, 1.4f);
            ElementalGroundImpact.Spawn(source.gameObject.scene, center, normal, radius, effectLifetime, crackDetail,
                particleCount, type, tint, groundMaterial, particleMaterial, energyMaterial, effectSpeed, edgeRoughness,
                crackGlow, crackOpacity, crackWidth, groundAndObstacles, coreRadiusRatio, coreOpacity);
            // Registry is one entry per combat target, independent of the number of colliders on a player.
            foreach (var target in EnemyTarget.Active)
            {
                if (target == null || !target.IsAlive || target.gameObject.scene != source.gameObject.scene) continue;
                Vector3 delta = target.GroundPosition - center;
                if (Mathf.Abs(Vector3.Dot(delta, normal)) > heightTolerance || Vector3.ProjectOnPlane(delta, normal).sqrMagnitude > radius * radius) continue;
                if (requireLineOfSight)
                {
                    Vector3 origin = center + Vector3.up * .35f;
                    Vector3 sight = target.AimPosition - origin;
                    int obstacles = physics.Raycast(origin, sight.normalized, hits, sight.magnitude, groundAndObstacles, QueryTriggerInteraction.Ignore);
                    bool blocked = obstacles == hits.Length;
                    for (int i = 0; i < obstacles; i++) if (!BowArrowProjectile.IsCharacterCollider(hits[i].collider)) { blocked = true; break; }
                    if (blocked) continue;
                }
                // A ground slam cannot be timed-parried; existing roll, guard and invulnerability gates still apply.
                if (context.attack.applyElementToPlayer)
                    target.ReceiveElementalDamage(context.attack.damage, source, type, context.attack.canBeParried);
                else target.ReceiveDamage(context.attack.damage, source, context.attack.canBeParried);
            }
        }
    }
}
