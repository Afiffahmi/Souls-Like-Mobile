using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerCombatMode { Normal = 0, Sword = 1, Magic = 2, Bow = 3 }

// Combat is an optional extension of the existing motor. Facing, movement frame,
// gravity, lock-on and the normal locomotion state selection stay with that motor.
public partial class PlayerStateManager
{
    private static readonly int CombatModeHash = Animator.StringToHash("CombatMode");
    private static readonly int ParryHash = Animator.StringToHash("Parry");
    private static readonly int ParryReturnModeHash = Animator.StringToHash("ParryReturnMode");
    private bool hasCombatParameters;
    private bool parryRequested;
    private PlayerCombatMode parryReturnMode;
    private InputAction normalModeAction, swordModeAction, magicModeAction, bowModeAction, parryAction;

    public PlayerCombatMode CombatMode => hasCombatParameters && anim != null
        ? (PlayerCombatMode)anim.GetInteger(CombatModeHash) : PlayerCombatMode.Normal;
    public bool IsParrying => parryRequested || AnimatorIsInParry;
    public bool IsCombatActive => hasCombatParameters && anim != null &&
        (CombatMode != PlayerCombatMode.Normal || IsParrying || IsChangingEquipment ||
         (anim.isInitialized && anim.GetCurrentAnimatorStateInfo(0).IsTag("CombatLocomotion")));
    public float ParryNormalizedTime => AnimatorIsInParry
        ? Mathf.Clamp01(anim.GetCurrentAnimatorStateInfo(0).normalizedTime) : 0f;

    // Hooks for future timing/window consumers; these perform no gameplay effects.
    public event System.Action ParryStarted;
    public event System.Action ParryCompleted;

    private bool AnimatorIsInParry => hasCombatParameters && anim != null && anim.isInitialized &&
        (anim.GetCurrentAnimatorStateInfo(0).IsTag("CombatParry") ||
         (anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsTag("CombatParry")));

    private void InitializeCombat()
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;
        bool mode = false, trigger = false, returnMode = false;
        foreach (var p in anim.parameters)
        {
            mode |= p.nameHash == CombatModeHash && p.type == AnimatorControllerParameterType.Int;
            trigger |= p.nameHash == ParryHash && p.type == AnimatorControllerParameterType.Trigger;
            returnMode |= p.nameHash == ParryReturnModeHash && p.type == AnimatorControllerParameterType.Int;
        }
        hasCombatParameters = mode && trigger && returnMode;
    }

    private void BindCombatInput()
    {
        if (movementInput == null || movementInput.actions == null) return;
        normalModeAction = movementInput.actions.FindAction("SelectNormal", false);
        swordModeAction = movementInput.actions.FindAction("SelectSword", false);
        magicModeAction = movementInput.actions.FindAction("SelectMagic", false);
        bowModeAction = movementInput.actions.FindAction("SelectBow", false);
        parryAction = movementInput.actions.FindAction("Parry", false);
    }

    private void UpdateCombat()
    {
        if (!hasCombatParameters || anim == null || !anim.isActiveAndEnabled) return;
        if (IsChangingEquipment)
        {
            MaintainEquipmentRequest();
            return;
        }
        if (IsParrying)
        {
            // Lock the remembered mode even if another caller edits the parameter.
            anim.SetInteger(CombatModeHash, (int)parryReturnMode);
            if (AnimatorIsInParry) anim.ResetTrigger(ParryHash);
            return;
        }
        int mode = anim.GetInteger(CombatModeHash);
        if (mode < 0 || mode > 3) anim.SetInteger(CombatModeHash, 0);
        if (movementInput != null && movementInput.isActiveAndEnabled && movementInput.inputIsActive)
        {
            if (Pressed(normalModeAction)) TrySetCombatMode(PlayerCombatMode.Normal);
            else if (Pressed(swordModeAction)) TrySetCombatMode(PlayerCombatMode.Sword);
            else if (Pressed(magicModeAction)) TrySetCombatMode(PlayerCombatMode.Magic);
            else if (Pressed(bowModeAction)) TrySetCombatMode(PlayerCombatMode.Bow);
            if (Pressed(parryAction)) TryParry();
        }
        if (IsCombatActive)
            anim.SetFloat(SpeedHash, Mathf.Min(anim.GetFloat(SpeedHash), 1f));
        else
            anim.ResetTrigger(ParryHash); // Do not queue a normal-mode parry for later.
    }

    private static bool Pressed(InputAction action) => action != null && action.enabled && action.WasPressedThisFrame();

    // PlayerInput uses SendMessages. Polling above is the single dispatch point,
    // so these receivers deliberately do not also trigger actions.
    private void OnSelectNormal(InputValue value) { }
    private void OnSelectSword(InputValue value) { }
    private void OnSelectMagic(InputValue value) { }
    private void OnSelectBow(InputValue value) { }
    private void OnParry(InputValue value) { }

    /// <summary>Parry/equipment animations finish before accepting another mode change.</summary>
    public bool TrySetCombatMode(PlayerCombatMode mode)
    {
        if (!hasCombatParameters || anim == null || !isActiveAndEnabled ||
            !anim.isActiveAndEnabled || (int)mode < 0 || (int)mode > 3 || IsParrying || IsChangingEquipment) return false;
        if (mode == CombatMode) return true;
        BeginEquipmentRequest(mode);
        anim.SetInteger(CombatModeHash, (int)mode);
        if (mode != PlayerCombatMode.Normal)
        {
            // Remove an already-held sprint and any damped run weight immediately.
            planarVelocity = Vector3.ClampMagnitude(planarVelocity, walkSpeed);
            anim.SetFloat(SpeedHash, Mathf.Min(anim.GetFloat(SpeedHash), 1f));
        }
        anim.ResetTrigger(ParryHash);
        return true;
    }

    // UnityEvent/mobile button entry points. The same parry API serves all modes.
    public void SetCombatMode(int mode) => TrySetCombatMode((PlayerCombatMode)mode);
    public void SelectNormal() => TrySetCombatMode(PlayerCombatMode.Normal);
    public void SelectSword() => TrySetCombatMode(PlayerCombatMode.Sword);
    public void SelectMagic() => TrySetCombatMode(PlayerCombatMode.Magic);
    public void SelectBow() => TrySetCombatMode(PlayerCombatMode.Bow);
    public void RequestParry() => TryParry();

    public bool TryParry()
    {
        if (!hasCombatParameters || anim == null || !isActiveAndEnabled || !anim.isActiveAndEnabled ||
            !anim.isInitialized || IsParrying || IsChangingEquipment || CombatMode == PlayerCombatMode.Normal || anim.IsInTransition(0) ||
            !anim.GetCurrentAnimatorStateInfo(0).IsTag("CombatLocomotion")) return false;
        // Latch the actual current Animator mode, not a newly requested next mode.
        parryReturnMode = (PlayerCombatMode)anim.GetInteger(ParryReturnModeHash);
        if (parryReturnMode != CombatMode) return false;
        parryRequested = true;
        anim.ResetTrigger(ParryHash);
        anim.SetTrigger(ParryHash);
        return true;
    }

    public void NotifyParryStarted(PlayerCombatMode mode)
    {
        parryReturnMode = mode;
        parryRequested = true;
        ParryStarted?.Invoke();
    }

    public void NotifyParryCompleted()
    {
        parryRequested = false;
        ParryCompleted?.Invoke();
    }

    private void DisableCombat()
    {
        parryRequested = false;
        if (hasCombatParameters && anim != null) anim.ResetTrigger(ParryHash);
    }
}
