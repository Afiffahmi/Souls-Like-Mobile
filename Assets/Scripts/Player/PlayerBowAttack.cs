using UnityEngine;

public partial class PlayerStateManager
{
    public const string BowAttackStatePath = "Base Layer.Attack.Bow_LightAttack";
    private static readonly int BowTimeHash = Animator.StringToHash("BowTime");
    private static readonly int BowFinishedHash = Animator.StringToHash("BowFinished");
    private readonly BowAttackTimeline bowTimeline = new BowAttackTimeline();
    private AnimationClip bowCharacterClip;
    private bool hasBowControl, bowAttackActive, bowStateEntered;
    private bool bowUsesInput, bowButtonHeld;
    private float bowCapturedBaseSpeed = 1f;
    private float bowCapturedDrawSpeed = 1f;

    [Header("Bow")]
    [Tooltip("Shared bow speed: light draw/release, heavy charge and heavy release. Equipment SPD multiplies this value.")]
    [HideInInspector, Min(0.01f)] public float bowAttackSpeed = 0.4f; // Legacy migration only.
    public float BowBaseAttackSpeed => CaptureAttackDefaults(PlayerCombatMode.Bow,CombatAttackInput.LightAttack).speed;
    [Tooltip("Draw-only speed multiplier. Does not change hold or release speed.")]
    [HideInInspector, Min(0.01f)] public float bowDrawSpeed = 2f; // Legacy migration only.
    public bool logBowEvents;

    public BowAttackPhase BowPhase => bowTimeline.Phase;
    public float BowAnimationFrame => bowTimeline.Frame;
    public float BowLastFrame => bowCharacterClip == null ? 70f : bowCharacterClip.length * bowCharacterClip.frameRate;
    public bool IsHoldingBow => bowAttackActive && BowPhase == BowAttackPhase.Hold;
    public bool IsBowLightGemCharged => bowAttackActive && bowTimeline.IsGemCharged;
    // Shared by light and heavy frame timelines; the shooter spawns after pose evaluation.
    public event System.Action BowReleased;

    private void InitializeBowAttack()
    {
        bool time = false, finished = false;
        foreach (var parameter in anim.parameters)
        {
            time |= parameter.nameHash == BowTimeHash && parameter.type == AnimatorControllerParameterType.Float;
            finished |= parameter.nameHash == BowFinishedHash && parameter.type == AnimatorControllerParameterType.Bool;
        }
        foreach (var state in anim.GetBehaviours<CombatBowAttackState>())
            if (state.characterAnimation != null) bowCharacterClip = state.characterAnimation;
        hasBowControl = time && finished && bowCharacterClip != null &&
            bowCharacterClip.frameRate > 0f && BowLastFrame > BowAttackTimeline.ReleaseFrame;
    }

    private bool TryBeginBowAttack()
    {
        if (!hasBowControl || !anim.HasState(0, Animator.StringToHash(BowAttackStatePath))) return false;
        bowWeaponStats = CaptureWeaponStats(PlayerCombatMode.Bow, CombatAttackInput.LightAttack);
        bowAttackDefaults = CaptureAttackDefaults(PlayerCombatMode.Bow,CombatAttackInput.LightAttack);
        bowCapturedBaseSpeed = bowAttackDefaults.speed;
        bowCapturedDrawSpeed = Mathf.Max(.01f,WeaponStatModifier.Finite(bowDefaultDrawSpeed,1));
        bowTimeline.Begin(ChargeDuration(bowLightChargeSeconds, bowAttackDefaults, bowWeaponStats));
        bowAttackActive = true;
        bowStateEntered = bowUsesInput = bowButtonHeld = false;
        ClearAttackTriggers();
        anim.SetFloat(BowTimeHash, BowAnimationFrame / BowLastFrame);
        anim.SetBool(BowFinishedHash, false);
        anim.SetTrigger(LightAttackHash);
        LogBow("Draw started.");
        return true;
    }

    /// <summary>Mobile PointerDown. Pair with EndLightAttackHold on PointerUp/cancel.</summary>
    public void BeginLightAttackHold()
    {
        if (TryAttack(CombatAttackInput.LightAttack) && bowAttackActive) bowButtonHeld = true;
    }

    public void EndLightAttackHold()
    {
        bowButtonHeld = false;
        if (bowAttackActive) bowTimeline.RequestRelease();
    }

    public void NotifyBowAttackEntered()
    {
        // Ignore direct Animator triggers without an accepted gameplay request.
        if (!bowAttackActive || !isActiveAndEnabled)
        {
            anim.SetBool(BowFinishedHash, true);
            return;
        }
        bowStateEntered = true;
        EquipmentPhase = PlayerEquipmentPhase.None;
        ActionState = PlayerActionState.Attack;
        AttackSubstate = PlayerAttackSubstate.Bow;
        SetActiveStatePath("Attack > Bow > Draw");
    }

    private void UpdateBowAttack()
    {
        anim.SetInteger(CombatModeHash, (int)PlayerCombatMode.Bow);
        anim.ResetTrigger(ParryHash);
        if (bowStateEntered) ClearAttackTriggers();
        bool held = bowUsesInput
            ? movementInput != null && movementInput.isActiveAndEnabled && movementInput.inputIsActive &&
                lightAttackAction != null && lightAttackAction.enabled && lightAttackAction.IsPressed()
            : bowButtonHeld;
        float dt = anim.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        AdvanceBowAttack(dt, held);
    }

    private void AdvanceBowAttack(float dt, bool held)
    {
        bowTimeline.UpdateCharge(dt, held);
        if (!bowStateEntered) return;

        var previous = BowPhase;
        bowTimeline.Advance(dt * Mathf.Max(0f, anim.speed) * bowCapturedBaseSpeed *
            bowCharacterClip.frameRate * bowWeaponStats.AgilityMultiplier, BowLastFrame, bowCapturedDrawSpeed);
        anim.SetFloat(BowTimeHash, BowAnimationFrame / BowLastFrame);
        anim.SetBool(BowFinishedHash, BowPhase == BowAttackPhase.Finished);
        if (previous != BowPhase)
        {
            SetActiveStatePath("Attack > Bow > " + BowPhase);
            LogBow(BowPhase.ToString());
            if ((previous == BowAttackPhase.Draw || previous == BowAttackPhase.Hold) &&
                (BowPhase == BowAttackPhase.Release || BowPhase == BowAttackPhase.Finished))
                BowReleased?.Invoke();
        }
    }

    private void ResetBowAttack()
    {
        if (bowAttackActive && bowTimeline.Phase == BowAttackPhase.Finished && bowAttackDefaults != null)
            attackRecoveryUntil = System.Math.Max(attackRecoveryUntil, AttackClock + bowWeaponStats.Duration(bowAttackDefaults.recoverySeconds));
        bowAttackActive = bowStateEntered = bowUsesInput = bowButtonHeld = false;
        bowTimeline.Reset();
        if (!hasBowControl || anim == null) return;
        anim.SetBool(BowFinishedHash, true);
        anim.SetFloat(BowTimeHash, BowAttackTimeline.DrawFrame / BowLastFrame);
    }

    private void LogBow(string message)
    {
        if (logBowEvents) Debug.Log($"[Bow] {name}: {message}", this);
    }
}
