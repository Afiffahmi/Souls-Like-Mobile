using ElementalGems;
using UnityEngine;

namespace SoulsLike.Enemies
{
    [CreateAssetMenu(menuName = "Enemies/Animation Actions/Travelling Ground Slash")]
    public sealed class EnemyGroundSlashAction : EnemyAnimationAction
    {
        [Min(.1f)] public float distance = 12;
        [Min(.1f)] public float speed = 13;
        [Min(.1f)] public float height = 2.5f;
        [Min(.1f)] public float hitHalfWidth = .7f;
        [Min(.1f)] public float trailLifetime = 1.5f;
        public bool launchFromSwordTip = true;
        public Vector3 localOffset = new Vector3(0, 0, 1.2f);
        public LayerMask groundAndObstacles = ~0;
        public bool useInnateElement = true;
        public GemDefinition[] elementPalette = System.Array.Empty<GemDefinition>();
        public Material crescentMaterial, trailMaterial, particleMaterial, debrisMaterial;
        public override bool IsConfigured => distance > 0 && speed > 0 && height > 0 &&
            crescentMaterial != null && trailMaterial != null;
        public override bool DealsDamage => true;
        public override void Execute(EnemyAttackContext context)
        {
            if (!CanStart(context.source) || context.attack == null) return;
            var source = context.source;
            var element = source.GetComponent<ElementalEnemy>();
            var type = element != null ? (useInnateElement ? element.element : element.CurrentElement) : ElementType.Normal;
            var gem = System.Array.Find(elementPalette, g => g != null && g.element == type);
            Color tint = gem != null ? gem.color : ElementalReactionVfx.Tint(type);
            Vector3 heading = Vector3.ProjectOnPlane(source.transform.forward, Vector3.up).normalized;
            if (heading.sqrMagnitude < .001f) heading = Vector3.forward;
            var motor = source.GetComponent<EnemyNavMeshMotor>();
            Vector3 origin = motor != null && motor.Ready ? motor.NavigationPosition : source.transform.position;
            origin += Quaternion.LookRotation(heading) * localOffset;
            var swordVisual = source.GetComponent<EnemySlashVisual>();
            if (launchFromSwordTip && swordVisual != null && swordVisual.sword != null)
                origin = swordVisual.sword.TransformPoint(swordVisual.swordTipLocal);
            EnemyGroundSlash.Spawn(context, this, origin, heading, type, tint);
            CombatCameraShake.EnemyAttack(origin, true, 1.4f);
        }
    }
}
