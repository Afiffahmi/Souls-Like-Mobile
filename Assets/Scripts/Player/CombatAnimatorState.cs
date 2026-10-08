using UnityEngine;

// Each locomotion state remembers its mode. One shared parry state locks that
// remembered value until ParryFinished and returns through its matching transition.
public sealed class CombatAnimatorState : StateMachineBehaviour
{
    public PlayerCombatMode mode;
    public bool isParry;
    [Tooltip("Clip used by the parry state, for frame-accurate hold timing.")]
    public AnimationClip parryAnimation;
    private int returnMode;
    private static readonly int ModeHash = Animator.StringToHash("CombatMode");
    private static readonly int ReturnHash = Animator.StringToHash("ParryReturnMode");
    private static readonly int ParryHash = Animator.StringToHash("Parry");

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var player = animator.GetComponentInParent<PlayerStateManager>();
        if (!isParry)
        {
            if (mode != PlayerCombatMode.Normal) animator.SetInteger(ReturnHash, (int)mode);
            player?.NotifyAnimatorStateEntered(mode, false);
            return;
        }
        returnMode = Mathf.Clamp(animator.GetInteger(ReturnHash), 1, 3);
        LockParry(animator);
        player?.NotifyAnimatorStateEntered((PlayerCombatMode)returnMode, true);
        player?.NotifyParryStarted((PlayerCombatMode)returnMode);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (isParry) LockParry(animator);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!isParry) return;
        LockParry(animator);
        animator.GetComponentInParent<PlayerStateManager>()?.NotifyParryCompleted();
    }

    private void LockParry(Animator animator)
    {
        animator.SetInteger(ModeHash, returnMode);
        animator.SetInteger(ReturnHash, returnMode);
        animator.ResetTrigger(ParryHash);
    }
}
