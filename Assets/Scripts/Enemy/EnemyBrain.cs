using System;
using UnityEngine;

namespace SoulsLike.Enemies
{
    [Serializable]
    public sealed class EnemySpecialDebugSlot
    {
        public bool play;
        public EnemyAttackDefinition attack;
    }
    public enum EnemyState { Idle, Chasing, Attacking, TakingDamage, Dying, Returning, Standby, ResetPosition, Spawning }
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
        [Header("Enemy Special Attack Debug (this instance only)")]
        [Tooltip("Use only the checked specials on THIS enemy. Health milestones and return-to-spawn still apply unless Bypass Health Milestone is enabled. Editor/development builds only.")]
        public bool debugRepeatSpecialAttacks;
        [Tooltip("OFF: lose the configured HP, return home, then play a checked special. ON: immediately repeat checked specials for animation debugging.")]
        public bool debugBypassHealthMilestone;
        public bool debugIgnoreSpecialCooldown = true;
        [Min(0)] public float debugSpecialRepeatDelay = .5f;
        public EnemySpecialDebugSlot[] debugSpecialAttacks = Array.Empty<EnemySpecialDebugSlot>();
        [SerializeField] private EnemyState state;
        public EnemyState State => state;
        public float StandbyRemaining => Mathf.Max(0, standbyUntil - Time.time);
        public Enemy Health { get; private set; }
        public EnemyTarget Target { get; private set; }
        public EnemyAttackDefinition CurrentAttack { get; private set; }
        public Vector3 HomePosition { get; private set; }
        public int LastAttackIndex { get; private set; } = -1;
        public EnemyAttackDefinition CurrentSequence { get; private set; }
        public int CurrentSequenceStep { get; private set; }
        public float SequencePauseRemaining => queuedSequenceStep != null ? Mathf.Max(0, sequenceResumeAt - Time.time) : 0;
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
        private bool actionExecuted;
        private bool attackShakePlayed;
        private bool attackCueShown, protectionBrokenByParry;
        private EnemyAttackCue attackCue;
        private EnemyAttackDefinition queuedSequenceStep;
        private float sequenceResumeAt;
        public bool IsAttackProtected
        {
            get
            {
                var attack = CurrentAttack;
                if (!isActiveAndEnabled || state != EnemyState.Attacking || protectionBrokenByParry || attack == null ||
                    !attack.HasDamagePayload || !attack.showAttackCue || !attack.invulnerableAfterCue || attack.animation == null) return false;
                return animationDriver != null && animationDriver.TryGetTime(attack.stateName, out float time) &&
                    time < 1f && time * attack.animation.length * attack.animation.frameRate >= attack.attackCueFrame;
            }
        }
        private bool spawnAnimationEntered;
        private int pendingHealthSpecials, healthSpecialThresholdsCrossed, lastHealthSpecialIndex = -1;
        private bool healthSpecialReturnRequired;
        private bool ImmediateSpecialDebug => SpecialDebugActive && debugBypassHealthMilestone;
        private bool SpecialDebugActive
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return isActiveAndEnabled && debugRepeatSpecialAttacks && profile != null;
#else
                return false;
#endif
            }
        }
        private bool IsSpecialChecked(EnemyAttackDefinition attack)
        {
            if (attack == null || debugSpecialAttacks == null) return false;
            foreach (var slot in debugSpecialAttacks)
                if (slot != null && slot.play && slot.attack == attack) return true;
            return false;
        }
        public int PendingHealthSpecials => pendingHealthSpecials;
        private float nextResetHealthPercent;
        private bool resetPositionPending;
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
            attackReady = new float[profile.attacks.Length]; motor.Configure(profile);
            HomePosition = motor.ResolveHomePosition(transform.position); homeRotation = transform.rotation;
            nextSense = Time.time + UnityEngine.Random.Range(0, profile.detectionInterval);
            initialized = true;
            nextResetHealthPercent = 100f - Mathf.Clamp(profile.resetPositionHealthInterval, 1f, 100f);
            QueueResetPosition();
            QueueHealthSpecial();
            if (Health.IsDead) OnDeath();
            else if (profile.spawnAnimation != null && !profile.spawnAnimation.isLooping && animationDriver.HasState("Spawn"))
            {
                SetState(EnemyState.Spawning);
                // Start at frame zero with no idle-to-spawn blend exposing the standing pose.
                animationDriver.Play("Spawn", 0, true);
            }
            else SetState(EnemyState.Idle);
            if (!motor.Ready) Debug.LogWarning("Enemy is not on a baked NavMesh. Position its root on a compatible surface.", this);
        }
        private void OnDisable()
        {
            if (attackCue != null) attackCue.Hide();
            if (Health != null) { Health.OnDamaged -= OnDamaged; Health.OnDeath -= OnDeath; }
            if (motor != null) motor.Stop();
            if (motor != null) motor.CancelResetPosition();
            if (motor != null) motor.CancelAttackMovement();
            CurrentAttack = null;
            CurrentSequence = null; CurrentSequenceStep = 0;
            queuedSequenceStep = null; sequenceResumeAt = 0;
            standbyUntil = 0;
            if (initialized && !Health.IsDead) state = EnemyState.Returning;
        }
        private void Update()
        {
            if (!initialized || profile == null) return;
            // Older ordinary prefabs can acquire elemental control on their first gem hit.
            if (status == null) status = GetComponent<EnemyControlStatus>();
            if (Health.IsDead) { if (state != EnemyState.Dying) OnDeath(); return; }
            if (status != null && status.CannotAct && !IsAttackProtected)
            {
                if (state != EnemyState.TakingDamage && state != EnemyState.ResetPosition && state != EnemyState.Spawning) Interrupt();
                motor.Stop(); return;
            }
            if (state == EnemyState.Spawning) { TickSpawn(); return; }
            if (state == EnemyState.ResetPosition) { TickResetPosition(); return; }
            if (resetPositionPending && !ImmediateSpecialDebug && state != EnemyState.TakingDamage && !IsAttackProtected)
            {
                if (BeginResetPosition()) return;
                FallBackToWalkingReset();
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
            if (!ImmediateSpecialDebug && pendingHealthSpecials > 0 && state != EnemyState.Attacking)
            {
                if (healthSpecialReturnRequired)
                {
                    // Failed/unavailable jump paths fall back to walking home; no attacks before arrival.
                    if (!HasReachedHome())
                    {
                        motor.MoveTo(HomePosition, profile.homeTolerance * .5f, profile, status != null ? status.MovementMultiplier : 1);
                        AnimateMotion(); return;
                    }
                    CompleteHealthSpecialReturn();
                }
                if (!Valid(Target) && Time.time >= nextSense) { nextSense = Time.time + profile.detectionInterval; Acquire(); }
                motor.Stop();
                animationDriver.Locomotion(false, profile.transitionDuration);
                if (CanPursueTarget())
                {
                    motor.Face(Target.GroundPosition - transform.position, profile.rotationSpeed);
                    if (Time.time >= nextAttack)
                    {
                        int special = ChooseAttack(true);
                        if (special >= 0 && (profile.attacks[special].HasApproachPhase || WithinArc(Target, profile.attacks[special].arc))) BeginAttack(special);
                    }
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
            if (state != EnemyState.Idle && !CanPursueTarget() && !IsAttackProtected)
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
                int index = ImmediateSpecialDebug ? ChooseAttack(true) : -1;
                if (index < 0 && !ImmediateSpecialDebug) index = profile.selector != null ? profile.selector.Select(this, Target) : ChooseAttack();
                if (CanUseAttack(index) && (profile.attacks[index].trigger == EnemyAttackTrigger.Normal || SpecialDebugActive || pendingHealthSpecials > 0))
                {
                    var attack = profile.attacks[index];
                    motor.Face(Target.GroundPosition - transform.position, profile.rotationSpeed);
                    if (attack.HasApproachPhase || WithinArc(Target, attack.arc)) { BeginAttack(index); return; }
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
        private bool HasReachedHome() => Vector3.Distance(motor.NavigationPosition, HomePosition) <= profile.homeTolerance;
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
        public bool CanHit(EnemyTarget target, EnemyAttackDefinition attack)
        {
            if (state != EnemyState.Attacking || Health.IsDead || (status != null && status.CannotAct) || !Valid(target)) return false;
            if (attack.slash != null && attack.slash.enabled && attack.slash.thrust)
            {
                // A thrust contacts the forward lance, not the broad melee selection radius.
                Vector3 delta = target.GroundPosition - motor.NavigationPosition;
                Vector3 local = Quaternion.Inverse(transform.rotation) * delta - attack.slash.localOffset;
                float scale = attack.slash.size;
                float reach = ElementalGems.GemCrescentSlash.ThrustLength * scale * attack.slash.visualScale.z;
                float width = ElementalGems.GemCrescentSlash.ThrustHalfWidth * scale * attack.slash.visualScale.x;
                return Mathf.Abs(delta.y) <= 2f && local.z >= -.35f && local.z <= reach + .35f &&
                    Mathf.Abs(local.x) <= width + .3f && HasLineOfSight(target);
            }
            return Vector3.Distance(transform.position, target.GroundPosition) <= attack.range &&
                WithinArc(target, attack.arc) && HasLineOfSight(target);
        }
        public bool CanSelectAttack(int index)
            => CanUseAttack(index) && profile.attacks[index].trigger == EnemyAttackTrigger.Normal && !ImmediateSpecialDebug;
        private bool CanUseAttack(int index)
        {
            if (profile == null || !Valid(Target) || index < 0 || index >= profile.attacks.Length || index >= attackReady.Length) return false;
            var attack = profile.attacks[index];
            bool debug = SpecialDebugActive && IsSpecialChecked(attack);
            if (attack == null || !attack.IsUsable ||
                (Time.time < attackReady[index] && !(debug && debugIgnoreSpecialCooldown)) || !animationDriver.HasState(attack.stateName)) return false;
            if (!debug && attack.animationAction != null && !attack.animationAction.CanStart(this)) return false;
            if (attack.repeatCount == 0 && attack.followUpSteps != null) foreach (var step in attack.followUpSteps)
                if (step.available && !animationDriver.HasState(step.stateName)) return false;
            float hp = (float)Health.CurrentHealth / Health.MaxHealth;
            float distance = Vector3.Distance(transform.position, Target.GroundPosition);
            bool selfCast = attack.animationAction != null && !attack.dealsDamage;
            return (debug || (hp >= attack.minimumHealthRatio && hp <= attack.maximumHealthRatio)) &&
                (selfCast || attack.HasApproachPhase ||
                    (distance >= attack.minimumRange && distance <= attack.range && HasLineOfSight(Target)));
        }
        private bool CanChooseAttack(int index, bool special)
        {
            if (!CanUseAttack(index)) return false;
            var attack = profile.attacks[index];
            if ((attack.trigger == EnemyAttackTrigger.HealthLossSpecial) != special) return false;
            if (special) return !SpecialDebugActive || IsSpecialChecked(attack);
            return !ImmediateSpecialDebug;
        }
        private int ChooseAttack(bool special = false)
        {
            int last = special ? lastHealthSpecialIndex : LastAttackIndex;
            int priority = int.MinValue, alternatives = 0;
            for (int i = 0; i < profile.attacks.Length; i++) if (CanChooseAttack(i, special))
            {
                int p = profile.attacks[i].priority;
                if (p > priority) { priority = p; alternatives = 0; }
                if (p == priority && i != last) alternatives++;
            }
            bool skipLast = profile.avoidRepeatingAttack && alternatives > 0;
            float total = 0;
            for (int i = 0; i < profile.attacks.Length; i++) if (CanChooseAttack(i, special) && profile.attacks[i].priority == priority && (!skipLast || i != last)) total += profile.attacks[i].weight;
            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < profile.attacks.Length; i++) if (CanChooseAttack(i, special) && profile.attacks[i].priority == priority && (!skipLast || i != last))
            { roll -= profile.attacks[i].weight; if (roll <= 0) return i; }
            return -1;
        }
        private float PreferredRange(float distance)
        {
            float best = .8f, score = float.MaxValue;
            foreach (var attack in profile.attacks)
            {
                if (attack == null || !attack.IsUsable) continue;
                if (!ConsiderAttackForMovement(attack)) continue;
                if (!SpecialDebugActive && attack.animationAction != null && !attack.animationAction.CanStart(this)) continue;
                float hp = (float)Health.CurrentHealth / Health.MaxHealth;
                if (!SpecialDebugActive && (hp < attack.minimumHealthRatio || hp > attack.maximumHealthRatio)) continue;
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
                if (!ConsiderAttackForMovement(attack)) continue;
                if (!SpecialDebugActive && attack.animationAction != null && !attack.animationAction.CanStart(this)) continue;
                float hp = (float)Health.CurrentHealth / Health.MaxHealth;
                if (SpecialDebugActive || (hp >= attack.minimumHealthRatio && hp <= attack.maximumHealthRatio)) minimum = Mathf.Min(minimum, attack.minimumRange);
            }
            return minimum == float.MaxValue ? 0 : minimum;
        }
        private void BeginAttack(int index)
        {
            CurrentSequence = profile.attacks[index]; CurrentSequenceStep = 0; LastAttackIndex = index;
            if (CurrentSequence.trigger == EnemyAttackTrigger.HealthLossSpecial)
            {
                pendingHealthSpecials = Mathf.Max(0, pendingHealthSpecials - 1);
                lastHealthSpecialIndex = index;
            }
            attackReady[index] = Time.time + CurrentSequence.cooldown;
            BeginAttackStep(CurrentSequence);
        }
        private void BeginAttackStep(EnemyAttackDefinition step)
        {
            queuedSequenceStep = null; sequenceResumeAt = 0;
            CurrentAttack = step;
            appliedHit = pendingContact = attackEntered = false; previousAttackTime = 0;
            actionExecuted = false;
            attackShakePlayed = false;
            attackCueShown = protectionBrokenByParry = false;
            if (attackCue != null) attackCue.Hide();
            SetState(EnemyState.Attacking);
            if (step.lockFacingAtStart && !step.HasApproachPhase && Valid(Target))
            {
                Vector3 direction = Vector3.ProjectOnPlane(Target.GroundPosition - motor.NavigationPosition, Vector3.up);
                if (direction.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(direction);
            }
            motor.BeginAttackMovement(step, Target);
            animationDriver.PlayAttack(CurrentAttack, profile.transitionDuration);
            AttackStarted?.Invoke(CurrentAttack);
        }
        private void TickAttack()
        {
            if (queuedSequenceStep != null)
            {
                if (!CanPursueTarget() || (resetPositionPending && !ImmediateSpecialDebug)) { FinishAttack(); return; }
                if (Time.time < sequenceResumeAt) return;
                BeginAttackStep(queuedSequenceStep);
                return;
            }
            var attack = CurrentAttack;
            if (attack == null || (!Valid(Target) && !IsAttackProtected)) { BeginReturn(); return; }
            if (!animationDriver.TryGetTime(attack.stateName, out float time))
            {
                if (attackEntered || Time.time - stateEntered > 1) FinishAttack();
                return;
            }
            attackEntered = true;
            float frame = time * attack.animation.length * attack.animation.frameRate;
            if (!attackCueShown && attack.showAttackCue && attack.HasDamagePayload && frame >= attack.attackCueFrame && time < 1f)
            {
                attackCueShown = true;
                if (attackCue == null) attackCue = gameObject.AddComponent<EnemyAttackCue>();
                attackCue.Show(attack, animationDriver.animator);
            }
            animationDriver.UpdateAttackSpeed(attack, time);
            bool approaching = motor.TickAttackApproach(attack, time, Target, profile, status != null ? status.MovementMultiplier : 1);
            if (!approaching) motor.TickAttackMovement(attack, time);
            if (!attack.lockFacingAtStart && time <= attack.turnUntil && Valid(Target))
                motor.Face(Target.GroundPosition - transform.position, profile.rotationSpeed);
            // Exact frame mode never delivers a delayed hit outside the authored window.
            // Legacy normalized mode retains crossing detection for existing profiles.
            bool inWindow = attack.useFrameWindow ? attack.IsInsideFrameWindow(time)
                : time >= attack.hitStart && previousAttackTime <= attack.hitEnd;
            // Cross the authored impact frame once per step, even when a rendered frame is skipped.
            // Ground slams dispatch their own stronger pulse at the actual ground impact.
            float shakeTime = attack.useFrameWindow
                ? attack.hitStartFrame / (attack.animation.length * attack.animation.frameRate) : attack.hitStart;
            if (!attackShakePlayed && attack.dealsDamage && !(attack.animationAction is EnemyGroundSlamAction) && time >= shakeTime)
            {
                attackShakePlayed = true;
                CombatCameraShake.EnemyAttack(AttackPosition, attack.attackType == EnemyAttackType.Special ||
                    attack.movement == EnemyAttackMotion.Jump);
            }
            if (attack.dealsDamage && attack.damageAtWindowEnd && !appliedHit)
            {
                // Give the target the whole visible window to parry. The end
                // resolution cannot hit a target that only arrives afterward.
                bool activeWindow = attack.useFrameWindow ? inWindow : time >= attack.hitStart && time < attack.hitEnd;
                if (activeWindow && CanHit(Target, attack))
                {
                    pendingContact = true;
                    if (attack.canBeParried && Target.TryParryAttack(this)) { appliedHit = true; return; }
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
            else if (attack.dealsDamage && !attack.damageAtWindowEnd && !appliedHit && inWindow)
            {
                if (CanHit(Target, attack))
                {
                    appliedHit = true;
                    attack.effect.Execute(new EnemyAttackContext(this, Target, attack));
                }
                else if (!attack.useFrameWindow) appliedHit = true;
                if (state != EnemyState.Attacking) return; // A parry can interrupt inside Execute.
            }
            if (!actionExecuted && attack.animationAction != null &&
                time * attack.animation.length * attack.animation.frameRate >= attack.actionFrame)
            {
                // Timeline crossing survives a slow frame, but cannot run after a parry or interruption above.
                actionExecuted = true;
                if (attack.animationAction.CanStart(this))
                    attack.animationAction.Execute(new EnemyAttackContext(this, Target, attack));
                if (state != EnemyState.Attacking || CurrentAttack != attack) return;
            }
            previousAttackTime = time;
            if (time >= 1)
            {
                if (CurrentSequence != null && CurrentSequence.repeatCount > 0)
                {
                    if (CurrentSequenceStep + 1 < CurrentSequence.repeatCount && CanPursueTarget() &&
                        (!resetPositionPending || ImmediateSpecialDebug))
                    {
                        CurrentSequenceStep++;
                        QueueSequenceStep(CurrentSequence);
                        return;
                    }
                    FinishAttack();
                    return;
                }
                while (CurrentSequence != null && CurrentSequence.followUpSteps != null &&
                    CurrentSequenceStep < CurrentSequence.followUpSteps.Length && CanPursueTarget() && (!resetPositionPending || ImmediateSpecialDebug))
                {
                    var next = CurrentSequence.followUpSteps[CurrentSequenceStep++];
                    if (next != null && !next.available) continue;
                    if (!SpecialDebugActive && next != null && next.animationAction != null && !next.animationAction.CanStart(this)) continue;
                    if (next != null && next.IsUsable && animationDriver.HasState(next.stateName)) { QueueSequenceStep(next); return; }
                    break;
                }
                FinishAttack();
            }
        }
        private void QueueSequenceStep(EnemyAttackDefinition next)
        {
            float delay = CurrentSequence != null ? Mathf.Max(0, CurrentSequence.repeatDelay) : 0;
            if (delay <= 0) { BeginAttackStep(next); return; }
            queuedSequenceStep = next; sequenceResumeAt = Time.time + delay;
            // Stay in the sequence while idle; clear the completed step so the gap cannot hit or move.
            CurrentAttack = null;
            motor.CancelAttackMovement(); motor.Stop();
            if (attackCue != null) attackCue.Hide();
            animationDriver.Locomotion(false, profile.transitionDuration);
        }
        private void FinishAttack()
        {
            queuedSequenceStep = null; sequenceResumeAt = 0;
            CurrentAttack = null;
            if (!CanPursueTarget()) { BeginReturn(); return; }
            standbyUntil = Time.time + Mathf.Max(0, ImmediateSpecialDebug ? debugSpecialRepeatDelay : postAttackStandbySeconds);
            nextAttack = Mathf.Max(standbyUntil, Time.time + profile.globalAttackCooldown);
            SetState(StandbyRemaining > 0 ? EnemyState.Standby : EnemyState.Chasing);
        }
        private void OnDamaged(int damage)
        {
            if (damage > 0 && initialized && !Health.IsDead) { QueueResetPosition(); QueueHealthSpecial(); }
            if (state == EnemyState.ResetPosition || state == EnemyState.Spawning) return;
            if (damage <= 0 || Health.IsDead || !initialized || state == EnemyState.TakingDamage) return;
            if (!Valid(Target)) Acquire();
            if (Health.DamageRequestsHitReaction) Interrupt();
        }
        public void Interrupt()
        {
            if (IsAttackProtected) return;
            if (!initialized || Health.IsDead || state == EnemyState.Dying || state == EnemyState.TakingDamage || state == EnemyState.ResetPosition || state == EnemyState.Spawning) return;
            hitElapsed = 0; hitAnimationEntered = false;
            float duration = profile.hit == null ? profile.hitReactionMilliseconds * .001f : 0;
            CurrentAttack = null; nextAttack = Mathf.Max(nextAttack, Time.time + (duration > 0 ? duration : profile.globalAttackCooldown));
            SetState(EnemyState.TakingDamage);
            float blend = duration > 0 ? Mathf.Min(profile.transitionDuration, duration * .25f) : profile.transitionDuration;
            hitAnimationActive = profile.hit != null && animationDriver.Play("Hit", blend, true);
            if (!hitAnimationActive) animationDriver.Locomotion(false, blend);
        }
        public void ReceiveSuccessfulParry()
        {
            if (CurrentAttack == null || !CurrentAttack.canBeParried || state != EnemyState.Attacking) return;
            protectionBrokenByParry = true;
            Interrupt();
        }
        private void OnDeath()
        {
            if (!initialized || state == EnemyState.Dying) return;
            motor.CancelResetPosition(); resetPositionPending = false;
            pendingHealthSpecials = 0;
            CurrentAttack = null; Target = null; standbyUntil = 0;
            if (status != null) status.ClearOnDeath();
            SetState(EnemyState.Dying);
            if (animationDriver.animator != null) animationDriver.animator.speed = 1;
            animationDriver.Play("Die", profile.transitionDuration, true);
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (motor.Agent != null) motor.Agent.enabled = false;
            if (profile.corpseLifetime > 0) Destroy(gameObject, Mathf.Max(profile.corpseLifetime, profile.die != null ? profile.die.length : 0));
        }
        private void BeginReturn()
        {
            Target = null; CurrentAttack = null; standbyUntil = 0;
            motor.Stop();
            SetState(EnemyState.Returning);
            animationDriver.Locomotion(false, profile.transitionDuration);
        }
        private void TickSpawn()
        {
            if (animationDriver.TryGetTime("Spawn", out float time))
            {
                spawnAnimationEntered = true;
                if (time < 1f) return;
            }
            else if (!spawnAnimationEntered && Time.time - stateEntered <= 1f) return;
            else Debug.LogWarning("Spawn animation left its state before completion. Rebuild this enemy's controller.", this);
            SetState(EnemyState.Idle);
            nextSense = Time.time;
            Acquire();
        }
        private bool ConsiderAttackForMovement(EnemyAttackDefinition attack)
        {
            if (ImmediateSpecialDebug) return attack.trigger == EnemyAttackTrigger.HealthLossSpecial && IsSpecialChecked(attack);
            if (SpecialDebugActive && attack.trigger == EnemyAttackTrigger.HealthLossSpecial && !IsSpecialChecked(attack)) return false;
            return attack.trigger == EnemyAttackTrigger.Normal || pendingHealthSpecials > 0;
        }
        private void QueueHealthSpecial()
        {
            if (!profile.healthLossSpecialsEnabled || Health.IsDead) return;
            int crossed = (Health.MaxHealth - Health.CurrentHealth) / Mathf.Max(1, profile.healthLossSpecialInterval);
            if (crossed <= healthSpecialThresholdsCrossed) return;
            pendingHealthSpecials += crossed - healthSpecialThresholdsCrossed;
            healthSpecialThresholdsCrossed = crossed;
            healthSpecialReturnRequired = true;
            resetPositionPending = true;
        }
        private void CompleteHealthSpecialReturn()
        {
            RestoreElementAfterReset();
            healthSpecialReturnRequired = false;
            resetPositionPending = false;
            standbyUntil = Time.time + Mathf.Max(0, profile.resetPositionRecovery);
            nextAttack = Mathf.Max(nextAttack, standbyUntil);
        }
        private void QueueResetPosition()
        {
            // Health-special profiles use the HP milestone as the single return trigger.
            if (profile.healthLossSpecialsEnabled) return;
            if (!profile.resetPositionEnabled || profile.resetPositionAnimation == null || Health.IsDead) return;
            float percent = 100f * Health.CurrentHealth / Mathf.Max(1, Health.MaxHealth);
            // One large hit may cross several thresholds: one return consumes them together.
            while (nextResetHealthPercent > .001f && percent <= nextResetHealthPercent)
            {
                resetPositionPending = true;
                nextResetHealthPercent -= Mathf.Clamp(profile.resetPositionHealthInterval, 1f, 100f);
            }
        }
        private bool BeginResetPosition()
        {
            if (!profile.resetPositionEnabled || profile.resetPositionAnimation == null) return false;
            if (!animationDriver.HasState("ResetPosition") || !motor.PrepareResetPosition(HomePosition)) return false;
            resetPositionPending = false;
            CurrentAttack = null; appliedHit = true; pendingContact = false;
            SetState(EnemyState.ResetPosition);
            RestoreElementAfterReset();
            animationDriver.Play("ResetPosition", profile.transitionDuration, true);
            return true;
        }
        private void TickResetPosition()
        {
            if (!animationDriver.TryGetTime("ResetPosition", out float time))
            {
                if (Time.time - stateEntered > 1f)
                {
                    FallBackToWalkingReset();
                }
                return;
            }
            float frames = profile.resetPositionAnimation.length * profile.resetPositionAnimation.frameRate;
            float start = Mathf.Clamp(profile.resetPositionJumpStartFrame, 0, Mathf.Max(0, frames - 1));
            float end = Mathf.Clamp(profile.resetPositionJumpEndFrame, start + 1, Mathf.Max(start + 1, frames));
            float travel = Mathf.InverseLerp(start, end, time * frames);
            motor.TickResetPosition(Mathf.SmoothStep(0, 1, travel), profile.rotationSpeed);
            if (time < 1) return;
            bool reachedHome = HasReachedHome();
            motor.CancelResetPosition();
            if (!reachedHome)
            {
                FallBackToWalkingReset(); return;
            }
            CompleteHealthSpecialReturn();
            if (CanPursueTarget()) SetState(EnemyState.Standby);
            else { Target = null; nextSense = standbyUntil; SetState(EnemyState.Idle); }
        }
        private void FallBackToWalkingReset()
        {
            // Consume the attempted jump, but keep the health special queued.
            // Replaying the jump every second could permanently prevent the special.
            motor.CancelResetPosition();
            resetPositionPending = false;
            BeginReturn();
        }
        private void RestoreElementAfterReset()
        {
            var elemental = GetComponent<ElementalGems.ElementalEnemy>();
            if (elemental != null) elemental.RestoreInnateElementForReset();
        }
        private void ReturnHome()
        {
            if (HasReachedHome())
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
            if (next != EnemyState.Attacking && attackCue != null) attackCue.Hide();
            if (next != EnemyState.Attacking)
            {
                CurrentSequence = null; CurrentSequenceStep = 0; queuedSequenceStep = null; sequenceResumeAt = 0;
                motor.CancelAttackMovement();
            }
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
