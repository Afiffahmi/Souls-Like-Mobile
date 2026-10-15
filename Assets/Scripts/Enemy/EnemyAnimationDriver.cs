using UnityEngine;

namespace SoulsLike.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyAnimationDriver : MonoBehaviour
    {
        public Animator animator;
        [Tooltip("Use the Run state while moving. Leave off for enemies with separate walking locomotion.")]
        public bool runForLocomotion;
        private int requested;
        public const string AttackPhaseSpeedParameter = "EnemyAttackPhaseSpeed";
        private static readonly int AttackPhaseSpeedHash = Animator.StringToHash(AttackPhaseSpeedParameter);
        private bool hasAttackPhaseSpeed;
        public bool Ready => animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;
        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) { animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            if (animator != null)
                foreach (var parameter in animator.parameters)
                    if (parameter.nameHash == AttackPhaseSpeedHash && parameter.type == AnimatorControllerParameterType.Float)
                        hasAttackPhaseSpeed = true;
        }
        public bool HasState(string state) => Ready && animator.HasState(0, Animator.StringToHash("Base Layer." + state));
        public bool Play(string state, float blend, bool restart = false)
        {
            int hash = Animator.StringToHash("Base Layer." + state);
            if (!Ready || !animator.HasState(0, hash)) return false;
            ResetAttackSpeed();
            bool currentPlaying = animator.GetCurrentAnimatorStateInfo(0).fullPathHash == hash;
            bool playing = currentPlaying;
            if (animator.IsInTransition(0)) playing = animator.GetNextAnimatorStateInfo(0).fullPathHash == hash;
            // Re-entering the same state during its outgoing idle blend can retain
            // the finished state's time. Explicitly restart so the next attack cannot skip.
            if (restart && currentPlaying) animator.Play(hash, 0, 0);
            else if (restart || requested != hash || !playing) animator.CrossFadeInFixedTime(hash, blend, 0, 0);
            requested = hash;
            return true;
        }
        public bool PlayAttack(EnemyAttackDefinition attack, float blend)
        {
            bool played = Play(attack.stateName, blend, true);
            if (played)
            {
                // Flush the restart before the brain can sample an outgoing copy
                // of this same attack at normalized time >= 1.
                animator.Update(0);
                UpdateAttackSpeed(attack, 0);
            }
            return played;
        }
        public void UpdateAttackSpeed(EnemyAttackDefinition attack, float normalizedTime)
        {
            // State multiplier leaves Animator.speed owned by stun/pause systems.
            if (Ready && hasAttackPhaseSpeed) animator.SetFloat(AttackPhaseSpeedHash, attack.PlaybackMultiplierAt(normalizedTime));
        }
        public void ResetAttackSpeed()
        {
            if (animator != null && hasAttackPhaseSpeed) animator.SetFloat(AttackPhaseSpeedHash, 1f);
        }
        public bool TryGetTime(string state, out float time)
        {
            time = 0;
            if (!Ready) return false;
            int hash = Animator.StringToHash("Base Layer." + state);
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.fullPathHash == hash) { time = next.normalizedTime; return true; }
            }
            if (info.fullPathHash != hash) return false;
            time = info.normalizedTime; return true;
        }
        public void Locomotion(bool moving, float blend) => Play(moving ? (runForLocomotion && HasState("Run") ? "Run" : "Walk") : "Idle", blend);
        private void OnDisable() { requested = 0; ResetAttackSpeed(); }
    }
}
