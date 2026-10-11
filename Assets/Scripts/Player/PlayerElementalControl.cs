using UnityEngine;

public partial class PlayerStateManager
{
    private ElementalGems.PlayerElementalState elementalStatus;
    private ElementalGems.PlayerElementalState ElementalStatus
    { get { if (elementalStatus == null) TryGetComponent(out elementalStatus); return elementalStatus; } }
    private bool ElementalActionBlocked => ElementalStatus != null && ElementalStatus.CannotAct;
    private bool ElementalMovementBlocked => ElementalStatus != null && ElementalStatus.CannotMove;
    private float ElementalMovementScale => ElementalStatus != null ? ElementalStatus.MovementScale : 1;
    public void InterruptForElementalStatus()
    {
        if (TryGetComponent<SoulsLike.Riding.MountedPlayerController>(out var mounted) && mounted.IsBusy) return;
        var mode = CombatMode;
        DisableCombat();
        planarVelocity = Vector3.zero;
        ResetLocomotionAnimation();
        if (anim == null || !anim.isInitialized || !anim.isActiveAndEnabled) return;
        string locomotion = RollConfiguration.LocomotionPath(mode);
        if (anim.HasState(0, Animator.StringToHash(locomotion))) anim.CrossFadeInFixedTime(locomotion, .08f, 0, 0);
    }
}
