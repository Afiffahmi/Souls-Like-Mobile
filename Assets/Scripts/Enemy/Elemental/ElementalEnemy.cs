using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ElementalGems
{
    [DisallowMultipleComponent, RequireComponent(typeof(Enemy))]
    public sealed class ElementalEnemy : MonoBehaviour
    {
        [Header("Elemental identity")]
        [InspectorName("Innate Element"), Tooltip("Normal = an ordinary enemy, primed by elemental knockback. Any other value = born from that element; its innate element is never overwritten.")]
        public ElementType element;
        [Tooltip("How long an applied element stays. Zero keeps it until replaced, death or disable.")]
        [Min(0)] public float infusionDuration;
        public bool showElementAura = true;
        [Header("Link-up reactions")]
        [Min(.1f)] public float reactionRadius = 4f;
        [Min(0)] public float reactionDamageScale = .65f;
        [Min(.1f)] public float reactionCooldown = .65f;
        [Range(2, 12)] public int maxChainTargets = 6;
        [Header("Darkness shield")]
        public bool darknessShieldEnabled = true;
        [Min(1), Tooltip("Each layer needs two different non-Darkness elements, or one linked elemental reaction.")]
        public int darknessShieldLayers = 2;
        [Min(.1f)] public float purificationWindow = 6f;
        [Range(0, 1), Tooltip("Health damage while the Darkness shield is intact. Zero requires Purification first.")]
        public float shieldDamageMultiplier;
        public ElementType InfusedElement { get; private set; }
        public bool IsElementBorn => element != ElementType.Normal;
        public ElementType CurrentElement => IsElementBorn ? element : InfusedElement;
        public bool HasDarknessShield => darknessShieldEnabled && CurrentElement == ElementType.Darkness && !shieldBroken;
        public int ShieldLayersRemaining => HasDarknessShield ? Mathf.Max(0, darknessShieldLayers - purifiedLayers) : 0;
        public float DamageTakenMultiplier => (fractureLeft > 0 ? 1.35f : 1f) * (HasDarknessShield ? shieldDamageMultiplier : 1f);
        public Enemy Health => health;
        public event Action ElementChanged;
        private float infusionLeft, fractureLeft, nextReactionTime, purificationUntil;
        private int purificationElements, purifiedLayers;
        private bool shieldBroken;
        private ElementalReactionVfx aura, shieldAura;
        private ElementType auraElement;
        [Header("Resistances and control")]
        [Tooltip("Incoming element multipliers; omitted elements take 1x. Zero grants immunity.")]
        public ElementMatchup[] resistances = Array.Empty<ElementMatchup>();
        public StatusKind[] immunities = Array.Empty<StatusKind>();
        [Tooltip("AI attack behaviours to suspend while stunned or staggered. Health/this component must not be included.")]
        public Behaviour[] interruptOnStun = Array.Empty<Behaviour>();
        [Min(0)] public float knockbackResistance;
        private sealed class ActiveStatus { public StatusSpec spec; public float left, damageRemainder; }
        private readonly List<ActiveStatus> active = new List<ActiveStatus>();
        private readonly List<Behaviour> suspended = new List<Behaviour>();
        private Enemy health;
        private NavMeshAgent agent;
        private Animator animator;
        private float originalSpeed, originalAnimationSpeed;
        private bool ownsAgent, ownsAnimator;
        private Vector3 push;
        private readonly KnockbackMotion knockbackMotion = new KnockbackMotion();
        public float KnockbackRemaining => knockbackMotion.Remaining;
        public bool IsStunned { get; private set; }
        public bool IsRooted { get; private set; }
        public float MovementMultiplier { get; private set; } = 1;
        public event Action StatusChanged;
        private void Awake()
        {
            health = GetComponent<Enemy>(); agent = GetComponent<NavMeshAgent>(); animator = GetComponentInChildren<Animator>();
            health.OnDeath += ClearStatuses;
        }
        private void Start() => RefreshAura();
        private void OnDestroy() { if (health != null) health.OnDeath -= ClearStatuses; }
        public static ElementalEnemy GetOrAdd(Enemy enemy)
        {
            var result = enemy.GetComponent<ElementalEnemy>() ?? enemy.gameObject.AddComponent<ElementalEnemy>();
            if (enemy.GetComponent<SoulsLike.Enemies.EnemyBrain>() != null && enemy.GetComponent<SoulsLike.Enemies.EnemyControlStatus>() == null)
                enemy.gameObject.AddComponent<SoulsLike.Enemies.ElementalEnemyControl>();
            return result;
        }
        public float Resistance(ElementType incoming)
        {
            foreach (var row in resistances)
                if (row.defender == incoming)
                {
                    float value = Mathf.Max(0, row.multiplier);
                    // Fracture opens armor but preserves explicit elemental immunity.
                    return fractureLeft > 0 && value > 0 && value < 1 ? Mathf.Lerp(value, 1, .75f) : value;
                }
            return 1;
        }
        public bool HasStatus(StatusKind kind) => active.Exists(s => s.spec.kind == kind);
        public void Apply(GemAttack attack, Vector3 direction, bool allowControlEffects = true, float knockbackDurationMultiplier = 1f, bool applyElementStatuses = false)
        {
            bool primes = allowControlEffects && (applyElementStatuses || CanKnockback(direction, attack.knockback, knockbackDurationMultiplier));
            // Ordinary enemies only acquire a gem's status package from a real knockback.
            if (IsElementBorn || primes) foreach (var spec in attack.statuses)
            {
                // Early sword light hits still apply damage/debuffs, but cannot trigger
                // the AI Hit animation indirectly via stun/stagger or add a backward push.
                if (!allowControlEffects && (spec.kind == StatusKind.Stun || spec.kind == StatusKind.Stagger)) continue;
                if (spec.duration <= 0 || Array.IndexOf(immunities, spec.kind) >= 0 || UnityEngine.Random.value >= spec.chance) continue;
                var status = active.Find(s => s.spec.kind == spec.kind);
                if (status == null) { status = new ActiveStatus { spec = spec }; active.Add(status); }
                else { var merged = status.spec; merged.magnitude = Mathf.Max(merged.magnitude, spec.magnitude); status.spec = merged; }
                status.left = Mathf.Max(status.left, spec.duration);
            }
            if (allowControlEffects) ApplyKnockback(direction, attack.knockback, knockbackDurationMultiplier);
            UpdateControls();
        }
        public bool CanKnockback(Vector3 direction, float strength, float durationMultiplier = 1f) =>
            isActiveAndEnabled && health != null && !health.IsDead &&
            WeaponStatModifier.Finite(strength) > knockbackResistance && WeaponStatModifier.Finite(durationMultiplier, 1) > 0 &&
            Vector3.ProjectOnPlane(direction, Vector3.up).sqrMagnitude > .000001f;

        public void Infuse(ElementType incoming, GemAttack visual = null)
        {
            if (IsElementBorn || incoming == ElementType.Normal || health == null || health.IsDead || !isActiveAndEnabled || Resistance(incoming) <= 0) return;
            // A Darkness coating stays protected until its combination shield is broken.
            if (HasDarknessShield && incoming != ElementType.Darkness) return;
            bool changed = InfusedElement != incoming;
            InfusedElement = incoming; infusionLeft = infusionDuration;
            if (changed)
            {
                if (incoming == ElementType.Darkness) { shieldBroken = false; purifiedLayers = purificationElements = 0; }
                ElementChanged?.Invoke();
                GemFeedback.Show(transform.position + Vector3.up, incoming.ToString(), ElementalReactionVfx.Tint(incoming));
            }
            RefreshAura(visual);
        }
        public bool BeginReaction()
        {
            if (Time.time < nextReactionTime) return false;
            nextReactionTime = Time.time + Mathf.Max(.1f, reactionCooldown);
            return true;
        }
        public void ConsumeInfusion(ElementType expected)
        {
            if (IsElementBorn || InfusedElement != expected || HasDarknessShield) return;
            InfusedElement = ElementType.Normal; infusionLeft = 0;
            ElementChanged?.Invoke(); RefreshAura();
        }
        public void AddStatus(StatusKind kind, float duration, float magnitude = 1)
        {
            if (health == null || health.IsDead || !isActiveAndEnabled || duration <= 0 || Array.IndexOf(immunities, kind) >= 0) return;
            if (HasDarknessShield && (kind == StatusKind.Burn || kind == StatusKind.Poison || kind == StatusKind.Void)) return;
            var status = active.Find(s => s.spec.kind == kind);
            if (status == null)
            {
                status = new ActiveStatus { spec = new StatusSpec { kind = kind, chance = 1, duration = duration, magnitude = magnitude } };
                active.Add(status);
            }
            else { var spec = status.spec; spec.magnitude = Mathf.Max(spec.magnitude, magnitude); status.spec = spec; }
            status.left = Mathf.Max(status.left, duration);
            UpdateControls();
        }
        public void FractureArmor(float duration) { fractureLeft = Mathf.Max(fractureLeft, duration); }
        public void GroundElectricity()
        {
            // The innate identity of a Lightning-born creature remains Lightning.
            if (!IsElementBorn && InfusedElement == ElementType.Lightning)
            { InfusedElement = ElementType.Normal; infusionLeft = 0; ElementChanged?.Invoke(); RefreshAura(); }
        }
        public bool Purify(ElementType incoming, ElementType linked, GemAttack visual, bool allowControlEffects = true)
        {
            if (!HasDarknessShield || health == null || health.IsDead || !isActiveAndEnabled) return false;
            if (Time.time > purificationUntil) purificationElements = 0;
            int incomingBit = PurificationBit(incoming), linkedBit = PurificationBit(linked);
            if (incomingBit == 0 && linkedBit == 0) return false;
            purificationElements |= incomingBit | linkedBit;
            purificationUntil = Time.time + Mathf.Max(.1f, purificationWindow);
            if ((purificationElements & (purificationElements - 1)) == 0) return false;
            purificationElements = 0; purifiedLayers++;
            bool broken = purifiedLayers >= Mathf.Max(1, darknessShieldLayers);
            GemFeedback.Show(transform.position + Vector3.up, broken ? "PURIFICATION - SHIELD BROKEN" : "PURIFICATION - " + ShieldLayersRemaining + " SHIELD LEFT", Color.white);
            ElementalReactionVfx.Burst(gameObject.scene, transform.position + Vector3.up, Color.white, 2.2f, visual);
            if (broken)
            {
                shieldBroken = true;
                if (!IsElementBorn) InfusedElement = ElementType.Normal;
                if (allowControlEffects) AddStatus(StatusKind.Stagger, 1.1f);
                ElementChanged?.Invoke(); RefreshAura();
            }
            return broken;
        }
        private static int PurificationBit(ElementType value) => value == ElementType.Normal || value == ElementType.Darkness ? 0 : 1 << (int)value;
        public int TakeReactionDamage(float amount, ElementType incoming, bool hitReaction = false)
        {
            if (health == null || health.IsDead || !isActiveAndEnabled) return 0;
            int before = health.CurrentHealth;
            health.TakeDamage(Mathf.Max(0, Mathf.RoundToInt(amount * Resistance(incoming) * DamageTakenMultiplier)), hitReaction);
            return before - health.CurrentHealth;
        }
        private void RefreshAura(GemAttack visual = null)
        {
            var current = CurrentElement;
            if (!showElementAura || health == null || health.IsDead || !isActiveAndEnabled) current = ElementType.Normal;
            bool shieldVisible = current != ElementType.Normal && HasDarknessShield;
            if (shieldVisible && shieldAura == null) shieldAura = ElementalReactionVfx.Shield(transform, visual);
            if (!shieldVisible && shieldAura != null) { shieldAura.gameObject.SetActive(false); Destroy(shieldAura.gameObject); shieldAura = null; }
            if (aura != null && current == auraElement) return;
            if (aura != null) { aura.gameObject.SetActive(false); Destroy(aura.gameObject); aura = null; }
            auraElement = current;
            if (current != ElementType.Normal) aura = ElementalReactionVfx.Aura(transform, current, visual);
        }
        public void ApplyKnockback(Vector3 direction, float strength, float durationMultiplier = 1f)
        {
            if (health == null || health.IsDead || !isActiveAndEnabled) return;
            knockbackMotion.Add(direction, Mathf.Max(0, strength - knockbackResistance), durationMultiplier);
            push = knockbackMotion.Velocity;
        }
        private void Update()
        {
            if (health.IsDead) { ClearStatuses(); return; }
            fractureLeft = Mathf.Max(0, fractureLeft - Time.deltaTime);
            if (infusionDuration > 0 && InfusedElement != ElementType.Normal && (infusionLeft -= Time.deltaTime) <= 0)
            { InfusedElement = ElementType.Normal; ElementChanged?.Invoke(); }
            RefreshAura();
            bool changed = false;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var s = active[i];
                float dt = Mathf.Min(s.left, Time.deltaTime);
                s.left -= dt;
                if (s.spec.kind == StatusKind.Burn || s.spec.kind == StatusKind.Poison || s.spec.kind == StatusKind.Void)
                {
                    s.damageRemainder += s.spec.magnitude * dt * (HasDarknessShield ? shieldDamageMultiplier : 1);
                    int damage = Mathf.FloorToInt(s.damageRemainder + 0.0001f);
                    // Lingering damage is not another weapon impact. Only the
                    // originating contact decides whether to play a Hit reaction.
                    if (damage > 0) { health.TakeDamage(damage, false); s.damageRemainder -= damage; }
                    if (health.IsDead) return; // OnDeath can clear the status list during damage dispatch.
                }
                if (s.left <= 0) { active.RemoveAt(i); changed = true; }
            }
            if (changed) UpdateControls();
            if (!UsesPhysicsBody()) AdvanceKnockback(Time.deltaTime);
        }
        // Rigidbody displacement belongs to the physics clock; other movers use the frame clock.
        private bool UsesPhysicsBody()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) return false;
            var controller = GetComponent<CharacterController>();
            if (controller != null && controller.enabled) return false;
            var body = GetComponent<Rigidbody>();
            return body != null && !body.isKinematic;
        }
        private void FixedUpdate()
        {
            if (health != null && !health.IsDead && UsesPhysicsBody()) AdvanceKnockback(Time.fixedDeltaTime);
        }
        private void AdvanceKnockback(float seconds)
        {
            if (knockbackMotion.Remaining > 0f)
            {
                Vector3 delta = knockbackMotion.Advance(seconds);
                var controller = GetComponent<CharacterController>();
                var body = GetComponent<Rigidbody>();
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(delta);
                else if (controller != null && controller.enabled) controller.Move(delta);
                else if (body != null && !body.isKinematic) body.MovePosition(body.position + delta); // No residual added velocity after the timed push expires.
                else
                {
                    // Static practice targets also show knockback, without moving through walls.
                    if (!Physics.Raycast(transform.position + Vector3.up * 0.3f, delta.normalized, delta.magnitude + 0.2f, ~0, QueryTriggerInteraction.Ignore)) transform.position += delta;
                }
                push = knockbackMotion.Velocity;
            }
        }
        private void UpdateControls()
        {
            bool stun = HasStatus(StatusKind.Stun) || HasStatus(StatusKind.Stagger);
            bool root = HasStatus(StatusKind.Root);
            float slow = 0;
            foreach (var s in active) if (s.spec.kind == StatusKind.Slow) slow = Mathf.Max(slow, Mathf.Clamp01(s.spec.magnitude));
            IsStunned = stun; IsRooted = root; MovementMultiplier = stun || root ? 0 : 1 - slow;
            if (agent != null)
            {
                if (MovementMultiplier < 1 && !ownsAgent) { originalSpeed = agent.speed; ownsAgent = true; }
                if (ownsAgent) { agent.speed = originalSpeed * MovementMultiplier; if (MovementMultiplier == 1) ownsAgent = false; }
            }
            if (animator != null)
            {
                if (stun && !ownsAnimator) { originalAnimationSpeed = animator.speed; ownsAnimator = true; animator.speed = 0; }
                else if (!stun && ownsAnimator) { animator.speed = originalAnimationSpeed; ownsAnimator = false; }
            }
            if (stun)
            {
                foreach (var b in interruptOnStun)
                    if (b != null && b != this && b != health && b.enabled) { b.enabled = false; suspended.Add(b); }
            }
            else { foreach (var b in suspended) if (b != null) b.enabled = true; suspended.Clear(); }
            StatusChanged?.Invoke();
        }
        public void ClearStatuses()
        {
            active.Clear(); knockbackMotion.Clear(); push = Vector3.zero;
            InfusedElement = ElementType.Normal; infusionLeft = fractureLeft = nextReactionTime = purificationUntil = 0;
            purificationElements = purifiedLayers = 0; shieldBroken = false;
            if (aura != null) { aura.gameObject.SetActive(false); Destroy(aura.gameObject); aura = null; }
            UpdateControls(); ElementChanged?.Invoke();
            if (shieldAura != null) { shieldAura.gameObject.SetActive(false); Destroy(shieldAura.gameObject); shieldAura = null; }
        }
        private void OnDisable() => ClearStatuses();
    }
}
