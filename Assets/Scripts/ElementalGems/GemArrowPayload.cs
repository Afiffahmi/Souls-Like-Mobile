using System.Collections.Generic;
using UnityEngine;

namespace ElementalGems
{
    [DisallowMultipleComponent]
    public sealed class GemArrowPayload : MonoBehaviour
    {
        public GemAttack Attack { get; private set; }
        public ElementType Element => Attack != null ? Attack.element : ElementType.Normal;
        private GemManager source;
        private float baseDamage, radius;
        private float knockbackDuration = 1f;
        private bool impacted, chargedLight;
        public bool IsEmbeddedInEnemy { get; private set; }
        public bool KeepTipEffectsOnImpact => IsEmbeddedInEnemy;
        public bool IsChargedLight => chargedLight;
        private TrailRenderer trail;
        private GroundFieldSnapshot groundField;
        public void Initialize(GemAttack attack, GemManager owner, float damage, float groundRadius, GroundFieldSnapshot field = null, float knockbackDurationMultiplier = 1f, bool chargedLight = false)
        {
            this.chargedLight = chargedLight;
            IsEmbeddedInEnemy = false;
            knockbackDuration = Mathf.Clamp(WeaponStatModifier.Finite(knockbackDurationMultiplier, 1), 0, 4);
            Attack = attack; source = owner; baseDamage = damage; radius = field != null ? field.radius : groundRadius; groundField = field; impacted = false;
            if (trail != null) Destroy(trail.gameObject);
            if (attack.element != ElementType.Normal && attack.trailMaterial != null)
            {
                var go = new GameObject("Elemental Arrow Trail"); go.transform.SetParent(transform, false);
                var projectile = GetComponent<BowArrowProjectile>();
                go.transform.localPosition = Vector3.forward * (projectile != null ? projectile.tipOffset : 0f);
                trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = attack.trailMaterial; trail.time = attack.trailLifetime;
                // A broader, longer tapered wake makes the charged arrow readable in motion.
                if (chargedLight) trail.time = Mathf.Max(0.45f, attack.trailLifetime * 1.5f);
                trail.startWidth = chargedLight ? Mathf.Max(0.14f, attack.trailWidth * 1.65f) : attack.trailWidth;
                trail.endWidth = 0;
                trail.numCornerVertices = 3; trail.numCapVertices = 2;
                trail.textureMode = LineTextureMode.Stretch;
                trail.startColor = attack.color; trail.endColor = new Color(attack.color.r, attack.color.g, attack.color.b, 0);
                trail.minVertexDistance = 0.025f; trail.emitting = true;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
            }
        }
        private void ReplaceOtherGemArrows(Enemy enemy)
        {
            // Only this enemy's lodged arrows participate. Same-gem arrows stay attached;
            // normal shots and ground bursts do not replace an embedded gem.
            foreach (var previous in enemy.GetComponentsInChildren<GemArrowPayload>(true))
            {
                if (previous == this || !previous.IsEmbeddedInEnemy || previous.Element == Element ||
                    previous.GetComponentInParent<Enemy>() != enemy) continue;
                previous.IsEmbeddedInEnemy = false;
                previous.gameObject.SetActive(false);
                Destroy(previous.gameObject);
            }
        }

        public void Impact(Collider collider, Vector3 point, Vector3 normal, bool ground)
        {
            if (impacted || Attack == null) return;
            impacted = true;
            var struckEnemy = !ground && collider != null ? collider.GetComponentInParent<Enemy>() : null;
            IsEmbeddedInEnemy = Element != ElementType.Normal && struckEnemy != null;
            if (IsEmbeddedInEnemy) ReplaceOtherGemArrows(struckEnemy);
            if (trail != null) trail.emitting = KeepTipEffectsOnImpact;
            var seen = new HashSet<Enemy>();
            if (ground)
            {
                foreach (var c in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Collide))
                {
                    var enemy = c.GetComponentInParent<Enemy>();
                    if (enemy == null || !seen.Add(enemy)) continue;
                    // Ground burst cannot pass through world geometry.
                    Vector3 end = c.bounds.center;
                    if (Physics.Linecast(point + Vector3.up * 0.1f, end, out var wall, ~0, QueryTriggerInteraction.Ignore) &&
                        wall.collider.GetComponentInParent<Enemy>() != enemy) continue;
                    ElementalDamage.Hit(Attack, baseDamage, source, enemy, end, end - point, knockbackDurationMultiplier: knockbackDuration);
                }
            }
            else if (collider != null)
                ElementalDamage.Hit(Attack, baseDamage, source, collider.GetComponentInParent<Enemy>(), point, transform.forward, knockbackDurationMultiplier: knockbackDuration);
            ElementalDamage.Impact(Attack, point, normal);
            if (ground) GemGroundField.Spawn(groundField, Attack, source, point, normal, gameObject.scene);
        }
    }
}
