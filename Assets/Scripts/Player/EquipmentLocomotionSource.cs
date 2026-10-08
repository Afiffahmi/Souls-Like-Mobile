using UnityEngine;

// Tracks the gait before equipment takes over the base layer. The separate
// masked layer has no gameplay behaviours or equipment transitions of its own.
public sealed class EquipmentLocomotionSource : StateMachineBehaviour
{
    public PlayerCombatMode mode;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => Track(animator, stateInfo);

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => Track(animator, stateInfo);

    private void Track(Animator animator, AnimatorStateInfo stateInfo)
        => animator.GetComponentInParent<PlayerStateManager>()?.TrackEquipmentLocomotion(mode, stateInfo.normalizedTime);
}
