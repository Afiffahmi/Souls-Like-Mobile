using UnityEngine;

public sealed class CombatAttackState : StateMachineBehaviour
{
    public CombatAttackConfiguration configuration;
    public CombatAttackInput input;
    public int stepIndex;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var player = animator.GetComponentInParent<PlayerStateManager>();
        if (player != null && !player.NotifyAttackEntered(configuration, input, stepIndex) && configuration != null)
            animator.Play(configuration.LocomotionStatePath, layerIndex, 0f);
        LockWeapon(animator);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) => LockWeapon(animator);

    private void LockWeapon(Animator animator)
    {
        if (configuration == null) return;
        animator.SetInteger("CombatMode", (int)configuration.weapon);
        animator.ResetTrigger("Parry");
        animator.ResetTrigger("LightAttack");
        animator.ResetTrigger("HeavyAttack");
        animator.ResetTrigger("SpecialAttack");
    }
}
