using UnityEngine;

public enum PlayerEquipmentPhase { None, Equip, Unequip }

// Animation-only equipment flow; no inventory, weapon spawning, or gameplay effects.
public partial class PlayerStateManager
{
    private bool equipmentChangeRequested;
    private PlayerCombatMode equipmentDestination;

    public PlayerEquipmentPhase EquipmentPhase { get; private set; }
    public bool IsChangingEquipment => equipmentChangeRequested || AnimatorIsChangingEquipment;

    private bool AnimatorIsChangingEquipment => hasCombatParameters && anim != null && anim.isInitialized &&
        (anim.GetCurrentAnimatorStateInfo(0).IsTag("CombatEquipment") ||
         (anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsTag("CombatEquipment")));

    private void BeginEquipmentRequest(PlayerCombatMode destination)
    {
        equipmentDestination = destination;
        equipmentChangeRequested = true;
    }

    private void MaintainEquipmentRequest()
    {
        anim.SetInteger(CombatModeHash, (int)equipmentDestination);
        anim.ResetTrigger(ParryHash);
        anim.SetFloat(SpeedHash, Mathf.Min(anim.GetFloat(SpeedHash), 1f));
    }

    public void NotifyEquipmentStarted(PlayerCombatMode weapon, bool equipping, PlayerCombatMode destination)
    {
        BeginEquipmentLegLocomotion();
        BeginEquipmentRequest(destination);
        EquipmentPhase = equipping ? PlayerEquipmentPhase.Equip : PlayerEquipmentPhase.Unequip;
        ActionState = PlayerActionState.Attack;
        AttackSubstate = weapon switch
        {
            PlayerCombatMode.Sword => PlayerAttackSubstate.Sword,
            PlayerCombatMode.Magic => PlayerAttackSubstate.Magic,
            PlayerCombatMode.Bow => PlayerAttackSubstate.Bow,
            _ => PlayerAttackSubstate.None
        };
        SetActiveStatePath($"Attack > {weapon} > {EquipmentPhase}");
    }

    private void NotifyEquipmentSettled(PlayerCombatMode mode)
    {
        // An intermediate Normal Idle/Locomotion callback must not cancel a
        // pending equip. Clear only when the requested final state is entered.
        if (equipmentChangeRequested && mode == equipmentDestination)
        {
            equipmentChangeRequested = false;
            EndEquipmentLegLocomotion();
        }
    }
}
