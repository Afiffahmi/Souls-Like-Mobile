using System.Collections.Generic;
using UnityEngine;

namespace ElementalGems
{
    /// <summary>Player-owned elemental state. Recipes and visuals are shared with enemy reactions.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(GemManager), typeof(PlayerOverall))]
    public sealed class PlayerElementalState : MonoBehaviour
    {
        [Min(.1f)] public float elementDuration = 10;
        [Min(0)] public float reactionDamageScale = .65f;
        [Min(0)] public float reactionCooldown = .65f;
        [Min(.1f)] public float reactionRadius = 4;
        [Range(0, 20)] public float auraWispsPerSecond = 8;
        public ElementType CurrentElement => gems != null ? gems.EquippedElement : ElementType.Normal;
        public float Remaining => Mathf.Max(0, elementExpiresAt - Time.time);
        public bool CannotAct => isActiveAndEnabled && (Has(StatusKind.Stun) || Has(StatusKind.Stagger));
        public bool CannotMove => CannotAct || (isActiveAndEnabled && Has(StatusKind.Root));
        public float MovementScale
        {
            get
            {
                if (!isActiveAndEnabled) return 1;
                if (CannotMove) return 0;
                float speed = 1;
                foreach (var status in statuses) if (status.kind == StatusKind.Slow && status.until > Time.time)
                    speed = Mathf.Min(speed, 1 - Mathf.Clamp01(status.magnitude));
                return speed;
            }
        }
        public float DamageTakenMultiplier => isActiveAndEnabled && Time.time < fractureUntil ? 1.35f : 1;
        private sealed class ActiveStatus { public StatusKind kind; public float until, magnitude, remainder; }
        private readonly List<ActiveStatus> statuses = new List<ActiveStatus>(8);
        private readonly List<GemSurfaceFlames> wisps = new List<GemSurfaceFlames>();
        private GemManager gems;
        private PlayerOverall health;
        private PlayerStateManager player;
        private float nextReaction, fractureUntil, nextVisual, elementExpiresAt;
        private void Awake()
        { gems = GetComponent<GemManager>(); health = GetComponent<PlayerOverall>(); player = GetComponent<PlayerStateManager>(); }
        private bool Has(StatusKind kind)
        { foreach (var status in statuses) if (status.kind == kind && status.until > Time.time) return true; return false; }

