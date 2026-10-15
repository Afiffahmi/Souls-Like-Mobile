using System.Collections.Generic;
using ElementalGems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulsLike.Enemies
{
    // Sample after EnemySlashVisual has applied this frame's animation and travel.
    [DisallowMultipleComponent, DefaultExecutionOrder(220)]
    public sealed class EnemySlashDamage : MonoBehaviour
    {
        const float Thickness = .12f;
        readonly HashSet<EnemyTarget> hit = new HashSet<EnemyTarget>();
        readonly Dictionary<EnemyTarget, Vector3> touching = new Dictionary<EnemyTarget, Vector3>();
        Collider[] overlaps = new Collider[16];
        readonly RaycastHit[] obstacles = new RaycastHit[32];
        EnemyBrain brain;
        EnemySlashVisual visual;
        EnemyAnimationDriver driver;
        EnemyControlStatus control;
        EnemyAttackDefinition attack;
        GemCrescentSlash previousSlash;
        Vector3 previousPosition, previousScale;
        Quaternion previousRotation;
        float previousSweep;

        public static bool UsesSlash(EnemyAttackDefinition definition) => definition != null &&
            definition.effect is EnemyMeleeEffect && definition.slash != null && definition.slash.enabled;

        void OnEnable()
        {
            brain = GetComponent<EnemyBrain>(); visual = GetComponent<EnemySlashVisual>();
            driver = GetComponent<EnemyAnimationDriver>(); control = GetComponent<EnemyControlStatus>();
            if (brain == null) return;
            brain.AttackStarted += Begin;
            brain.StateChanged += StateChanged;
            Begin(brain.CurrentAttack);
        }
        void OnDisable()
        {
            if (brain != null) { brain.AttackStarted -= Begin; brain.StateChanged -= StateChanged; }
            Begin(null);
        }
        void Begin(EnemyAttackDefinition definition)
        { attack = definition; hit.Clear(); touching.Clear(); previousSlash = null; }
        void StateChanged(EnemyState state) { if (state != EnemyState.Attacking) Begin(null); }

        bool Ready(GemCrescentSlash slash)
        {
            if (control == null) control = GetComponent<EnemyControlStatus>();
            if (!isActiveAndEnabled || brain == null || !brain.isActiveAndEnabled ||
                brain.State != EnemyState.Attacking || brain.Health == null || brain.Health.IsDead ||
                (control != null && control.CannotAct) || !UsesSlash(attack) || !attack.dealsDamage ||
                brain.CurrentAttack != attack || visual == null || !visual.isActiveAndEnabled ||
                slash == null || !slash.isActiveAndEnabled || !slash.HasDrivenGeometry ||
                driver == null || !driver.TryGetTime(attack.stateName, out float time)) return false;
            // Never let a stale visual, recovery frame, or entirely skipped window deal damage.
            return attack.useFrameWindow ? attack.IsInsideFrameWindow(time) : time >= attack.hitStart && time < attack.hitEnd;
        }

        public bool IsTouching(EnemyTarget target)
        {
            var slash = visual != null ? visual.ActiveSlash : null;
            if (target == null || !Ready(slash)) return false;
            Collect(slash, false);
            return touching.ContainsKey(target);
        }

        void LateUpdate()
        {
            var slash = visual != null ? visual.ActiveSlash : null;
            if (!Ready(slash)) { previousSlash = null; touching.Clear(); return; }
            Collect(slash, true);
            var currentAttack = attack;
            // Contact is the impact. The legacy end-window delay belongs to non-slash melee.
            // Copy because a successful parry can synchronously clear the contact collection.
            targets.Clear(); targets.AddRange(touching.Keys);
            foreach (var target in targets)
            {
                if (brain.State != EnemyState.Attacking || attack != currentAttack) break;
                if (target == null || !target.IsAlive || !hit.Add(target)) continue;
                var element = brain.GetComponent<ElementalEnemy>();
                if (attack.applyElementToPlayer && element != null)
                    target.ReceiveElementalDamage(attack.damage, brain, element.CurrentElement, attack.canBeParried);
                else target.ReceiveDamage(attack.damage, brain, attack.canBeParried);
            }
        }
        readonly List<EnemyTarget> targets = new List<EnemyTarget>();

        void Collect(GemCrescentSlash slash, bool sweepMotion)
        {
            Physics.SyncTransforms();
            touching.Clear();
            Vector3 position = slash.transform.position, scale = slash.transform.lossyScale;
            Quaternion rotation = slash.transform.rotation;
            float reveal = slash.IsThrust ? 1 : slash.RevealedSweep;
            bool interpolate = sweepMotion && previousSlash == slash;
            float reach = 4f * Mathf.Max(scale.x, scale.y, scale.z);
            float travel = interpolate ? Vector3.Distance(position, previousPosition) +
                Quaternion.Angle(rotation, previousRotation) * Mathf.Deg2Rad * reach +
                Vector3.Distance(scale, previousScale) * 4 : 0;
            int steps = Mathf.Clamp(Mathf.CeilToInt(travel / .15f), 1, 48);
            for (int f = 1; f <= steps; f++)
            {
                float t = (float)f / steps;
                var matrix = Matrix4x4.TRS(interpolate ? Vector3.Lerp(previousPosition, position, t) : position,
                    interpolate ? Quaternion.Slerp(previousRotation, rotation, t) : rotation,
                    interpolate ? Vector3.Lerp(previousScale, scale, t) : scale);
                float visible = interpolate ? Mathf.Lerp(previousSweep, reveal, t) : reveal;
                // Spacing stays below the contact thickness even on enlarged effects.
                float length = slash.IsThrust ? GemCrescentSlash.ThrustLength + GemCrescentSlash.ThrustTail :
                    (slash.FullCircle ? 360 : GemCrescentSlash.BodyArc) * Mathf.Deg2Rad * 3.5f;
                int bands = Mathf.Clamp(Mathf.CeilToInt(length * Mathf.Max(scale.x, scale.z) * visible / .15f), 1, 256);
                for (int i = 0; i <= bands; i++)
                {
                    float u = visible * i / bands;
                    Vector3 inner, outer;
                    if (slash.IsThrust)
                    {
                        // The shader displays the entire tapered lance, including its short tail.
                        float taper = Mathf.Pow(Mathf.Max(0, Mathf.Sin(u * Mathf.PI)), .55f) * (1 - .5f * u);
                        float width = GemCrescentSlash.ThrustHalfWidth * slash.bandWidth * taper;
                        float z = Mathf.Lerp(-GemCrescentSlash.ThrustTail, GemCrescentSlash.ThrustLength, u);
                        inner = new Vector3(-width, 0, z); outer = new Vector3(width, 0, z);
                    }
                    else slash.GetLocalHitSpan(u, out inner, out outer);
                    Query(matrix.MultiplyPoint3x4(inner), matrix.MultiplyPoint3x4(outer));
                }
            }
            if (sweepMotion)
            {
                previousSlash = slash; previousPosition = position; previousRotation = rotation;
                previousScale = scale; previousSweep = reveal;
            }
        }

        void Query(Vector3 from, Vector3 to)
        {
            var physics = gameObject.scene.GetPhysicsScene();
            int count;
            while ((count = physics.OverlapCapsule(from, to, Thickness, overlaps, ~0, QueryTriggerInteraction.Collide)) == overlaps.Length)
                System.Array.Resize(ref overlaps, overlaps.Length * 2);
            for (int i = 0; i < count; i++)
            {
                var collider = overlaps[i];
                var target = collider.GetComponentInParent<EnemyTarget>();
                if (target == null || !target.isActiveAndEnabled || !target.IsAlive || target.transform.IsChildOf(transform) ||
                    target.gameObject.scene != gameObject.scene || touching.ContainsKey(target)) continue;
                Vector3 span = to - from;
                float t = span.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector3.Dot(collider.bounds.center - from, span) / span.sqrMagnitude) : 0;
                Vector3 point = Vector3.Lerp(from, to, t);
                if (Blocked(point, target)) continue;
                touching.Add(target, point);
            }
        }

        bool Blocked(Vector3 point, EnemyTarget target)
        {
            if (brain.profile == null) return true;
            Vector3 delta = target.AimPosition - point;
            if (delta.sqrMagnitude < .0001f) return false;
            int count = gameObject.scene.GetPhysicsScene().Raycast(point, delta.normalized, obstacles,
                delta.magnitude, brain.profile.obstructionLayers, QueryTriggerInteraction.Ignore);
            if (count == obstacles.Length) return true;
            for (int i = 0; i < count; i++)
            {
                var other = obstacles[i].transform;
                if (other.IsChildOf(transform) || other.IsChildOf(target.transform)) continue;
                return true;
            }
            return false;
        }
    }
}
