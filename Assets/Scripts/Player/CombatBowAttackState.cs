using UnityEngine;

public sealed class CombatBowAttackState : StateMachineBehaviour
{
    public AnimationClip characterAnimation;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponentInParent<PlayerStateManager>()?.NotifyBowAttackEntered();
    }
}
