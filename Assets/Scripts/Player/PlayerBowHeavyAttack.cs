using UnityEngine;

public partial class PlayerStateManager
{
    public const string BowHeavySingleStatePath = "Base Layer.Attack.Bow_HeavyAttack_1";
    public const string BowHeavyChargedStatePath = "Base Layer.Attack.Bow_HeavyAttack_2";
    private static readonly int BowHeavyTimeHash = Animator.StringToHash("BowHeavyTime");
    private static readonly int BowHeavyFinishedHash = Animator.StringToHash("BowHeavyFinished");
    private static readonly int BowHeavyChargedHash = Animator.StringToHash("BowHeavyCharged");
    private readonly BowHeavyHoldCycle bowHeavyCycle = new BowHeavyHoldCycle();
    private readonly BowHeavyAttackTimeline bowHeavyTimeline = new BowHeavyAttackTimeline();
    private BowHeavyAttackConfiguration bowHeavyConfiguration;
    private AnimationClip bowHeavyClip;
    private bool hasBowHeavyControl, bowHeavyActive, bowHeavyCharging, bowHeavyEntered, bowHeavyCharged, bowHeavyUsesInput;

    public bool IsChargingBowHeavyAttack => bowHeavyCharging;
    public bool IsBowHeavyAttacking => bowHeavyActive;
    public float BowHeavyAnimationFrame => bowHeavyTimeline.Frame;
    public int BowHeavyArrowsReleased => bowHeavyTimeline.Shots;
    private double BowHeavyClock => anim.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledTimeAsDouble : Time.timeAsDouble;

    private void InitializeBowHeavyAttack()
    {
        foreach (var state in anim.GetBehaviours<CombatBowHeavyAttackState>())
            if (state.configuration != null) bowHeavyConfiguration = state.configuration;
        bool time = false, finished = false, charged = false;
        foreach (var p in anim.parameters)
        {
            time |= p.nameHash == BowHeavyTimeHash && p.type == AnimatorControllerParameterType.Float;
            finished |= p.nameHash == BowHeavyFinishedHash && p.type == AnimatorControllerParameterType.Bool;
            charged |= p.nameHash == BowHeavyChargedHash && p.type == AnimatorControllerParameterType.Bool;
        }
        hasBowHeavyControl = time && finished && charged && bowHeavyConfiguration != null && bowHeavyConfiguration.Validate() &&
            anim.HasState(0, Animator.StringToHash(BowHeavySingleStatePath)) && anim.HasState(0, Animator.StringToHash(BowHeavyChargedStatePath));
    }

    private bool TryBeginBowHeavyAttack()
    {
        if (!hasBowHeavyControl || !bowHeavyConfiguration.Validate() || !bowHeavyCycle.Begin(BowHeavyClock)) return false;
        bowHeavyActive = bowHeavyCharging = true;
        bowHeavyEntered = bowHeavyCharged = bowHeavyUsesInput = false;
        bowHeavyTimeline.Reset();
        ClearAttackTriggers();
        anim.ResetTrigger(ParryHash);
        anim.SetBool(BowHeavyFinishedHash, false);
        anim.SetFloat(BowHeavyTimeHash, 0f);
        ActionState = PlayerActionState.Attack;
        AttackSubstate = PlayerAttackSubstate.Bow;
        SetActiveStatePath("Attack > Bow > Heavy charge");
        return true;
    }

    /// <summary>PointerDown API; the existing OnScreenButton also works via HeavyAttack input.</summary>
    public void BeginHeavyAttackHold() => TryAttack(CombatAttackInput.HeavyAttack);
    public void EndHeavyAttackHold() => AdvanceBowHeavyHold(BowHeavyClock, false);

    // A click-only UnityEvent is an explicit short press; hold controls use the paired API.
    private void RequestHeavyAttackClick()
    {
        if (TryAttack(CombatAttackInput.HeavyAttack) && bowHeavyCharging) EndHeavyAttackHold();
    }

