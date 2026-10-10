using UnityEngine;

public partial class PlayerStateManager
{
    public const string SwordHeavyTimeParameter = "SwordHeavyTime";
    public const string SwordHeavyFinishedParameter = "SwordHeavyFinished";
    private readonly SwordHeavyAttackTimeline swordHeavyTimeline = new SwordHeavyAttackTimeline();
    private bool swordHeavyActive, swordHeavyEntered, swordHeavyHeld, swordHeavyUsesInput;
    private CombatAttackStep swordHeavyStep;
    private SwordHeavyChargeSettings swordHeavySettings;
    private ElementalGems.GemSlashSettings swordHeavySlash;
    private SwordHeavyDamagePhase swordHeavyDamagePhase;
    private int swordHeavyWindowId;
    private int swordHeavyStateHash;
    private double swordHeavyLastClock;
    private float swordHeavyChargeStageDuration = 2f;

    public bool IsSwordHeavyAttacking => swordHeavyActive;
    public bool IsRepeatingSwordHeavyAttack => swordHeavyActive && swordHeavyTimeline.RepeatingLow;
    public int SwordHeavyLowPass => swordHeavyTimeline.LowPass;
    public SwordHeavyDamagePhase CurrentSwordHeavyDamagePhase => swordHeavyActive ? swordHeavyTimeline.DamagePhase : SwordHeavyDamagePhase.None;
    public float SwordHeavyAnimationFrame => swordHeavyTimeline.Frame;

    private void PrepareSwordHeavyAttack(CombatAttackConfiguration config)
    {
        var sourceStep = config.heavyAttackChain[0];
        var defaults = CaptureAttackDefaults(PlayerCombatMode.Sword,CombatAttackInput.HeavyAttack);
        swordHeavyChargeStageDuration = ChargeDuration(swordHeavyChargeSecondsPerStage, defaults,
            CaptureWeaponStats(PlayerCombatMode.Sword, CombatAttackInput.HeavyAttack));
        swordHeavyStep = new CombatAttackStep { animation=sourceStep.animation,slash=sourceStep.slash,
            playbackSpeed=defaults.speed,postAttackRecovery=defaults.recoverySeconds };
        // Capture both windows so changing Inspector values cannot retime a running attack.
        swordHeavySettings = JsonUtility.FromJson<SwordHeavyChargeSettings>(JsonUtility.ToJson(config.swordHeavyCharge));
        swordHeavySettings.baseKnockback = Mathf.Max(0,WeaponStatModifier.Finite(swordHeavyBaseKnockback));
        swordHeavySettings.heldLowPasses = Mathf.Max(1,swordHeavyHeldLowPasses);
        swordHeavySettings.lowDamageHeavyAttack.damageMultiplier = Mathf.Max(0,WeaponStatModifier.Finite(swordHeavyLowDamageMultiplier,1));
        swordHeavySettings.highDamageHeavyAttack.damageMultiplier = Mathf.Max(0,WeaponStatModifier.Finite(swordHeavyHighDamageMultiplier,1));
        swordHeavySettings.lowDamageHeavyAttack.knockbackMultiplier = Mathf.Max(0,WeaponStatModifier.Finite(swordHeavyLowPushMultiplier,1));
        swordHeavySettings.highDamageHeavyAttack.knockbackMultiplier = Mathf.Max(0,WeaponStatModifier.Finite(swordHeavyHighPushMultiplier,1));
        swordHeavyActive = swordHeavyHeld = true;
        swordHeavyEntered = swordHeavyUsesInput = false;
        swordHeavyLastClock = AttackClock;
        swordChargeVfxStarted = AttackClock;
        swordChargeVfxHeld = false;
        swordHeavyDamagePhase = SwordHeavyDamagePhase.None;
        swordHeavyWindowId = 0;
        swordHeavySlash = null;
        swordHeavyTimeline.Begin(swordHeavyStep.animation.length * swordHeavyStep.animation.frameRate, swordHeavySettings,
            swordHeavyChargeStageDuration);
        anim.SetFloat(SwordHeavyTimeParameter, 0f);
        anim.SetBool(SwordHeavyFinishedParameter, false);
    }

    public void EnterSwordHeavyAttack(int stateHash)
    {
        swordHeavyStateHash = stateHash;
        swordHeavyEntered = true;
        TickSwordHeavyEffects();
    }

    private void PollSwordHeavyRelease()
    {
        if (!swordHeavyUsesInput || !swordHeavyHeld) return;
        if (movementInput == null || !movementInput.isActiveAndEnabled || !movementInput.inputIsActive ||
            heavyAttackAction == null || !heavyAttackAction.enabled)
        {
            CancelSwordHeavyHold();
            return;
        }
        if (!heavyAttackAction.IsPressed()) EndSwordHeavyHold();
    }

