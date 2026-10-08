using UnityEngine;

public partial class PlayerStateManager
{
    private const string EquipmentLegLayer = "Equipment Legs";
    private PlayerCombatMode equipmentLocomotionSource;
    private float equipmentLocomotionTime;
    private bool equipmentLegsActive;

    public void TrackEquipmentLocomotion(PlayerCombatMode mode, float normalizedTime)
    {
        // Retain the outgoing gait throughout Unequip -> Equip when swapping weapons.
        if (equipmentLegsActive) return;
        equipmentLocomotionSource = mode;
        equipmentLocomotionTime = normalizedTime;
    }

    private void BeginEquipmentLegLocomotion()
    {
        if (equipmentLegsActive || anim == null) return;
        int layer = anim.GetLayerIndex(EquipmentLegLayer);
        if (layer < 0) return; // Compatible with controllers without this optional layer.
        int state = Animator.StringToHash(EquipmentLegLayer + "." + equipmentLocomotionSource);
        if (!anim.HasState(layer, state)) return;
        anim.Play(state, layer, Mathf.Repeat(equipmentLocomotionTime, 1f));
        anim.SetLayerWeight(layer, 1f);
        equipmentLegsActive = true;
    }

    private void EndEquipmentLegLocomotion()
    {
        if (anim != null)
        {
            int layer = anim.GetLayerIndex(EquipmentLegLayer);
            if (layer >= 0) anim.SetLayerWeight(layer, 0f);
        }
        equipmentLegsActive = false;
    }
}
