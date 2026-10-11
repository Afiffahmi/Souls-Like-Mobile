using UnityEngine;

namespace SoulsLike.Riding
{
    [DisallowMultipleComponent]
    public sealed class HorseAnimationController : MonoBehaviour
    {
        public const string WalkingLayerName = "Walking Legs";
        public const string WalkTimeParameter = "WalkCycleTime";
        public Animator animator;
        public HorseAnimationSet animationSet;
        private HorseMotion? last;
        private AnimatorOverrideController runtime;
        private int walkingLayer = -1;
        private float walkPhase, walkingWeight;
        private HorseController horse;
        public float WalkPhase => walkPhase;
        private void Awake()
        {
            horse = GetComponent<HorseController>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (animator && animationSet)
            {
                runtime = animationSet.CreateController(false);
                animator.runtimeAnimatorController = runtime;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                walkingLayer = animator.GetLayerIndex(WalkingLayerName);
            }
        }
        private static bool IsWalking(HorseMotion motion) => motion == HorseMotion.Walk || motion == HorseMotion.WalkLeft || motion == HorseMotion.WalkRight;

        public void Sample(HorseMotion motion, float elapsed)
        {
            if (!animator || !animationSet) return;
            var entry = animationSet.Get(motion);
            if (entry == null || string.IsNullOrEmpty(entry.horseState)) return;
            bool walking = IsWalking(motion);
            if (walkingLayer >= 0)
            {
                // Keep the same footstep phase across forward/left/right changes.
                // Turn clips can be much longer than a single walking stride.
                if (walking)
                {
                    if (!last.HasValue || !IsWalking(last.Value)) walkPhase = 0;
                    else walkPhase = Mathf.Repeat(walkPhase + Time.deltaTime * (horse ? horse.LocomotionAnimationRate : 1f) / animationSet.Duration(HorseMotion.Walk), 1f);
                }
                float target = walking ? 1f : 0f;
                walkingWeight = motion == HorseMotion.Death1 || motion == HorseMotion.Death2 || animationSet.transitionSeconds <= 0
                    ? target : Mathf.MoveTowards(walkingWeight, target, Time.deltaTime / animationSet.transitionSeconds);
                animator.SetFloat(WalkTimeParameter, walkPhase);
                animator.SetLayerWeight(walkingLayer, walkingWeight);
            }
            if (last != motion)
            {
                animator.CrossFadeInFixedTime(entry.horseState, motion == HorseMotion.Death1 ? 0 : animationSet.transitionSeconds, 0, 0);
                last = motion;
            }
            float phase = HorseAnimationSet.Loops(motion) ? elapsed / entry.Seconds % 1f : Mathf.Clamp01(elapsed / entry.Seconds);
            // The straight-walk body and legs must use the same cycle.
            if (walkingLayer >= 0 && motion == HorseMotion.Walk) phase = walkPhase;
            animator.SetFloat(animationSet.timeParameter, phase);
        }
        private void OnDestroy() { if (runtime) Destroy(runtime); }
    }
}
