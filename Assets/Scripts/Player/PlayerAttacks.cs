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
    private readonly SwordLightComboProgress swordLightCombo = new SwordLightComboProgress();
    public bool IsSwordLightComboFinisher => attackStateEntered && activeAttackConfiguration != null &&
        activeAttackConfiguration.weapon == PlayerCombatMode.Sword && activeAttackInput == CombatAttackInput.LightAttack &&
        activeAttackIndex == 2 && swordLightCombo.IsFinisher;
    private double attackRecoveryUntil;
    private double AttackClock => anim != null && anim.updateMode == AnimatorUpdateMode.UnscaledTime
        ? Time.unscaledTimeAsDouble : Time.timeAsDouble;
    public float AttackRecoveryRemaining => (float)System.Math.Max(0, attackRecoveryUntil - AttackClock);
    private InputAction lightAttackAction, heavyAttackAction, specialAttackAction;

    public bool IsAttacking => bowHeavyActive || bowAttackActive || attackPendingOrActive || (anim != null && anim.isInitialized &&
        (anim.GetCurrentAnimatorStateInfo(0).IsTag("CombatAttack") ||
         (anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsTag("CombatAttack"))));
    public bool HasBufferedAttack => followUpBuffered;
    // Includes the accepted request before Animator entry and every chained step.
    public bool IsAttackMovementLocked => bowHeavyActive || bowAttackActive || (attackPendingOrActive && activeAttackInput == CombatAttackInput.LightAttack);
    public int CurrentAttackNumber => bowHeavyActive ? (bowHeavyCharged ? 2 : 1) : bowAttackActive ? 1 : IsAttacking ? activeAttackIndex + 1 : 0;
    public CombatAttackInput? CurrentAttackInput => bowHeavyActive ? CombatAttackInput.HeavyAttack : bowAttackActive ? CombatAttackInput.LightAttack :
        IsAttacking ? activeAttackInput : (CombatAttackInput?)null;
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
        if (Pressed(lightAttackAction))
        {
            if (TryAttack(CombatAttackInput.LightAttack) && bowAttackActive) bowUsesInput = true;
        }
        else if (Pressed(heavyAttackAction))
        {
            if (TryAttack(CombatAttackInput.HeavyAttack) && bowHeavyActive) bowHeavyUsesInput = true;
        }
        else if (Pressed(specialAttackAction)) TryAttack(CombatAttackInput.SpecialAttack);
    }

    private void OnLightAttack(InputValue value) { }
    private void OnHeavyAttack(InputValue value) { }
    private void OnSpecialAttack(InputValue value) { }
    public void LightAttack() => TryAttack(CombatAttackInput.LightAttack);
    public void HeavyAttack() => RequestHeavyAttackClick();
    public void SpecialAttack() => TryAttack(CombatAttackInput.SpecialAttack);

    public float GetSpecialAttackCooldownRemaining(PlayerCombatMode weapon) =>
        attackConfigurations.TryGetValue(weapon, out var configuration)
            ? specialCooldowns.Remaining(configuration, Time.timeAsDouble) : 0f;

    public bool TryAttack(CombatAttackInput input)
    {
        if (!isActiveAndEnabled || !hasCombatParameters || anim == null || !anim.isActiveAndEnabled || !anim.isInitialized ||
            IsChangingEquipment || IsParrying || IsRolling || AttackRecoveryRemaining > 0 ||
            !System.Enum.IsDefined(typeof(CombatAttackInput), input)) return false;
        if (IsAttacking) return !bowHeavyActive && !bowAttackActive && TryBufferAttack(input);
        if (anim.IsInTransition(0) || !anim.GetCurrentAnimatorStateInfo(0).IsTag("CombatLocomotion") ||
            anim.GetInteger(ParryReturnModeHash) != (int)CombatMode) return false;
        if (CombatMode == PlayerCombatMode.Bow && input == CombatAttackInput.HeavyAttack)
            return TryBeginBowHeavyAttack();
        if (CombatMode == PlayerCombatMode.Bow && input == CombatAttackInput.LightAttack)
            return TryBeginBowAttack();
        if (!attackConfigurations.TryGetValue(CombatMode, out var config) || !config.Validate(out _)) return false;
        var chain = config.Chain(input);
        if (input != CombatAttackInput.SpecialAttack && (chain == null || chain.Count == 0)) return false;
        if (!anim.HasState(0, Animator.StringToHash(config.StatePath(input, 0)))) return false;
        if (input == CombatAttackInput.SpecialAttack && !specialCooldowns.TryStart(config, Time.timeAsDouble)) return false;
        activeAttackConfiguration = config;
        activeAttackInput = input;
        activeAttackIndex = 0;
        swordLightCombo.Reset();
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
        if (step.useChainTransitionFrame && info.normalizedTime >= step.ChainTransitionNormalized)
        {
            // Animator exit-time conditions are one-shot. A late press must not
            // wait for a threshold that this non-looping clip already passed.
            anim.SetBool(AttackBufferedHash, false);
            anim.CrossFadeInFixedTime(activeAttackConfiguration.StatePath(input, activeAttackIndex + 1), 0f, 0, 0f);
        }
        else anim.SetBool(AttackBufferedHash, true);
        return true;
    }

    private void MaintainAttackLock()
    {
        if (bowHeavyActive)
        {
            UpdateBowHeavyAttack();
            return;
        }
        if (bowAttackActive)
        {
            UpdateBowAttack();
            return;
        }
        if (activeAttackConfiguration == null) return;
        anim.SetInteger(CombatModeHash, (int)activeAttackConfiguration.weapon);
        anim.ResetTrigger(ParryHash);
        if (attackStateEntered) ClearAttackTriggers();
        anim.SetFloat(SpeedHash, Mathf.Min(anim.GetFloat(SpeedHash), 1f));
    }

    public bool NotifyAttackEntered(CombatAttackConfiguration config, CombatAttackInput input, int index)
    {
        if (config == null || !isActiveAndEnabled || AttackRecoveryRemaining > 0) return false;
        bool acceptedSpecialRequest = attackPendingOrActive && !attackStateEntered && activeAttackConfiguration == config && activeAttackInput == input;
        // Direct Animator triggers must respect Special's cooldown too.
        if (input == CombatAttackInput.SpecialAttack && !acceptedSpecialRequest && !specialCooldowns.TryStart(config, Time.timeAsDouble)) return false;
        bool continuingLightCombo = attackStateEntered && activeAttackConfiguration == config && activeAttackInput == CombatAttackInput.LightAttack;
        if (config.weapon == PlayerCombatMode.Sword && input == CombatAttackInput.LightAttack)
            swordLightCombo.Enter(index, continuingLightCombo);
        else swordLightCombo.Reset();
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

    public void NotifyAttackExited(CombatAttackConfiguration config, CombatAttackInput input, int index, float normalizedTime)
    {
        var chain = config != null ? config.Chain(input) : null;
        if (chain == null || index < 0 || index >= chain.Count || normalizedTime < 1f) return;
        // Starts on actual animation completion, so playback-speed changes and
        // pauses cannot shorten the animation portion of the lockout.
        attackRecoveryUntil = System.Math.Max(attackRecoveryUntil, AttackClock + chain[index].postAttackRecovery);
    }

    private void NotifyAttackLocomotionEntered(PlayerCombatMode mode)
    {
        if (bowHeavyActive && bowHeavyEntered) ResetBowHeavyAttack();
        if (bowAttackActive && bowStateEntered) ResetBowAttack();
        // Do not cancel an accepted trigger before its first attack state enters.
        if (attackPendingOrActive && !attackStateEntered) return;
        if (activeAttackConfiguration != null) ResetAttackSequence();
    }

    private void ResetAttackSequence()
    {
        swordLightCombo.Reset();
        ResetBowHeavyAttack();
        ResetBowAttack();
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
