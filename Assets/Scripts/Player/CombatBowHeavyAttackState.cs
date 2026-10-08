using UnityEngine;

public sealed class CombatBowHeavyAttackState : StateMachineBehaviour
{
    public BowHeavyAttackConfiguration configuration;
    public bool charged;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var player = animator.GetComponentInParent<PlayerStateManager>();
        if (player == null || !player.NotifyBowHeavyAttackEntered(configuration, charged))
            animator.Play("Base Layer.Attack.Bow", layerIndex, 0f);
    }
}
