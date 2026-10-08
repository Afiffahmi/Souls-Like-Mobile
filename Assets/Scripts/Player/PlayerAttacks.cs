using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerStateManager
{
    private static readonly int AttackBufferedHash = Animator.StringToHash("AttackBuffered");
    private static readonly int LightAttackHash = Animator.StringToHash("LightAttack");
    private static readonly int HeavyAttackHash = Animator.StringToHash("HeavyAttack");
    private static readonly int SpecialAttackHash = Animator.StringToHash("SpecialAttack");
    private readonly Dictionary<PlayerCombatMode, CombatAttackConfiguration> attackConfigurations = new Dictionary<PlayerCombatMode, CombatAttackConfiguration>();
    private readonly CombatSpecialCooldowns specialCooldowns = new CombatSpecialCooldowns();
    private CombatAttackConfiguration activeAttackConfiguration;
    private CombatAttackInput activeAttackInput;
    private int activeAttackIndex;
    private bool attackPendingOrActive;
    private bool attackStateEntered;
    private bool followUpBuffered;
    private InputAction lightAttackAction, heavyAttackAction, specialAttackAction;

    public bool IsAttacking => attackPendingOrActive || (anim != null && anim.isInitialized &&
        (anim.GetCurrentAnimatorStateInfo(0).IsTag("CombatAttack") ||
         (anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsTag("CombatAttack"))));
    public bool HasBufferedAttack => followUpBuffered;
    // Includes the accepted request before Animator entry and every chained step.
    public bool IsAttackMovementLocked => attackPendingOrActive && activeAttackInput == CombatAttackInput.LightAttack;
    public int CurrentAttackNumber => IsAttacking ? activeAttackIndex + 1 : 0;
    public CombatAttackInput? CurrentAttackInput => IsAttacking ? activeAttackInput : (CombatAttackInput?)null;
    public float SpecialAttackCooldownRemaining => GetSpecialAttackCooldownRemaining(CombatMode);

    // Configurations are referenced by their generated Animator states, avoiding
    // scene-specific setup and keeping future weapons on the same input API.
    private void InitializeAttacks()
    {
        attackConfigurations.Clear();
        if (anim == null || anim.runtimeAnimatorController == null) return;
        foreach (var state in anim.GetBehaviours<CombatAttackState>())
            if (state.configuration != null)
                attackConfigurations[state.configuration.weapon] = state.configuration;
    }

    private void BindAttackInput()
    {
        if (movementInput == null || movementInput.actions == null) return;
        lightAttackAction = movementInput.actions.FindAction("LightAttack", false);
        heavyAttackAction = movementInput.actions.FindAction("HeavyAttack", false);
        specialAttackAction = movementInput.actions.FindAction("SpecialAttack", false);
    }

    private void PollAttackInput()
    {
        if (movementInput == null || !movementInput.isActiveAndEnabled || !movementInput.inputIsActive) return;
        // One request per frame; Light, Heavy, Special is the simultaneous-press priority.
        if (Pressed(lightAttackAction)) TryAttack(CombatAttackInput.LightAttack);
        else if (Pressed(heavyAttackAction)) TryAttack(CombatAttackInput.HeavyAttack);
        else if (Pressed(specialAttackAction)) TryAttack(CombatAttackInput.SpecialAttack);
    }

    private void OnLightAttack(InputValue value) { }
    private void OnHeavyAttack(InputValue value) { }
    private void OnSpecialAttack(InputValue value) { }
    public void LightAttack() => TryAttack(CombatAttackInput.LightAttack);
    public void HeavyAttack() => TryAttack(CombatAttackInput.HeavyAttack);
    public void SpecialAttack() => TryAttack(CombatAttackInput.SpecialAttack);

    public float GetSpecialAttackCooldownRemaining(PlayerCombatMode weapon) =>
        attackConfigurations.TryGetValue(weapon, out var configuration)
            ? specialCooldowns.Remaining(configuration, Time.timeAsDouble) : 0f;

    public bool TryAttack(CombatAttackInput input)
    {
        if (!isActiveAndEnabled || !hasCombatParameters || anim == null || !anim.isActiveAndEnabled || !anim.isInitialized ||
            IsChangingEquipment || IsParrying || IsRolling || !System.Enum.IsDefined(typeof(CombatAttackInput), input)) return false;
        if (IsAttacking) return TryBufferAttack(input);
        if (anim.IsInTransition(0) || !anim.GetCurrentAnimatorStateInfo(0).IsTag("CombatLocomotion") ||
            anim.GetInteger(ParryReturnModeHash) != (int)CombatMode ||
            !attackConfigurations.TryGetValue(CombatMode, out var config) || !config.Validate(out _)) return false;
        var chain = config.Chain(input);
        if (input != CombatAttackInput.SpecialAttack && (chain == null || chain.Count == 0)) return false;
        if (!anim.HasState(0, Animator.StringToHash(config.StatePath(input, 0)))) return false;
        if (input == CombatAttackInput.SpecialAttack && !specialCooldowns.TryStart(config, Time.timeAsDouble)) return false;
        activeAttackConfiguration = config;
        activeAttackInput = input;
        activeAttackIndex = 0;
        attackPendingOrActive = true;
        attackStateEntered = false;
        followUpBuffered = false;
        ClearAttackTriggers();
        anim.SetBool(AttackBufferedHash, false);
        anim.ResetTrigger(ParryHash);
        anim.SetTrigger(TriggerFor(input));
        return true;
    }

    private bool TryBufferAttack(CombatAttackInput input)
    {
        if (!attackStateEntered || activeAttackConfiguration == null || input != activeAttackInput ||
            input == CombatAttackInput.SpecialAttack || followUpBuffered || anim.IsInTransition(0)) return false;
        var chain = activeAttackConfiguration.Chain(input);
        if (chain == null || activeAttackIndex < 0 || activeAttackIndex + 1 >= chain.Count) return false;
        var info = anim.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName(activeAttackConfiguration.StatePath(input, activeAttackIndex))) return false;
        var step = chain[activeAttackIndex];
        if (info.normalizedTime < step.comboWindowStart || info.normalizedTime > step.comboWindowEnd) return false;
        followUpBuffered = true;
        // The transition also requires its authored exit time. This does not
        // change animation until the step's configured chain transition point.
        anim.SetBool(AttackBufferedHash, true);
        return true;
    }

    private void MaintainAttackLock()
    {
        if (activeAttackConfiguration == null) return;
        anim.SetInteger(CombatModeHash, (int)activeAttackConfiguration.weapon);
        anim.ResetTrigger(ParryHash);
        if (attackStateEntered) ClearAttackTriggers();
        anim.SetFloat(SpeedHash, Mathf.Min(anim.GetFloat(SpeedHash), 1f));
    }

    public bool NotifyAttackEntered(CombatAttackConfiguration config, CombatAttackInput input, int index)
    {
        if (config == null || !isActiveAndEnabled) return false;
        bool acceptedSpecialRequest = attackPendingOrActive && !attackStateEntered && activeAttackConfiguration == config && activeAttackInput == input;
        // Direct Animator triggers must respect Special's cooldown too.
        if (input == CombatAttackInput.SpecialAttack && !acceptedSpecialRequest && !specialCooldowns.TryStart(config, Time.timeAsDouble)) return false;
        activeAttackConfiguration = config;
        activeAttackInput = input;
        activeAttackIndex = index;
        attackPendingOrActive = true;
        attackStateEntered = true;
        followUpBuffered = false;
        anim.SetBool(AttackBufferedHash, false);
        ClearAttackTriggers();
        MaintainAttackLock();
        EquipmentPhase = PlayerEquipmentPhase.None;
        ActionState = PlayerActionState.Attack;
        AttackSubstate = config.weapon switch { PlayerCombatMode.Sword => PlayerAttackSubstate.Sword,
            PlayerCombatMode.Magic => PlayerAttackSubstate.Magic, PlayerCombatMode.Bow => PlayerAttackSubstate.Bow, _ => PlayerAttackSubstate.None };
        SetActiveStatePath($"Attack > {config.weapon} > {config.StateName(input, index)}");
        return true;
    }

    private void NotifyAttackLocomotionEntered(PlayerCombatMode mode)
    {
        // Do not cancel an accepted trigger before its first attack state enters.
        if (attackPendingOrActive && !attackStateEntered) return;
        if (activeAttackConfiguration != null) ResetAttackSequence();
    }

    private void ResetAttackSequence()
    {
        attackPendingOrActive = false;
        attackStateEntered = false;
        followUpBuffered = false;
        activeAttackIndex = 0;
        activeAttackConfiguration = null;
        if (anim == null || attackConfigurations.Count == 0) return;
        ClearAttackTriggers();
        anim.SetBool(AttackBufferedHash, false);
    }

    private void ClearAttackTriggers()
    {
        anim.ResetTrigger(LightAttackHash);
        anim.ResetTrigger(HeavyAttackHash);
        anim.ResetTrigger(SpecialAttackHash);
    }
    private static int TriggerFor(CombatAttackInput input) => input == CombatAttackInput.LightAttack
        ? LightAttackHash : input == CombatAttackInput.HeavyAttack ? HeavyAttackHash : SpecialAttackHash;
}
