using UnityEngine;

public partial class PlayerStateManager
{
    public event System.Action<int> SwordChargeReleased;
    public const float ChargeVfxHoldDelay = .2f;
    private double swordChargeVfxStarted;
    private bool swordChargeVfxHeld, bowHeavyChargeVfxHeld;
    public bool BowChargeVfxAllowed => bowHeavyActive
        ? bowHeavyChargeVfxHeld || (bowHeavyCharging && bowHeavyCycle.Held &&
            bowHeavyCycle.Elapsed(BowHeavyClock) >= ChargeVfxHoldDelay)
        : bowAttackActive && bowTimeline.HeldSeconds >= ChargeVfxHoldDelay;
    public bool SwordChargeVfxAllowed => swordHeavyActive && (swordChargeVfxHeld ||
        (swordHeavyHeld && AttackClock - swordChargeVfxStarted >= ChargeVfxHoldDelay));
    private void CaptureSwordChargeVfxHold(double now)
    {
        if (swordHeavyActive && swordHeavyHeld && now - swordChargeVfxStarted >= ChargeVfxHoldDelay)
            swordChargeVfxHeld = true;
    }
    public float BowLightChargeProgress => bowTimeline.ChargeProgress;
    public float BowHeavyChargeProgress => bowHeavyCycle.Progress(BowHeavyClock, bowHeavyChargeDuration);
    public float BowLightHoldSeconds => bowTimeline.HeldSeconds;
    public bool IsBowLightChargingVisual => bowAttackActive && bowTimeline.IsCharging;
    public bool TryGetTimedChargeSeconds(out float seconds)
    {
        seconds = 0;
        if (IsBowLightChargingVisual) { seconds = bowTimeline.HeldSeconds; return true; }
        if (bowHeavyActive && bowHeavyCharging && bowHeavyCycle.Held)
        { seconds = bowHeavyCycle.Elapsed(BowHeavyClock); return true; }
        if (swordHeavyActive && swordHeavyHeld)
        { seconds = (float)System.Math.Max(0, AttackClock - swordChargeVfxStarted); return true; }
        return false;
    }

    // The aura uses a fixed visual scale of two seconds per marker. Map gameplay
    // progress onto that scale so configurable times and equipment SPD also retime the VFX.
    public bool TryGetChargeVisualSeconds(out float seconds)
    {
        if (!TryGetTimedChargeSeconds(out seconds)) return false;
        if (swordHeavyActive)
            seconds = seconds / swordHeavyChargeStageDuration * ElementalGems.GemChargeAura.SecondsPerStage;
        else if (bowHeavyActive)
            seconds = BowHeavyChargeProgress * ElementalGems.GemChargeAura.SecondsPerStage;
        return true;
    }

    // Only a physically held charge displays an aura. Released attack animations never recharge it.
    public bool TryGetChargeVisual(out int stage, out float progress)
    {
        stage = 1; progress = 0;
        if (!TryGetTimedChargeSeconds(out float seconds) || seconds < ChargeVfxHoldDelay) return false;
        if (IsBowLightChargingVisual) progress = bowTimeline.ChargeProgress;
        else if (bowHeavyActive && bowHeavyCharging) progress = BowHeavyChargeProgress;
        else if (TryGetChargeVisualSeconds(out float visualSeconds))
            ElementalGems.GemChargeAura.TimedSize(visualSeconds, out stage, out progress);
        return true;
    }
}
