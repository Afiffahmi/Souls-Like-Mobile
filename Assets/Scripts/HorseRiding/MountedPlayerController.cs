using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SoulsLike.Riding
{
    public enum RiderPhase { OnFoot, Mounting, Riding, Dismounting, Ejected }

    [DefaultExecutionOrder(-30), DisallowMultipleComponent, RequireComponent(typeof(PlayerStateManager))]
    public sealed class MountedPlayerController : MonoBehaviour
    {
        [Header("Existing player references")]
        public PlayerStateManager player;
        public Animator playerAnimator;
        public PlayerInput input;
        [Header("Input action names (existing action map)")]
        public string moveAction = "Move", sprintAction = "Sprint", specialAction = "SpecialAttack";
        [Header("Mount / dismount timing")]
        [Range(.05f,1)] public float attachAtNormalized = .65f;
        [Range(0,.95f)] public float detachAtNormalized = .25f;
        [Header("Safe placement")]
        public LayerMask groundAndObstacles = ~0;
        [Min(.5f)] public float groundProbe = 5f;
        [Header("Ejection")]
        [Range(0,.9f)] public float ejectDetachAtNormalized = .15f;
        [Min(0)] public float ejectUpSpeed = 5f;
        [Min(0)] public float ejectSideSpeed = 3f;
        public RiderPhase Phase { get; private set; }
        public HorseController Horse { get; private set; }
        public bool IsBusy => Phase != RiderPhase.OnFoot;
        public bool IsRiding => Phase == RiderPhase.Riding;
        public bool CanMount => !mountRequested && CanStartMount;
        private bool CanStartMount => !IsBusy && isActiveAndEnabled && player && player.isActiveAndEnabled &&
            !player.IsAttacking && !player.IsParrying && !player.IsRolling && !player.IsChangingEquipment &&
            (!TryGetComponent<PlayerOverall>(out var hp) || hp.currentHealth > 0) &&
            (!TryGetComponent<ElementalGems.PlayerElementalState>(out var status) || !status.CannotAct);
        private CharacterController body;
        private PlayerLockOn lockOn;
        private bool wasPlayerEnabled, wasLockEnabled, wasBodyEnabled, wasRootMotion;
        private RuntimeAnimatorController originalController;
        private AnimatorOverrideController mountedController;
        private AnimatorUpdateMode originalUpdate;
        private AnimatorCullingMode originalCulling;
        private PlayerCombatMode originalMode;
        private Transform originalParent;
        private Vector3 originalScale, startPosition, targetPosition, ejectVelocity;
        private Quaternion startRotation;
        private float elapsed;
        private float mountDismountSpeed = 1f;
        private bool mountRequested;
        private HorseController pendingHorse;
        private float pendingMountRange;
        private HorseMotion? lastAnimation;
        private InputAction move, sprint, special;
        private readonly Dictionary<Collider,bool> colliders = new Dictionary<Collider,bool>();
        private readonly List<Collider> ignoredHorseColliders = new List<Collider>();
        private HorseAnimationSet set;
        private bool detached;

        private void Awake()
        {
            if (!player) player = GetComponent<PlayerStateManager>();
            if (!playerAnimator) playerAnimator = player.anim;
            if (!input) input = GetComponent<PlayerInput>();
            body = GetComponent<CharacterController>(); lockOn = GetComponent<PlayerLockOn>();
        }
        private void Start()
        {
            if (!input || !input.actions) return;
            move = input.actions.FindAction(moveAction, false); sprint = input.actions.FindAction(sprintAction, false);
            special = input.actions.FindAction(specialAction, false);
        }
        public bool TryMount(HorseController horse, float range)
        {
            if (Time.timeScale <= 0 || !CanMount || !horse || !horse.dismountPoint || !playerAnimator || !horse.Animations ||
                !horse.Animations.playerController || Vector3.Distance(transform.position, horse.transform.position) > range || !horse.TryReserve(this)) return false;
            // Use the same request as Select Normal, keeping the on-foot Animator
            // active until its weapon unequip animation has actually completed.
            if (!player.TrySetCombatMode(PlayerCombatMode.Normal)) { horse.Release(this); return false; }
            if (player.IsChangingEquipment)
            {
                pendingHorse = horse; pendingMountRange = range; mountRequested = true;
                return true;
            }
            BeginMount(horse);
            return true;
        }
        private void UpdateMountRequest()
        {
            if (!pendingHorse || !pendingHorse.isActiveAndEnabled || pendingHorse.IsDead || pendingHorse.Rider != this ||
                !pendingHorse.saddlePoint || !pendingHorse.dismountPoint || !pendingHorse.Animations || !pendingHorse.Animations.playerController ||
                !player || !player.isActiveAndEnabled || !playerAnimator || !playerAnimator.isActiveAndEnabled ||
                Vector3.Distance(transform.position,pendingHorse.transform.position) > pendingMountRange ||
                (TryGetComponent<PlayerOverall>(out var hp) && hp.currentHealth <= 0) ||
                (TryGetComponent<ElementalGems.PlayerElementalState>(out var status) && status.CannotAct))
            { CancelMountRequest(); return; }
            if (Time.timeScale <= 0 || player.IsChangingEquipment) return;
            if (!CanStartMount || player.CombatMode != PlayerCombatMode.Normal)
            { CancelMountRequest(); return; }
            var horse = pendingHorse;
            pendingHorse = null; mountRequested = false;
            BeginMount(horse);
        }
        private void CancelMountRequest()
        {
            if (pendingHorse) pendingHorse.Release(this);
            pendingHorse = null; mountRequested = false;
        }
        private void BeginMount(HorseController horse)
        {
            Horse = horse; set = horse.Animations; originalMode = player.CombatMode;
            originalParent = transform.parent; originalScale = transform.localScale;
            wasPlayerEnabled = player.enabled; wasLockEnabled = lockOn && lockOn.enabled;
            wasBodyEnabled = body && body.enabled;
            if (lockOn) lockOn.enabled = false;
            player.enabled = false; // Existing OnDisable cancels combat, charge, roll and residual velocity.
            originalController = playerAnimator.runtimeAnimatorController;
            wasRootMotion = playerAnimator.applyRootMotion;
            originalUpdate = playerAnimator.updateMode; originalCulling = playerAnimator.cullingMode;
            colliders.Clear();
            foreach (var c in GetComponentsInChildren<Collider>(true)) { colliders[c] = c.enabled; c.enabled = false; }
            // Stage the rider at the authored side-of-horse marker before the
            // riding Animator starts. Colliders are disabled for this teleport.
            startPosition = horse.dismountPoint.position;
            Vector3 towardHorse = Vector3.ProjectOnPlane(horse.transform.position - startPosition, Vector3.up);
            if (towardHorse.sqrMagnitude < .0001f)
                towardHorse = Vector3.ProjectOnPlane(horse.transform.forward, Vector3.up);
            startRotation = towardHorse.sqrMagnitude > .0001f
                ? Quaternion.LookRotation(towardHorse, Vector3.up) : transform.rotation;
            transform.SetPositionAndRotation(startPosition, startRotation);
            mountedController = set.CreateController(true);
            playerAnimator.runtimeAnimatorController = mountedController;
            playerAnimator.SetInteger("CombatMode", (int)originalMode);
            playerAnimator.SetInteger("ParryReturnMode", (int)originalMode);
            playerAnimator.applyRootMotion = false; playerAnimator.updateMode = AnimatorUpdateMode.Normal;
            playerAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Phase = RiderPhase.Mounting; elapsed = 0; detached = false; lastAnimation = null;
            mountDismountSpeed = horse.EquipmentStats.speed;
        }
        private void Update()
        {
            if (mountRequested) { UpdateMountRequest(); return; }
            if (!IsBusy) return;
            if (!Horse) { AbortRide(); return; }
            // The sampled clip and attachment milestones share one scaled clock.
            // Capture at action start so upgrading mid-action cannot change its timing.
            elapsed += Time.deltaTime * (Phase == RiderPhase.Mounting || Phase == RiderPhase.Dismounting ? mountDismountSpeed : 1f);
            if (Phase == RiderPhase.Mounting)
            {
                if (Horse.IsDead) return; // Horse death owns the ejection timing, including a mount in progress.
                float n = elapsed / set.Duration(HorseMotion.Mount);
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(n / attachAtNormalized));
                transform.SetPositionAndRotation(Vector3.Lerp(startPosition, Horse.saddlePoint.position,t), Quaternion.Slerp(startRotation,Horse.saddlePoint.rotation,t));
                if (n >= attachAtNormalized) Attach();
                if (n >= 1) { Phase = RiderPhase.Riding; elapsed = 0; }
            }
            else if (Phase == RiderPhase.Riding)
            {
                bool active = input && input.isActiveAndEnabled && input.inputIsActive;
                Horse.SetInput(active && move != null && move.enabled ? move.ReadValue<Vector2>() : Vector2.zero,
                    active && sprint != null && sprint.enabled && sprint.IsPressed());
                if (active && special != null && special.enabled && special.WasPressedThisFrame()) TrySpecialAttack();
            }
            else if (Phase == RiderPhase.Dismounting)
            {
                if (Horse.IsDead) return;
                float n = elapsed / set.Duration(HorseMotion.Dismount);
                if (n >= detachAtNormalized)
                {
                    if (!detached) { Detach(); startPosition = transform.position; }
                    transform.position = Vector3.Lerp(startPosition, targetPosition, Mathf.SmoothStep(0,1,Mathf.InverseLerp(detachAtNormalized,1,n)));
                }
                if (n >= 1)
                {
                    // Recheck because an obstacle may have entered the destination during the animation.
                    if (TryLanding(targetPosition, out var safe)) { transform.position = safe; FinishRide(); }
                    else { Attach(); detached = false; Phase = RiderPhase.Riding; elapsed = 0; }
                }
            }
            else if (Phase == RiderPhase.Ejected)
            {
                float n = elapsed / set.Duration(HorseMotion.Ejected);
                if (n >= ejectDetachAtNormalized && !detached)
                {
                    Detach();
                    if (body) body.enabled = true;
                    foreach(var c in Horse.GetComponentsInChildren<Collider>())
                        if (body && c != body) { Physics.IgnoreCollision(body,c,true); ignoredHorseColliders.Add(c); }
                    ejectVelocity = -Horse.transform.right * ejectSideSpeed + Vector3.up * ejectUpSpeed;
                }
                if (detached && body && body.enabled)
                {
                    ejectVelocity.y = Mathf.Max(-50,ejectVelocity.y - 24 * Time.deltaTime);
                    body.Move(ejectVelocity * Time.deltaTime);
                    if (body.isGrounded && ejectVelocity.y < 0)
                    {
                        ejectVelocity = Vector3.down * 2;
                        if (n >= 1 && TryLanding(transform.position,out var safe)) { transform.position = safe; FinishRide(); }
                    }
                }
            }
        }
        public void SyncAnimation()
        {
            if (!Horse || !IsBusy) return;
            HorseMotion motion = Phase == RiderPhase.Mounting ? HorseMotion.Mount : Phase == RiderPhase.Dismounting ? HorseMotion.Dismount : Phase == RiderPhase.Ejected ? HorseMotion.Ejected : Horse.Motion;
            // Before ejection, keep the rider seated as Death1 begins.
            if (motion == HorseMotion.Death1 || motion == HorseMotion.Death2) motion = HorseMotion.Idle;
            float time = Phase == RiderPhase.Riding ? Horse.StateElapsed : elapsed;
            if (Phase == RiderPhase.Riding && motion == HorseMotion.Walk && Horse.animationController)
                time = Horse.animationController.WalkPhase * set.Duration(HorseMotion.Walk);
            Sample(motion,time);
        }
        private void LateUpdate()
        {
            if (Horse && transform.parent == Horse.saddlePoint) { transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; }
        }
        private void Sample(HorseMotion motion, float time)
        {
            var entry = set.Get(motion);
            if (entry == null || string.IsNullOrEmpty(entry.playerState)) return;
            if (lastAnimation != motion) { playerAnimator.CrossFadeInFixedTime(entry.playerState, set.transitionSeconds,0,0); lastAnimation = motion; }
            playerAnimator.SetFloat(set.timeParameter, HorseAnimationSet.Loops(motion) ? time / entry.Seconds % 1 : Mathf.Clamp01(time / entry.Seconds));
        }
        private void Attach()
        {
            transform.SetParent(Horse.saddlePoint, true); transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity;
        }
        private void Detach() { transform.SetParent(originalParent, true); transform.localScale = originalScale; detached = true; }
        public bool TrySpecialAttack() => IsRiding && Horse && Horse.TrySpecialAttack();
        public bool TryDismount()
        {
            if (Time.timeScale <= 0 || !IsRiding || !Horse || !Horse.IsIdle || !FindDismount(out targetPosition)) return false;
            mountDismountSpeed = Horse.EquipmentStats.speed;
            Horse.SetInput(Vector2.zero,false); Phase = RiderPhase.Dismounting; elapsed = 0; detached = false; return true;
        }
        private bool FindDismount(out Vector3 point)
        {
            if (Horse.dismountPoint && TryLanding(Horse.dismountPoint.position,out point)) return true;
            foreach(var direction in new [] {-Horse.transform.right,Horse.transform.right,-Horse.transform.forward,Horse.transform.forward})
                if (TryLanding(Horse.transform.position + direction * 2,out point)) return true;
            point = default; return false;
        }
        private bool IsOwn(Collider c) => c.transform.IsChildOf(transform) || (Horse && c.transform.IsChildOf(Horse.transform));
        private bool TryLanding(Vector3 near, out Vector3 point)
        {
            point = default;
            var hits = Physics.RaycastAll(near + Vector3.up * groundProbe,Vector3.down,groundProbe * 2,groundAndObstacles,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var h in hits)
            {
                if (IsOwn(h.collider) || Vector3.Angle(h.normal,Vector3.up) > (body ? body.slopeLimit : 45)) continue;
                float radius = body ? body.radius * Mathf.Max(transform.lossyScale.x,transform.lossyScale.z) : .35f;
                float height = body ? Mathf.Max(radius * 2,body.height * transform.lossyScale.y) : 1.8f;
                Vector3 p = h.point + Vector3.up * (height * .5f - (body ? body.center.y * transform.lossyScale.y : height * .5f) + .08f);
                Vector3 center = p + (body ? Vector3.Scale(body.center, transform.lossyScale) : Vector3.up * height * .5f);
                bool blocked = false;
                foreach(var c in Physics.OverlapCapsule(center+Vector3.up*(height*.5f-radius),center-Vector3.up*(height*.5f-radius),radius*.95f,groundAndObstacles,QueryTriggerInteraction.Ignore))
                    if (!IsOwn(c)) { blocked = true; break; }
                if (!blocked) { point = p; return true; }
            }
            return false;
        }
        public void BeginEjection()
        {
            if (!IsBusy || Phase == RiderPhase.Ejected) return;
            Phase = RiderPhase.Ejected; elapsed = 0; detached = false;
        }
        public bool RedirectDamage(int damage)
        {
            if (!Horse || !Horse.absorbRiderDamage || (Phase != RiderPhase.Riding && Phase != RiderPhase.Mounting)) return false;
            Horse.TakeDamage(damage); return true;
        }
        private void FinishRide()
        {
            if (!IsBusy) return;
            Detach();
            foreach(var c in ignoredHorseColliders) if(c && body) Physics.IgnoreCollision(body,c,false);
            ignoredHorseColliders.Clear();
            foreach(var pair in colliders) if(pair.Key) pair.Key.enabled = pair.Value;
            if (body) body.enabled = wasBodyEnabled;
            if (playerAnimator)
            {
                playerAnimator.runtimeAnimatorController = originalController;
                playerAnimator.applyRootMotion = wasRootMotion; playerAnimator.updateMode = originalUpdate; playerAnimator.cullingMode = originalCulling;
                playerAnimator.Rebind(); playerAnimator.Update(0);
                playerAnimator.SetInteger("CombatMode",(int)originalMode); playerAnimator.SetInteger("ParryReturnMode",(int)originalMode);
                string path = RollConfiguration.LocomotionPath(originalMode);
                if (playerAnimator.HasState(0,Animator.StringToHash(path))) playerAnimator.Play(path,0,0);
            }
            if (mountedController) Destroy(mountedController);
            var previous = Horse; Horse = null; Phase = RiderPhase.OnFoot; lastAnimation = null;
            if (previous) previous.Release(this);
            if (player) player.enabled = wasPlayerEnabled;
            if (lockOn) lockOn.enabled = wasLockEnabled;
        }
        public void AbortRide()
        {
            CancelMountRequest();
            if (!IsBusy) return;
            if (Horse && FindDismount(out var safe)) transform.position = safe;
            FinishRide();
        }
        private void OnDisable() => AbortRide();
    }
}
