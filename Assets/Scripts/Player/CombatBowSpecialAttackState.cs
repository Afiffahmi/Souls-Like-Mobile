using UnityEngine;

public sealed class CombatBowSpecialAttackState : StateMachineBehaviour
{
    public AnimationClip animation;
    public bool highDamage;
    public override void OnStateEnter(Animator animator, AnimatorStateInfo info, int layer)
    {
        var player = animator.GetComponentInParent<PlayerStateManager>();
        if (player == null || !player.NotifyBowSpecialEntered(highDamage))
            animator.Play("Base Layer.Attack.Bow", layer, 0);
    }
    public override void OnStateExit(Animator animator, AnimatorStateInfo info, int layer) =>
        animator.GetComponentInParent<PlayerStateManager>()?.NotifyBowSpecialExited(highDamage);
}
