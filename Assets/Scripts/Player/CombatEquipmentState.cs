using UnityEngine;

public sealed class CombatEquipmentState : StateMachineBehaviour
{
    public PlayerCombatMode weapon;
    public bool equipping;
    private int destination;
    private static readonly int ModeHash = Animator.StringToHash("CombatMode");
    private static readonly int DestinationHash = Animator.StringToHash("EquipmentTargetMode");
    private static readonly int ParryHash = Animator.StringToHash("Parry");

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // The source state identifies what to put away. A separate latched
        // destination identifies Normal or the next weapon to equip.
        destination = equipping ? (int)weapon : Mathf.Clamp(animator.GetInteger(ModeHash), 0, 3);
        LockRequest(animator);
        animator.GetComponentInParent<PlayerStateManager>()?.NotifyEquipmentStarted(
            weapon, equipping, (PlayerCombatMode)destination);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => LockRequest(animator);

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => LockRequest(animator);

    private void LockRequest(Animator animator)
    {
        animator.SetInteger(ModeHash, destination);
        animator.SetInteger(DestinationHash, destination);
        animator.ResetTrigger(ParryHash);
    }
}