    private void UpdateSwordHeavyAttack()
    {
        anim.SetInteger(CombatModeHash, (int)PlayerCombatMode.Sword);
        anim.ResetTrigger(ParryHash);
        if (!swordHeavyEntered) return;
        ClearAttackTriggers();
        double now = AttackClock;
        CaptureSwordChargeVfxHold(now);
        AdvanceSwordHeavyAnimation((float)System.Math.Max(0, now - swordHeavyLastClock) * Mathf.Max(0, anim.speed));
        swordHeavyLastClock = now;
    }

    private void AdvanceSwordHeavyAnimation(float delta)
    {
        swordHeavyTimeline.Advance(delta, swordHeavyStep.animation.frameRate * swordHeavyStep.playbackSpeed * activeWeaponStats.AgilityMultiplier);
        anim.SetFloat(SwordHeavyTimeParameter, swordHeavyTimeline.Frame / (swordHeavyStep.animation.length * swordHeavyStep.animation.frameRate));
        anim.SetBool(SwordHeavyFinishedParameter, swordHeavyTimeline.Finished);
        TickSwordHeavyEffects();
    }

    private void TickSwordHeavyEffects()
    {
        var effects = GetComponent<ElementalGems.GemLightSlashEffects>();
        var melee = GetComponent<ElementalGems.GemSwordCombat>();
        float normalized = SwordHeavyAnimationFrame / (swordHeavyStep.animation.length * swordHeavyStep.animation.frameRate);
        var phase = swordHeavyTimeline.DamagePhase;
        if (phase != swordHeavyDamagePhase || swordHeavyWindowId != swordHeavyTimeline.WindowId)
        {
            if (swordHeavySlash != null)
            {
                effects?.FinishWindow(swordHeavyStateHash, normalized);
                effects?.End(swordHeavyStateHash);
                melee?.End(swordHeavyStateHash);
            }
            swordHeavyDamagePhase = phase;
            swordHeavyWindowId = swordHeavyTimeline.WindowId;
            swordHeavySlash = null;
            if (phase != SwordHeavyDamagePhase.None)
            {
                var damage = phase == SwordHeavyDamagePhase.Low ? swordHeavySettings.lowDamageHeavyAttack : swordHeavySettings.highDamageHeavyAttack;
                swordHeavySlash = damage.slash;
                if (phase == SwordHeavyDamagePhase.High && SwordChargeVfxAllowed)
                    SwordChargeReleased?.Invoke(Mathf.Max(1, swordHeavyTimeline.LowPass + 1));
                // Begin resets the hit set for each low pass and the final high slash.
                melee?.Begin(swordHeavyStateHash, CombatAttackInput.HeavyAttack, 1, false,
                    damage.damageMultiplier, damage.knockbackMultiplier, swordHeavySettings.baseKnockback);
                effects?.Begin(swordHeavyStateHash, swordHeavyStep.animation, swordHeavySlash);
            }
        }
        if (swordHeavySlash != null) effects?.Tick(swordHeavyStateHash, normalized);
    }

    private void EndSwordHeavyHold()
    {
        if (!swordHeavyHeld) return;
        CaptureSwordChargeVfxHold(AttackClock);
        if (swordHeavyActive && swordHeavyEntered) UpdateSwordHeavyAttack();
        swordHeavyHeld = false;
        // Compare the original hold against the speed-adjusted durations captured at press.
        if (swordHeavyActive)
            swordHeavyTimeline.Release(System.Math.Max(0, AttackClock - swordChargeVfxStarted));
    }

    // Release before the frame-12 decision selects a tap. After it, the accepted sequence finishes.
    private void CancelSwordHeavyHold()
    {
        EndSwordHeavyHold();
        swordHeavyUsesInput = false;
    }

    public void ExitSwordHeavyAttack()
    {
        var effects = GetComponent<ElementalGems.GemLightSlashEffects>();
        if (swordHeavyStep != null)
            effects?.FinishWindow(swordHeavyStateHash, SwordHeavyAnimationFrame / (swordHeavyStep.animation.length * swordHeavyStep.animation.frameRate));
        effects?.End(swordHeavyStateHash);
        GetComponent<ElementalGems.GemSwordCombat>()?.End(swordHeavyStateHash);
        if (swordHeavyTimeline.Finished && swordHeavyStep != null)
            attackRecoveryUntil = System.Math.Max(attackRecoveryUntil, AttackClock + swordHeavyStep.postAttackRecovery / activeWeaponStats.AgilityMultiplier);
        ResetSwordHeavyAttack();
    }

    private void ResetSwordHeavyAttack()
    {
        if (swordHeavyActive)
        {
            GetComponent<ElementalGems.GemLightSlashEffects>()?.End(swordHeavyStateHash);
            GetComponent<ElementalGems.GemSwordCombat>()?.End(swordHeavyStateHash);
            if (anim != null) anim.SetBool(SwordHeavyFinishedParameter, true);
        }
        swordHeavyActive = swordHeavyEntered = false;
        swordChargeVfxHeld = false;
        swordHeavySlash = null;
        swordHeavyDamagePhase = SwordHeavyDamagePhase.None;
        // Keep Held/UsesInput until physical release; a long hold must not repeat the whole attack.
    }
}