    private void PollBowHeavyRelease()
    {
        if (!bowHeavyUsesInput || !bowHeavyCycle.Held) return;
        if (movementInput == null || !movementInput.isActiveAndEnabled || !movementInput.inputIsActive ||
            heavyAttackAction == null || !heavyAttackAction.enabled)
        {
            CancelHeavyAttackHold();
            return;
        }
        if (!heavyAttackAction.IsPressed()) EndHeavyAttackHold();
    }

    private void AdvanceBowHeavyHold(double now, bool held)
    {
        bool? decision = bowHeavyCycle.Advance(now, held, bowHeavyConfiguration != null ? bowHeavyConfiguration.holdDuration : 3f);
        if (!bowHeavyCharging || !decision.HasValue) return;
        bowHeavyCharging = false;
        bowHeavyCharged = decision.Value;
        bowHeavyClip = bowHeavyCharged ? bowHeavyConfiguration.chargedAnimation : bowHeavyConfiguration.singleAnimation;
        bowHeavyTimeline.Begin(bowHeavyCharged ? bowHeavyConfiguration.chargedReleaseFrames : new[] { bowHeavyConfiguration.singleReleaseFrame },
            bowHeavyClip.length * bowHeavyClip.frameRate);
        anim.SetBool(BowHeavyChargedHash, bowHeavyCharged);
        ClearAttackTriggers();
        anim.SetTrigger(HeavyAttackHash);
    }

    public bool NotifyBowHeavyAttackEntered(BowHeavyAttackConfiguration configuration, bool charged)
    {
        if (!isActiveAndEnabled || !bowHeavyActive || bowHeavyCharging || configuration != bowHeavyConfiguration || charged != bowHeavyCharged) return false;
        bowHeavyEntered = true;
        ClearAttackTriggers();
        EquipmentPhase = PlayerEquipmentPhase.None;
        ActionState = PlayerActionState.Attack;
        AttackSubstate = PlayerAttackSubstate.Bow;
        SetActiveStatePath(charged ? "Attack > Bow > Charged heavy" : "Attack > Bow > Single heavy");
        return true;
    }

    private void UpdateBowHeavyAttack()
    {
        anim.SetInteger(CombatModeHash, (int)PlayerCombatMode.Bow);
        anim.ResetTrigger(ParryHash);
        if (bowHeavyCharging)
        {
            ClearAttackTriggers();
            AdvanceBowHeavyHold(BowHeavyClock, bowHeavyCycle.Held);
            return;
        }
        if (!bowHeavyEntered) return;
        ClearAttackTriggers();
        float dt = anim.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        AdvanceBowHeavyAnimation(dt);
    }

    private void AdvanceBowHeavyAnimation(float dt)
    {
        if (!bowHeavyEntered) return;
        bool release = bowHeavyTimeline.Advance(dt * Mathf.Max(0f, anim.speed) * bowHeavyConfiguration.playbackSpeed * bowHeavyClip.frameRate);
        anim.SetFloat(BowHeavyTimeHash, bowHeavyTimeline.Frame / (bowHeavyClip.length * bowHeavyClip.frameRate));
        // Completion is only possible on a later update than the final release.
        anim.SetBool(BowHeavyFinishedHash, bowHeavyTimeline.Finished);
        if (release) BowReleased?.Invoke();
    }

    /// <summary>Input cancellation discards an uncommitted hold; a committed volley finishes.</summary>
    public void CancelHeavyAttackHold()
    {
        bowHeavyCycle.Cancel();
        bowHeavyUsesInput = false;
        if (!bowHeavyCharging) return;
        ResetBowHeavyAttack();
        ActionState = PlayerActionState.Attack;
        AttackSubstate = PlayerAttackSubstate.Bow;
        SetActiveStatePath("Attack > Bow");
    }

    private void ResetBowHeavyAttack()
    {
        bowHeavyActive = bowHeavyCharging = bowHeavyEntered = false;
        bowHeavyClip = null;
        bowHeavyTimeline.Reset();
        // Keep the input cycle consumed until its physical release.
        if (hasBowHeavyControl && anim != null) anim.SetBool(BowHeavyFinishedHash, true);
    }

    private void OnApplicationFocus(bool focused) { if (!focused) CancelHeavyAttackHold(); }
    private void OnApplicationPause(bool paused) { if (paused) CancelHeavyAttackHold(); }
}
