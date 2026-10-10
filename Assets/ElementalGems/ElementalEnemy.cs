using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ElementalGems
{
    [DisallowMultipleComponent, RequireComponent(typeof(Enemy))]
    public sealed class ElementalEnemy : MonoBehaviour
    {
        public ElementType element;
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
        private void Awake() { health = GetComponent<Enemy>(); agent = GetComponent<NavMeshAgent>(); animator = GetComponentInChildren<Animator>(); }
        public float Resistance(ElementType incoming)
        {
            foreach (var row in resistances) if (row.defender == incoming) return Mathf.Max(0, row.multiplier);
            return 1;
        }
        public bool HasStatus(StatusKind kind) => active.Exists(s => s.spec.kind == kind);
        public void Apply(GemAttack attack, Vector3 direction, bool allowControlEffects = true, float knockbackDurationMultiplier = 1f)
        {
            foreach (var spec in attack.statuses)
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
        public void ApplyKnockback(Vector3 direction, float strength, float durationMultiplier = 1f)
        {
            if (health == null || health.IsDead || !isActiveAndEnabled) return;
            knockbackMotion.Add(direction, Mathf.Max(0, strength - knockbackResistance), durationMultiplier);
            push = knockbackMotion.Velocity;
        }
        private void Update()
        {
            if (health.IsDead) { ClearStatuses(); return; }
            bool changed = false;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var s = active[i];
                float dt = Mathf.Min(s.left, Time.deltaTime);
                s.left -= dt;
                if (s.spec.kind == StatusKind.Burn || s.spec.kind == StatusKind.Poison || s.spec.kind == StatusKind.Void)
                {
                    s.damageRemainder += s.spec.magnitude * dt;
                    int damage = Mathf.FloorToInt(s.damageRemainder + 0.0001f);
                    // Lingering damage is not another weapon impact. Only the
                    // originating contact decides whether to play a Hit reaction.
                    if (damage > 0) { health.TakeDamage(damage, false); s.damageRemainder -= damage; }
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
        public void ClearStatuses() { active.Clear(); knockbackMotion.Clear(); push = Vector3.zero; UpdateControls(); }
        private void OnDisable() => ClearStatuses();
    }
}
