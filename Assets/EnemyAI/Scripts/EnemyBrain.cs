using System;
using UnityEngine;

namespace SoulsLike.Enemies
{
    public enum EnemyState { Idle, Chasing, Attacking, TakingDamage, Dying, Returning, Standby }
    [DisallowMultipleComponent, RequireComponent(typeof(Enemy), typeof(EnemyNavMeshMotor), typeof(EnemyAnimationDriver))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        public EnemyProfile profile;
        [Tooltip("Optional override; otherwise uses the registered living player targets.")]
        public EnemyTarget targetOverride;
        public Transform attackOrigin;
        [Header("Attack standby (per enemy)")]
        [Tooltip("Seconds spent standing idle after each completed attack animation before chasing or attacking again. Set per scene enemy, or on its prefab. Zero disables this pause; attack cooldowns still apply.")]
        [Min(0)] public float postAttackStandbySeconds = 2f;
        [SerializeField] private EnemyState state;
        public EnemyState State => state;
        public float StandbyRemaining => Mathf.Max(0, standbyUntil - Time.time);
        public Enemy Health { get; private set; }
        public EnemyTarget Target { get; private set; }
        public EnemyAttackDefinition CurrentAttack { get; private set; }
        public Vector3 HomePosition { get; private set; }
        public int LastAttackIndex { get; private set; } = -1;
        public event Action<EnemyState> StateChanged;
        public event Action<EnemyAttackDefinition> AttackStarted;
        private EnemyNavMeshMotor motor;
        private EnemyAnimationDriver animationDriver;
        private EnemyControlStatus status;
        private Quaternion homeRotation;
        private float[] attackReady = Array.Empty<float>();
        private float nextSense, nextAttack, stateEntered, previousAttackTime, lastProgress, hitElapsed, standbyUntil;
        private Vector3 progressPosition;
        private bool appliedHit, pendingContact, attackEntered, initialized, hitAnimationActive, hitAnimationEntered;
        private readonly RaycastHit[] sightHits = new RaycastHit[32];
        public Vector3 AttackPosition => attackOrigin != null ? attackOrigin.position : transform.position + Vector3.up;

        private void Awake()
        {
            Health = GetComponent<Enemy>(); motor = GetComponent<EnemyNavMeshMotor>();
            animationDriver = GetComponent<EnemyAnimationDriver>(); status = GetComponent<EnemyControlStatus>();
        }
        private void OnEnable() { Health.OnDamaged += OnDamaged; Health.OnDeath += OnDeath; }
        private void Start()
        {
            if (profile == null) { Debug.LogError("Enemy AI requires an EnemyProfile.", this); enabled = false; return; }
            HomePosition = transform.position; homeRotation = transform.rotation;
            attackReady = new float[profile.attacks.Length]; motor.Configure(profile);
            nextSense = Time.time + UnityEngine.Random.Range(0, profile.detectionInterval);
            initialized = true;
            if (Health.IsDead) OnDeath(); else SetState(EnemyState.Idle);
            if (!motor.Ready) Debug.LogWarning("Enemy is not on a baked NavMesh. Position its root on a compatible surface.", this);
        }
        private void OnDisable()
        {
            if (Health != null) { Health.OnDamaged -= OnDamaged; Health.OnDeath -= OnDeath; }
            if (motor != null) motor.Stop();
            CurrentAttack = null;
            standbyUntil = 0;
            if (initialized && !Health.IsDead) state = EnemyState.Returning;
        }
        private void Update()
        {
            if (!initialized || profile == null) return;
            if (Health.IsDead) { if (state != EnemyState.Dying) OnDeath(); return; }
            if (status != null && status.CannotAct)
            {
                if (state != EnemyState.TakingDamage) Interrupt();
                motor.Stop(); return;
            }
            if (state == EnemyState.TakingDamage)
            {
                // Finish the reaction even if the target dies/escapes during it.
                hitElapsed += Time.deltaTime; // Stun above pauses recovery as well as the animator.
                bool playing = animationDriver.TryGetTime("Hit", out float t);
                hitAnimationEntered |= playing;
                float fallback = profile.hitReactionMilliseconds > 0 ? profile.hitReactionMilliseconds * .001f : profile.hitFallbackDuration;
                float speed = animationDriver.animator != null ? Mathf.Abs(animationDriver.animator.speed) : 1;
                float timeout = Mathf.Max(fallback, (profile.hit != null ? profile.hit.length : 0) / Mathf.Max(.01f, speed) + profile.transitionDuration + .25f);
                bool finished = hitAnimationActive
                    ? (playing && t >= 1) || (hitAnimationEntered && !playing) || hitElapsed >= timeout
                    : hitElapsed >= fallback;
                if (finished)
                {
                    hitAnimationActive = false;
                    animationDriver.Locomotion(false, profile.transitionDuration);
                    if (!CanPursueTarget()) BeginReturn();
                    else SetState(StandbyRemaining > 0 ? EnemyState.Standby : EnemyState.Chasing);
                }
                return;
            }
            if (state == EnemyState.Returning)
            {
                if (Time.time >= nextSense)
                {
                    nextSense = Time.time + profile.detectionInterval;
                    Acquire();
                }
                if (state == EnemyState.Returning) { ReturnHome(); return; }
            }
            if (state != EnemyState.Idle && !CanPursueTarget())
            { BeginReturn(); return; }
            if (state == EnemyState.Standby)
            {
                motor.Face(Target.GroundPosition - transform.position, profile.rotationSpeed);
                if (StandbyRemaining > 0) return;
                SetState(EnemyState.Chasing);
            }
            if (state == EnemyState.Attacking) { TickAttack(); return; }
            if (Time.time >= nextSense)
            {
                nextSense = Time.time + profile.detectionInterval;
                if (state == EnemyState.Idle) Acquire();
            }
            if (state != EnemyState.Chasing || !Valid(Target)) return;
            float distance = Vector3.Distance(transform.position, Target.GroundPosition);
            if (Time.time >= nextAttack)
            {
                int index = profile.selector != null ? profile.selector.Select(this, Target) : ChooseAttack();
                if (CanSelectAttack(index))
                {
                    var attack = profile.attacks[index];
                    motor.Face(Target.GroundPosition - transform.position, profile.rotationSpeed);
                    if (WithinArc(Target, attack.arc)) { BeginAttack(index); return; }
                }
            }
            float minimum = MinimumEngagementRange();
            if (distance < minimum)
            {
                Vector3 away = Vector3.ProjectOnPlane(transform.position - Target.GroundPosition, Vector3.up).normalized;
                if (away.sqrMagnitude < .01f) away = -transform.forward;
                Vector3 retreat = Target.GroundPosition + away * (minimum + .75f);
                if (!OutsideTerritory(retreat)) motor.MoveTo(retreat, .1f, profile, status != null ? status.MovementMultiplier : 1);
                else motor.Stop();
                AnimateMotion(); CheckProgress(); return;
            }
            float range = PreferredRange(distance);
            bool visible = HasLineOfSight(Target);
            bool canStand = distance <= range && visible;
            if (canStand)
            {
                motor.Stop(); motor.Face(Target.GroundPosition - transform.position, profile.rotationSpeed);
                animationDriver.Locomotion(false, profile.transitionDuration); lastProgress = Time.time;
            }
            else
            {
                motor.MoveTo(Target.GroundPosition, visible ? Mathf.Max(.1f, range * .8f) : .1f, profile, status != null ? status.MovementMultiplier : 1);
                AnimateMotion();
                CheckProgress();
            }
        }
        private static bool Valid(EnemyTarget target) => target != null && target.isActiveAndEnabled && target.IsAlive;
        private bool OutsideTerritory(Vector3 position) => (position - HomePosition).sqrMagnitude > profile.chaseRadius * profile.chaseRadius;
        private bool CanPursueTarget() => Valid(Target) && !OutsideTerritory(Target.GroundPosition) && !OutsideTerritory(transform.position);
        private void Acquire()
        {
            if (OutsideTerritory(transform.position)) return;
            EnemyTarget nearest = null; float best = profile.detectionRadius * profile.detectionRadius;
            if (targetOverride != null)
            { if (Valid(targetOverride) && !OutsideTerritory(targetOverride.GroundPosition) && (targetOverride.GroundPosition - transform.position).sqrMagnitude <= best && (!profile.requireLineOfSight || HasLineOfSight(targetOverride))) nearest = targetOverride; }
            else foreach (var candidate in EnemyTarget.Active)
            {
                if (!Valid(candidate) || OutsideTerritory(candidate.GroundPosition)) continue;
                float sqr = (candidate.GroundPosition - transform.position).sqrMagnitude;
                if (sqr > best || (profile.requireLineOfSight && !HasLineOfSight(candidate))) continue;
                nearest = candidate; best = sqr;
            }
            if (nearest == null) return;
            Target = nearest; SetState(EnemyState.Chasing);
        }
        public bool HasLineOfSight(EnemyTarget target)
        {
            if (!Valid(target)) return false;
            Vector3 delta = target.AimPosition - AttackPosition;
            int count = Physics.RaycastNonAlloc(AttackPosition, delta.normalized, sightHits, delta.magnitude, profile.obstructionLayers, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false; // Fail closed in unusually dense geometry.
            for (int i = 0; i < count; i++)
            {
                Transform hit = sightHits[i].transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(target.transform)) continue;
                return false;
            }
            return true;
        }
        private bool WithinArc(EnemyTarget target, float arc)
        {
            Vector3 delta = Vector3.ProjectOnPlane(target.GroundPosition - transform.position, Vector3.up);
            return delta.sqrMagnitude < .001f || Vector3.Angle(transform.forward, delta) <= arc * .5f;
        }
        public bool CanHit(EnemyTarget target, EnemyAttackDefinition attack) =>
            state == EnemyState.Attacking && !Health.IsDead && (status == null || !status.CannotAct) && Valid(target) &&
            Vector3.Distance(transform.position, target.GroundPosition) <= attack.range &&
            WithinArc(target, attack.arc) && HasLineOfSight(target);
        public bool CanSelectAttack(int index)
        {
            if (profile == null || !Valid(Target) || index < 0 || index >= profile.attacks.Length || index >= attackReady.Length) return false;
            var attack = profile.attacks[index];
            if (attack == null || !attack.IsUsable || Time.time < attackReady[index] || !animationDriver.HasState(attack.stateName)) return false;
            float hp = (float)Health.CurrentHealth / Health.MaxHealth;
            float distance = Vector3.Distance(transform.position, Target.GroundPosition);
            return hp >= attack.minimumHealthRatio && hp <= attack.maximumHealthRatio && distance >= attack.minimumRange && distance <= attack.range && HasLineOfSight(Target);
        }
        private int ChooseAttack()
        {
            int priority = int.MinValue, alternatives = 0;
            for (int i = 0; i < profile.attacks.Length; i++) if (CanSelectAttack(i))
            {
                int p = profile.attacks[i].priority;
                if (p > priority) { priority = p; alternatives = 0; }
                if (p == priority && i != LastAttackIndex) alternatives++;
            }
            bool skipLast = profile.avoidRepeatingAttack && alternatives > 0;
            float total = 0;
            for (int i = 0; i < profile.attacks.Length; i++) if (CanSelectAttack(i) && profile.attacks[i].priority == priority && (!skipLast || i != LastAttackIndex)) total += profile.attacks[i].weight;
            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < profile.attacks.Length; i++) if (CanSelectAttack(i) && profile.attacks[i].priority == priority && (!skipLast || i != LastAttackIndex))
            { roll -= profile.attacks[i].weight; if (roll <= 0) return i; }
            return -1;
        }
        private float PreferredRange(float distance)
        {
            float best = .8f, score = float.MaxValue;
            foreach (var attack in profile.attacks)
            {
                if (attack == null || !attack.IsUsable) continue;
                float hp = (float)Health.CurrentHealth / Health.MaxHealth;
                if (hp < attack.minimumHealthRatio || hp > attack.maximumHealthRatio) continue;
                float cost = Mathf.Abs(distance - attack.range * .85f);
                if (cost < score) { score = cost; best = attack.range * .85f; }
            }
            return best;
        }
        private float MinimumEngagementRange()
        {
            float minimum = float.MaxValue;
            foreach (var attack in profile.attacks)
            {
                if (attack == null || !attack.IsUsable) continue;
                float hp = (float)Health.CurrentHealth / Health.MaxHealth;
                if (hp >= attack.minimumHealthRatio && hp <= attack.maximumHealthRatio) minimum = Mathf.Min(minimum, attack.minimumRange);
            }
            return minimum == float.MaxValue ? 0 : minimum;
        }
        private void BeginAttack(int index)
        {
            CurrentAttack = profile.attacks[index]; LastAttackIndex = index;
            attackReady[index] = Time.time + CurrentAttack.cooldown;
            appliedHit = pendingContact = attackEntered = false; previousAttackTime = 0;
            SetState(EnemyState.Attacking);
            animationDriver.PlayAttack(CurrentAttack, profile.transitionDuration);
            AttackStarted?.Invoke(CurrentAttack);
        }
        private void TickAttack()
        {
            var attack = CurrentAttack;
            if (attack == null || !Valid(Target)) { BeginReturn(); return; }
            if (!animationDriver.TryGetTime(attack.stateName, out float time))
            {
                if (attackEntered || Time.time - stateEntered > 1) FinishAttack();
                return;
            }
            attackEntered = true;
            animationDriver.UpdateAttackSpeed(attack, time);
            if (time <= attack.turnUntil) motor.Face(Target.GroundPosition - transform.position, profile.rotationSpeed);
            // Exact frame mode never delivers a delayed hit outside the authored window.
            // Legacy normalized mode retains crossing detection for existing profiles.
            bool inWindow = attack.useFrameWindow ? attack.IsInsideFrameWindow(time)
                : time >= attack.hitStart && previousAttackTime <= attack.hitEnd;
            if (attack.damageAtWindowEnd && !appliedHit)
            {
                // Give the target the whole visible window to parry. The end
                // resolution cannot hit a target that only arrives afterward.
                bool activeWindow = attack.useFrameWindow ? inWindow : time >= attack.hitStart && time < attack.hitEnd;
                if (activeWindow && CanHit(Target, attack))
                {
                    pendingContact = true;
                    if (Target.TryParryAttack(this)) { appliedHit = true; return; }
                }
                float end = attack.useFrameWindow
                    ? attack.hitEndFrame / (attack.animation.length * attack.animation.frameRate) : attack.hitEnd;
                if (time >= end)
                {
                    appliedHit = true;
                    if (pendingContact && CanHit(Target, attack))
                        attack.effect.Execute(new EnemyAttackContext(this, Target, attack));
                    if (state != EnemyState.Attacking) return;
                }
            }
            else if (!attack.damageAtWindowEnd && !appliedHit && inWindow)
            {
                if (CanHit(Target, attack))
                {
                    appliedHit = true;
                    attack.effect.Execute(new EnemyAttackContext(this, Target, attack));
                }
                else if (!attack.useFrameWindow) appliedHit = true;
                if (state != EnemyState.Attacking) return; // A parry can interrupt inside Execute.
            }
            previousAttackTime = time;
            if (time >= 1) FinishAttack();
        }
        private void FinishAttack()
        {
            CurrentAttack = null;
            if (!CanPursueTarget()) { BeginReturn(); return; }
            standbyUntil = Time.time + Mathf.Max(0, postAttackStandbySeconds);
            nextAttack = Mathf.Max(standbyUntil, Time.time + profile.globalAttackCooldown);
            SetState(StandbyRemaining > 0 ? EnemyState.Standby : EnemyState.Chasing);
        }
        private void OnDamaged(int damage)
        {
            if (damage <= 0 || Health.IsDead || !initialized || state == EnemyState.TakingDamage) return;
            if (!Valid(Target)) Acquire();
            if (Health.DamageRequestsHitReaction) Interrupt();
        }
        public void Interrupt()
        {
            if (!initialized || Health.IsDead || state == EnemyState.Dying || state == EnemyState.TakingDamage) return;
            hitElapsed = 0; hitAnimationEntered = false;
            float duration = profile.hit == null ? profile.hitReactionMilliseconds * .001f : 0;
            CurrentAttack = null; nextAttack = Mathf.Max(nextAttack, Time.time + (duration > 0 ? duration : profile.globalAttackCooldown));
            SetState(EnemyState.TakingDamage);
            float blend = duration > 0 ? Mathf.Min(profile.transitionDuration, duration * .25f) : profile.transitionDuration;
            hitAnimationActive = profile.hit != null && animationDriver.Play("Hit", blend, true);
            if (!hitAnimationActive) animationDriver.Locomotion(false, blend);
        }
        private void OnDeath()
        {
            if (!initialized || state == EnemyState.Dying) return;
            CurrentAttack = null; Target = null; standbyUntil = 0;
            if (status != null) status.ClearOnDeath();
            SetState(EnemyState.Dying);
            if (animationDriver.animator != null) animationDriver.animator.speed = 1;
            animationDriver.Play("Die", profile.transitionDuration, true);
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (motor.Agent != null) motor.Agent.enabled = false;
            if (profile.corpseLifetime > 0) Destroy(gameObject, Mathf.Max(profile.corpseLifetime, profile.die != null ? profile.die.length : 0));
        }
        private void BeginReturn() { Target = null; CurrentAttack = null; standbyUntil = 0; SetState(EnemyState.Returning); }
        private void ReturnHome()
        {
            if (Vector3.Distance(transform.position, HomePosition) <= profile.homeTolerance)
            {
                motor.Stop(); transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRotation, profile.rotationSpeed * Time.deltaTime);
                animationDriver.Locomotion(false, profile.transitionDuration);
                if (Quaternion.Angle(transform.rotation, homeRotation) < 2) { nextSense = Time.time + profile.detectionInterval; SetState(EnemyState.Idle); }
                return;
            }
            motor.MoveTo(HomePosition, profile.homeTolerance * .5f, profile, status != null ? status.MovementMultiplier : 1);
            AnimateMotion(); // Retry safely if home is temporarily inaccessible; never teleport through geometry.
        }
        private void CheckProgress()
        {
            if ((transform.position - progressPosition).sqrMagnitude > .04f || (status != null && status.MovementMultiplier <= 0))
            { progressPosition = transform.position; lastProgress = Time.time; }
            if (Time.time - lastProgress > profile.unreachableTimeout)
            {
                // Congestion or an obstacle is not a reason to forget a nearby player.
                // Stop clears the cached path; the next chase tick requests a fresh route.
                motor.Stop(); progressPosition = transform.position; lastProgress = Time.time;
            }
        }
        private void AnimateMotion() => animationDriver.Locomotion(motor.Ready && motor.Agent.velocity.sqrMagnitude > .01f, profile.transitionDuration);
        private void SetState(EnemyState next)
        {
            if (next != EnemyState.Attacking) animationDriver.ResetAttackSpeed();
            state = next; stateEntered = lastProgress = Time.time; progressPosition = transform.position;
            if (next != EnemyState.Chasing && next != EnemyState.Returning) motor.Stop();
            if (next == EnemyState.Idle || next == EnemyState.Standby) animationDriver.Locomotion(false, profile.transitionDuration);
            StateChanged?.Invoke(next);
        }
        private void OnDrawGizmosSelected()
        {
            if (profile == null) return;
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, profile.detectionRadius);
            Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(Application.isPlaying ? HomePosition : transform.position, profile.chaseRadius);
            Gizmos.color = Color.red;
            foreach (var a in profile.attacks) if (a != null && a.available) Gizmos.DrawWireSphere(transform.position, a.range);
        }
    }
}
