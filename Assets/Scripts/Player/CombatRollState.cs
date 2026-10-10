using UnityEngine;

public sealed class CombatRollState : StateMachineBehaviour
{
    public RollConfiguration configuration;
    public PlayerCombatMode mode;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var player = animator.GetComponentInParent<PlayerStateManager>();
        if (player == null || !player.NotifyRollEntered(configuration?.ForMode(mode)))
            animator.Play(RollConfiguration.LocomotionPath(mode), layerIndex, 0f);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => animator.GetComponentInParent<PlayerStateManager>()?.MaintainRollLock();
}
