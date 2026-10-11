using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SoulsLike.Riding
{
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController), typeof(HorseAnimationController))]
    public sealed class HorseController : MonoBehaviour
    {
        [Header("Rider attachment — adjust these empty child objects")]
        public Transform saddlePoint;
        public Transform dismountPoint;
        public HorseAnimationController animationController;
        public PlayerHorseEquipment equipment;
        [Header("Ground contact")]
        public LayerMask groundLayers = ~0;
        [Tooltip("Follow nearby ground down slopes and small drops. Larger drops still use gravity.")]
        [Min(0)] public float groundSnapDistance = .8f;
        [Header("Locomotion (joystick steers toward a camera-relative heading)")]
        [Min(.1f)] public float walkSpeed = 3.5f;
        [Min(.1f)] public float runSpeed = 8f;
        [Range(.01f,.5f)] public float inputDeadzone = .1f;
        [Tooltip("Optional joystick movement frame. Empty uses the rider's movement camera, then Camera.main.")]
        public Transform movementReference;
        [Tooltip("Maximum horse turning speed in degrees per second. Turning always uses walking speed.")]
        [Min(1f)] public float turnSpeed = 180f;
        [Tooltip("Heading error in degrees below which steering is complete and straight running is allowed.")]
        [Range(.01f,5f)] public float headingTolerance = .1f;
        [Tooltip("Return to the forward Walk animation when facing within this many degrees of the joystick direction. Small remaining steering corrections do not play a turning animation.")]
        [Range(0f,45f)] public float turnAnimationStopAngle = 10f;
        public float gravity = -24f;
        [Header("Turning animation end frames")]
        [Tooltip("Last frame of the horse's left-turn clip to play before returning to Walk. Frame numbers use the clip's own frame rate. 0 uses the full clip. Facing the joystick direction still ends the animation earlier.")]
        [Min(0)] public int leftTurnEndFrame;
        [Tooltip("Last frame of the horse's right-turn clip to play before returning to Walk. Frame numbers use the clip's own frame rate. 0 uses the full clip. Facing the joystick direction still ends the animation earlier.")]
        [Min(0)] public int rightTurnEndFrame;
        [Header("Health and death timing")]
        [Min(1)] public int maxHealth = 150;
        [Range(0,1)] public float ejectAtDeathNormalized = .2f;
        [Tooltip("Route incoming player health damage to the horse while riding.")]
        public bool absorbRiderDamage = true;
        [Header("Mounted special attack")]
        [Min(0)] public float specialCooldown = 3f;
        [Min(0)] public int specialDamage = 35;
        [Min(0)] public float specialKnockback = 5f;
        [Min(.1f)] public float specialRadius = 1.8f;
        public Vector3 specialCenter = new Vector3(0, 1, 1.5f);
        [Range(0,1)] public float specialHitStart = .25f;
        [Range(0,1)] public float specialHitEnd = .75f;
        public LayerMask specialTargets = ~0;
        public UnityEvent specialStarted = new UnityEvent();
        public UnityEvent died = new UnityEvent();
        public MountedPlayerController Rider { get; private set; }
        public HorseMotion Motion { get; private set; }
        public float StateElapsed { get; private set; }
        public int Health { get; private set; }
        public bool IsDead { get; private set; }
        public float CurrentSpeed { get; private set; }
        public int Level => equipment ? equipment.Level : 1;
        public HorseEquipmentStats EquipmentStats => equipment ? equipment.Capture() : HorseEquipmentStats.Default;
        public float EffectiveWalkSpeed => walkSpeed * EquipmentStats.speed;
        public float EffectiveRunSpeed => runSpeed * EquipmentStats.speed;
        public int EffectiveSpecialDamage => EquipmentStats.Damage(specialDamage);
        public float EffectiveSpecialKnockback => specialKnockback * EquipmentStats.knockback;
        public float LocomotionAnimationRate => Mathf.Clamp(CurrentSpeed / Mathf.Max(.1f,Motion == HorseMotion.Run ? runSpeed : walkSpeed),0,4);
        public bool IsIdle => !IsDead && Motion == HorseMotion.Idle && CurrentSpeed < .02f && motor && motor.isGrounded;
        public HorseAnimationSet Animations => animationController ? animationController.animationSet : null;
        public Vector2 Input { get; private set; }
        public bool Sprint { get; private set; }
        private CharacterController motor;
        private float vertical, cooldownUntil;
        private bool ejected;
        private float attackSpeed = 1, attackMoveSpeed, attackKnockback;
        private int attackDamage;
        private readonly RaycastHit[] groundHits = new RaycastHit[24];
        private HorseMotion? turnAnimationDirection;
        private bool turnAnimationFinished;
        // A perfectly backward stick has no left/right preference. Keep the last
        // deliberate turn side instead of oscillating at the +/-180 degree seam.
        private float lastTurnDirection = 1f;
        private readonly HashSet<Enemy> hit = new HashSet<Enemy>();

        private void Awake()
        {
            motor = GetComponent<CharacterController>();
            if (!animationController) animationController = GetComponent<HorseAnimationController>();
            Health = maxHealth;
        }
        private HorseMotion ResolveMotion(out Vector3 desiredForward, out float turnAngle)
        {
            desiredForward = transform.forward;
            turnAngle = 0;
            if (Input.sqrMagnitude <= inputDeadzone * inputDeadzone) return HorseMotion.Idle;
            Transform reference = movementReference;
            if (!reference && Rider && Rider.player) reference = Rider.player.cameraMain;
            if (!reference && Camera.main) reference = Camera.main.transform;
            PlayerStateManager.GetPlanarBasis(reference, out Vector3 forward, out Vector3 right);
            desiredForward = (forward * Input.y + right * Input.x).normalized;
            Vector3 facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            turnAngle = Vector3.SignedAngle(facing, desiredForward, Vector3.up);
            float side = Vector3.Dot(Vector3.Cross(Vector3.up, facing), desiredForward);
            // For a backward diagonal the side with more stick deflection decides
            // the turn. Only a nearly exact reversal needs the remembered side.
            if (Vector3.Dot(facing, desiredForward) < -.9998f && Mathf.Abs(side) < .015f)
                turnAngle = Mathf.Abs(turnAngle) * lastTurnDirection;
            if (Mathf.Abs(turnAngle) > headingTolerance)
                return turnAngle < 0 ? HorseMotion.WalkLeft : HorseMotion.WalkRight;
            return Sprint ? HorseMotion.Run : HorseMotion.Walk;
        }
        private Vector3 SteerAndMove(float dt)
        {
            HorseMotion motion = ResolveMotion(out Vector3 desiredForward, out float angle);
            if (motion == HorseMotion.Idle) { SetMotion(motion); return Vector3.zero; }
            if (motion == HorseMotion.WalkLeft || motion == HorseMotion.WalkRight)
            {
                lastTurnDirection = Mathf.Sign(angle);
                float step = Mathf.Clamp(angle, -Mathf.Max(1f, turnSpeed) * dt, Mathf.Max(1f, turnSpeed) * dt);
                transform.rotation = Quaternion.AngleAxis(step, Vector3.up) * transform.rotation;
            }
            else if (dt > 0)
            {
                transform.rotation = Quaternion.LookRotation(desiredForward, Vector3.up);
            }
            // Choose the animation AFTER rotating this frame so a completed turn
            // returns to forward locomotion immediately. Keep minor corrections in
            // Walk, with a small re-entry margin to avoid flickering near the limit.
            motion = ResolveMotion(out _, out float remainingAngle);
            if (motion == HorseMotion.WalkLeft || motion == HorseMotion.WalkRight)
            {
                bool wasTurning = Motion == HorseMotion.WalkLeft || Motion == HorseMotion.WalkRight;
                float animationThreshold = Mathf.Max(headingTolerance, turnAnimationStopAngle) + (wasTurning ? 0f : 3f);
                if (Mathf.Abs(remainingAngle) <= animationThreshold) motion = HorseMotion.Walk;
            }
            motion = ApplyTurnEndFrame(motion);
            SetMotion(motion);
            // Always travel along the horse's nose, including a walking U-turn.
            // The stick magnitude controls speed, never sideways or backward sliding.
            return transform.forward * Input.magnitude * (motion == HorseMotion.Run ? EffectiveRunSpeed : EffectiveWalkSpeed);
        }
        public void SetInput(Vector2 input, bool sprint) { Input = Vector2.ClampMagnitude(input, 1); Sprint = sprint; }
        private HorseMotion ApplyTurnEndFrame(HorseMotion requested)
        {
            if (requested != HorseMotion.WalkLeft && requested != HorseMotion.WalkRight)
            {
                turnAnimationDirection = null;
                turnAnimationFinished = false;
                return requested;
            }
            if (turnAnimationDirection != requested)
            {
                turnAnimationDirection = requested;
                turnAnimationFinished = false;
            }
            var entry = Animations ? Animations.Get(requested) : null;
            var clip = entry != null ? (entry.horseClip ? entry.horseClip : entry.horseControllerClip) : null;
            if (!turnAnimationFinished && Motion == requested && entry != null && clip)
            {
                int frame = requested == HorseMotion.WalkLeft ? leftTurnEndFrame : rightTurnEndFrame;
                float totalFrames = Mathf.Max(1f, clip.length * clip.frameRate);
                float endPhase = frame <= 0 ? 1f : Mathf.Clamp01(frame / totalFrames);
                // RideTime uses the paired animation duration, so convert the horse
                // clip frame into that same normalized timeline rather than seconds.
                if (StateElapsed >= entry.Seconds * endPhase) turnAnimationFinished = true;
            }
            // Keep steering physically, but do not restart the finished animation
            // until this turn ends or the joystick requests the opposite turn.
            return turnAnimationFinished ? HorseMotion.Walk : requested;
        }
        public bool TryReserve(MountedPlayerController rider)
        {
            if (IsDead || Rider || !saddlePoint || !Animations || !isActiveAndEnabled) return false;
            if (!equipment) equipment = rider.GetComponent<PlayerHorseEquipment>();
            Rider = rider; SetInput(Vector2.zero, false); return true;
        }
        public void Release(MountedPlayerController rider)
        {
            if (Rider != rider) return;
            Rider = null; SetInput(Vector2.zero, false); CurrentSpeed = 0;
            if (!IsDead) SetMotion(HorseMotion.Idle);
        }
        private void SetMotion(HorseMotion value)
        {
            if (value != HorseMotion.Walk && value != HorseMotion.WalkLeft && value != HorseMotion.WalkRight)
            {
                turnAnimationDirection = null;
                turnAnimationFinished = false;
            }
            if (Motion == value) return;
            Motion = value; StateElapsed = 0;
        }
        private void Update()
        {
            float dt = Time.deltaTime;
            // The shared clock drives both rider and horse. Attack stats are
            // captured at release; equipment swaps cannot alter an ongoing hit.
            bool moving = Motion >= HorseMotion.Walk && Motion <= HorseMotion.Run;
            StateElapsed += dt * (Motion == HorseMotion.SpecialAttack ? attackSpeed : moving ? LocomotionAnimationRate : 1f);
            Vector3 travel = Vector3.zero;
            if (IsDead)
            {
                if (Motion == HorseMotion.Death1)
                {
                    float normalized = StateElapsed / Animations.Duration(HorseMotion.Death1);
                    if (!ejected && normalized >= ejectAtDeathNormalized) { ejected = true; if (Rider) Rider.BeginEjection(); }
                    if (normalized >= 1) SetMotion(HorseMotion.Death2);
                }
            }
            else if (Rider && Rider.IsRiding)
            {
                if (Motion == HorseMotion.SpecialAttack)
                {
                    float n = StateElapsed / Animations.Duration(Motion);
                    if (n >= specialHitStart && n <= specialHitEnd) ApplySpecialDamage();
                    if (n >= 1) SetMotion(HorseMotion.Idle);
                    else travel = transform.forward * attackMoveSpeed;
                }
                if (Motion != HorseMotion.SpecialAttack)
                {
                    travel = SteerAndMove(dt);
                }
            }
            else if (!IsDead) SetMotion(HorseMotion.Idle);
            if (motor.enabled)
            {
                if (motor.isGrounded && vertical < 0) vertical = -2;
                vertical = Mathf.Max(-50, vertical + gravity * dt);
                Vector3 before = transform.position;
                motor.Move((travel + Vector3.up * vertical) * dt);
                SnapToGround();
                CurrentSpeed = dt > 0 ? Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude / dt : 0;
            }
            animationController.Sample(Motion, StateElapsed);
            if (Rider) Rider.SyncAnimation();
        }
        public bool TrySpecialAttack()
        {
            if (Time.timeScale <= 0 || IsDead || !Rider || !Rider.IsRiding || Motion != HorseMotion.Run ||
                ResolveMotion(out _, out _) != HorseMotion.Run || Time.time < cooldownUntil) return false;
            var stats = EquipmentStats;
            attackSpeed = stats.speed; attackMoveSpeed = runSpeed * stats.speed;
            attackDamage = stats.Damage(specialDamage); attackKnockback = specialKnockback * stats.knockback;
            cooldownUntil = Time.time + specialCooldown; hit.Clear(); SetMotion(HorseMotion.SpecialAttack); specialStarted.Invoke(); return true;
        }
        private void ApplySpecialDamage()
        {
            foreach (var c in Physics.OverlapSphere(transform.TransformPoint(specialCenter), specialRadius, specialTargets, QueryTriggerInteraction.Ignore))
            {
                var enemy = c.GetComponentInParent<Enemy>();
                if (!enemy || enemy.IsDead || enemy.IsInvulnerable || !hit.Add(enemy)) continue;
                Vector3 away = Vector3.ProjectOnPlane(enemy.transform.position-transform.position,Vector3.up);
                if (away.sqrMagnitude < .001f) away = transform.forward;
                var receiver = ElementalGems.ElementalEnemy.GetOrAdd(enemy);
                bool pushes = receiver && receiver.CanKnockback(away,attackKnockback);
                int before = enemy.CurrentHealth;
                enemy.TakeDamage(attackDamage,pushes);
                if (pushes && !enemy.IsDead && enemy.CurrentHealth < before) receiver.ApplyKnockback(away,attackKnockback);
            }
        }
        private void SnapToGround()
        {
            if (groundSnapDistance <= 0 || vertical > 0) return;
            Vector3 scale = transform.lossyScale;
            float radius = motor.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
            float height = Mathf.Max(motor.height*Mathf.Abs(scale.y),radius*2);
            Vector3 foot = transform.TransformPoint(motor.center)-Vector3.up*(height*.5f);
            int count = Physics.RaycastNonAlloc(foot+Vector3.up*.2f,Vector3.down,groundHits,groundSnapDistance+.2f,groundLayers,QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i=0;i<count;i++)
            {
                var ground = groundHits[i];
                if (ground.transform.IsChildOf(transform) || (Rider && ground.transform.IsChildOf(Rider.transform)) ||
                    Vector3.Angle(ground.normal,Vector3.up)>motor.slopeLimit) continue;
                float gap = foot.y-ground.point.y;
                if (gap >= -.05f && gap < nearest) nearest = gap;
            }
            if (nearest <= groundSnapDistance)
            {
                if (nearest > .005f) motor.Move(Vector3.down*(nearest+.02f));
                if (motor.isGrounded) vertical = -2;
            }
        }
        public void TakeDamage(int damage)
        {
            if (IsDead) return;
            Health = Mathf.Max(0, Health - Mathf.Max(0, damage));
            if (Health == 0) Die();
        }
        [ContextMenu("Kill Horse (Play Mode)")]
        public void Die()
        {
            if (!Application.isPlaying || IsDead) return;
            IsDead = true; Health = 0; CurrentSpeed = 0; SetInput(Vector2.zero, false);
            SetMotion(HorseMotion.Death1); animationController.Sample(Motion, 0); died.Invoke();
        }
        private void OnDisable() { if (Rider) Rider.AbortRide(); }
        private void OnDrawGizmosSelected()
        {
            if (saddlePoint) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(saddlePoint.position, .15f); Gizmos.DrawRay(saddlePoint.position, saddlePoint.forward); }
            if (dismountPoint) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(dismountPoint.position, .3f); }
        }
    }
}