        // Called only after the target has accepted contact (roll, guard and invulnerability already checked).
        public bool ReceiveHit(int damage, ElementType incoming)
        {
            if (!isActiveAndEnabled || health.currentHealth <= 0 || health.IsInvulnerable || damage <= 0) return false;
            var gem = System.Array.Find(gems.gems, g => g != null && g.element == incoming);
            var attack = new GemAttack(gem);
            ElementType previous = CurrentElement;
            // Enemy attack assets own base damage. Do not change gem damageScale or other shared gem tuning.
            health.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(damage * attack.MultiplierAgainst(previous))));
            if (health.currentHealth <= 0) { Clear(); return true; }
            if (incoming == ElementType.Normal || gem == null) return true;
            foreach (var spec in attack.statuses)
                if (spec.chance > 0 && Random.value < spec.chance) AddStatus(spec.kind, spec.duration, spec.magnitude);
            var reaction = ElementalReactions.Recipe(previous, incoming);
            if (reaction != ElementalReaction.None && Time.time >= nextReaction)
            {
                nextReaction = Time.time + reactionCooldown;
                React(reaction, attack, damage);
            }
            if (health.currentHealth <= 0) { Clear(); return true; }
            // Incoming element stays equipped; only effects expire. Manual gem changes remain available.
            gems.Equip(incoming, false);
            elementExpiresAt = Time.time + elementDuration;
            RefreshVisual();
            return true; // Contact still applies effects when debug health is clamped to 1.
        }
        private void React(ElementalReaction reaction, GemAttack attack, float baseDamage)
        {
            float extra = baseDamage * reactionDamageScale;
            var position = transform.position;
            var scene = gameObject.scene;
            var color = attack.color;
            string label = System.Text.RegularExpressions.Regex.Replace(reaction.ToString(), "([a-z])([A-Z])", "$1 $2").ToUpperInvariant();
            GemFeedback.Show(position + Vector3.up * 1.4f, label, color);
            switch (reaction)
            {
                case ElementalReaction.SteamBurst:
                    AddStatus(StatusKind.Stagger, .5f);
                    ElementalReactionVfx.Burst(scene, position + Vector3.up * .4f, new Color(.8f, .93f, 1), reactionRadius, attack);
                    break;
                case ElementalReaction.ChainLightning:
                    AddStatus(StatusKind.Stun, 1.2f);
                    ElementalReactionVfx.Burst(scene, position + Vector3.up, color, 1, attack);
                    break;
                case ElementalReaction.Wildfire:
                    AddStatus(StatusKind.Burn, 4, Mathf.Max(2, extra * .2f));
                    ElementalReactionVfx.Burst(scene, position + Vector3.up * .4f, color, reactionRadius * .6f, attack);
                    break;
                case ElementalReaction.RootFracture:
                    fractureUntil = Time.time + Mathf.Min(6, elementDuration);
                    AddStatus(StatusKind.Root, 2.5f);
                    ElementalReactionVfx.Roots(transform, attack);
                    break;
                case ElementalReaction.GroundBreak:
                    AddStatus(StatusKind.Stun, 1.2f); AddStatus(StatusKind.Stagger, 1.8f);
                    ElementalReactionVfx.Burst(scene, position + Vector3.up * .1f, color, 2, attack);
                    break;
                case ElementalReaction.ElementalVortex:
                    AddStatus(StatusKind.Slow, 1.8f, .5f);
                    ElementalReactionVfx.Vortex(scene, position, reactionRadius, color, attack);
                    break;
                case ElementalReaction.Purification:
                    extra = baseDamage;
                    ElementalReactionVfx.Burst(scene, position + Vector3.up, Color.white, reactionRadius, attack);
                    break;
            }
            if (extra > 0) health.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(extra)));
        }
        private void AddStatus(StatusKind kind, float duration, float magnitude = 0)
        {
            if (duration <= 0) return;
            bool interrupted = CannotAct;
            var status = statuses.Find(s => s.kind == kind);
            if (status == null) { status = new ActiveStatus { kind = kind }; statuses.Add(status); }
            status.until = Mathf.Max(status.until, Time.time + Mathf.Min(duration, elementDuration));
            status.magnitude = Mathf.Max(status.magnitude, magnitude);
            if (!interrupted && CannotAct && player != null) player.InterruptForElementalStatus();
        }
        private void Update()
        {
            if (health.currentHealth <= 0) { Clear(); return; }
            for (int i = statuses.Count - 1; i >= 0; i--)
            {
                var status = statuses[i];
                if (status.until <= Time.time) { statuses.RemoveAt(i); continue; }
                if (status.kind != StatusKind.Burn && status.kind != StatusKind.Poison && status.kind != StatusKind.Void) continue;
                status.remainder += status.magnitude * Time.deltaTime;
                int damage = Mathf.FloorToInt(status.remainder);
                status.remainder -= damage;
                if (damage > 0) health.TakeDamage(damage);
            }
            if (Time.time >= nextVisual) { nextVisual = Time.time + .1f; RefreshVisual(); }
        }
        private void RefreshVisual()
        {
            bool active = Remaining > 0 && health.currentHealth > 0;
            if (active && wisps.Count == 0)
            {
                // Visible body stays intact; only a few wisps on each skinned body surface.
                var bodies = GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var body in bodies)
                {
                    if (body.sharedMesh == null) continue;
                    var effect = GemSurfaceFlames.Create(body, auraWispsPerSecond / Mathf.Max(1, bodies.Length), .16f, .55f);
                    if (effect != null) wisps.Add(effect);
                }
            }
            foreach (var effect in wisps) if (effect != null)
            { effect.SetTint(gems.Equipped != null ? gems.Equipped.color : Color.white, .7f); effect.SetActive(active); }
        }
        private void Clear()
        {
            statuses.Clear(); fractureUntil = nextReaction = elementExpiresAt = 0;
            foreach (var effect in wisps) if (effect != null) effect.SetActive(false);
        }
        private void OnDisable() => Clear();
        private void OnDestroy() { foreach (var effect in wisps) if (effect != null) Destroy(effect.gameObject); }
    }
}
