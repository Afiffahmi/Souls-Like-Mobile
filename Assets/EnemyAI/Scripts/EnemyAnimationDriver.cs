using UnityEngine;

namespace SoulsLike.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyAnimationDriver : MonoBehaviour
    {
        public Animator animator;
        private int requested;
        public bool Ready => animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;
        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) { animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        }
        public bool HasState(string state) => Ready && animator.HasState(0, Animator.StringToHash("Base Layer." + state));
        public bool Play(string state, float blend, bool restart = false)
        {
            int hash = Animator.StringToHash("Base Layer." + state);
            if (!Ready || !animator.HasState(0, hash)) return false;
            bool playing = animator.GetCurrentAnimatorStateInfo(0).fullPathHash == hash;
            if (animator.IsInTransition(0)) playing = animator.GetNextAnimatorStateInfo(0).fullPathHash == hash;
            if (restart || requested != hash || !playing) animator.CrossFadeInFixedTime(hash, blend, 0, 0);
            requested = hash;
            return true;
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
        public void Locomotion(bool moving, float blend) => Play(moving ? "Walk" : "Idle", blend);
        private void OnDisable() => requested = 0;
    }
}
