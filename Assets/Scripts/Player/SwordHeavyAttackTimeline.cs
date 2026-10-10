using System;
using UnityEngine;

public enum SwordHeavyDamagePhase { None, Low, High }

[Serializable]
public sealed class SwordHeavyDamageSettings
{
    [Min(0)] public float damageMultiplier = 1f;
    [Tooltip("Multiplies existing knockback strength; weapon knockback duration bonuses still apply separately.")]
    [Min(0)] public float knockbackMultiplier = 1f;
    public ElementalGems.GemSlashSettings slash;
    public bool IsValid(AnimationClip clip) => Finite(damageMultiplier) && damageMultiplier >= 0 &&
        Finite(knockbackMultiplier) && knockbackMultiplier >= 0 && slash != null && slash.enabled && slash.IsValid(clip);
    static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}

[Serializable]
public sealed class SwordHeavyChargeSettings
{
    public bool enabled = true;
    [Tooltip("Heavy attacks always have this base push strength, even when the equipped gem has no knockback. Stronger gem knockback is preserved; low/high multipliers apply afterward.")]
    [Min(0)] public float baseKnockback = 5f;
    [Tooltip("Freeze at this frame while the original press is held. Release starts the held low-slash passes.")]
    [Min(1)] public int holdCheckFrame = 12;
    [Tooltip("Base low slash passes for a held attack. The second charge tier adds one pass; the third adds two. Charge timing is configured on Player State Manager. A tap always has one pass.")]
    [Min(1)] public int heldLowPasses = 2;
    [InspectorName("Low Damage Heavy Attack")]
    public SwordHeavyDamageSettings lowDamageHeavyAttack = new SwordHeavyDamageSettings {
        damageMultiplier = 1f, knockbackMultiplier = 1f,
        slash = new ElementalGems.GemSlashSettings { enabled = true, startFrame = 17, endFrame = 32,
            localEulerAngles = Vector3.zero, reverseSweep = true, fullCircle = true, localOffset = Vector3.up }
    };
    [InspectorName("High Damage Heavy Attack")]
    public SwordHeavyDamageSettings highDamageHeavyAttack = new SwordHeavyDamageSettings {
        damageMultiplier = 1.25f, knockbackMultiplier = 1.25f,
        slash = new ElementalGems.GemSlashSettings { enabled = true, startFrame = 48, endFrame = 56,
            localEulerAngles = new Vector3(0, 0, -90), reverseSweep = false }
    };
    public bool IsValid(CombatAttackStep step) => step != null && step.animation != null &&
        !float.IsNaN(baseKnockback) && !float.IsInfinity(baseKnockback) && baseKnockback >= 0 &&
        holdCheckFrame > 0 && heldLowPasses >= 1 && lowDamageHeavyAttack != null && highDamageHeavyAttack != null &&
        lowDamageHeavyAttack.IsValid(step.animation) && highDamageHeavyAttack.IsValid(step.animation) &&
        holdCheckFrame < lowDamageHeavyAttack.slash.startFrame &&
        lowDamageHeavyAttack.slash.endFrame < highDamageHeavyAttack.slash.startFrame;
}

/// <summary>Checks the original press at frame 12. Held: freeze there until release,
/// then play 17-32 for the held pass count, adding one at the second charge tier and two at the third.
/// Tap: play the complete clip once. Every slash
/// boundary is presented even during a hitch; remaining animation time is retained.</summary>
public sealed class SwordHeavyAttackTimeline
{
    enum Segment { HoldCheck, WaitForRelease, LowStart, LowEnd, RepeatLow, HighStart, HighEnd, Finish }
    public float Frame { get; private set; }
    public bool Held { get; private set; }
    public bool HoldDecided { get; private set; }
    public bool WaitingForRelease => segment == Segment.WaitForRelease && Held;
    public bool RepeatingLow { get; private set; }
    public bool Finished { get; private set; }
    public int LowPass { get; private set; }
    public int WindowId { get; private set; }
    public SwordHeavyDamagePhase DamagePhase { get; private set; }
    Segment segment;
    double debt;
    float endFrame, checkFrame, lowStart, lowEnd, highStart, highEnd;
    int heldPasses, totalPasses;
    float chargeStageDuration = 2f;

    public void Begin(float totalFrames, SwordHeavyChargeSettings settings, float secondsPerStage = 2f)
    {
        Reset(); Held = true;
        chargeStageDuration = Mathf.Max(.01f, WeaponStatModifier.Finite(secondsPerStage, 2f));
        endFrame = totalFrames; checkFrame = settings.holdCheckFrame; heldPasses = settings.heldLowPasses;
        lowStart = settings.lowDamageHeavyAttack.slash.startFrame; lowEnd = settings.lowDamageHeavyAttack.slash.endFrame;
        highStart = settings.highDamageHeavyAttack.slash.startFrame; highEnd = settings.highDamageHeavyAttack.slash.endFrame;
    }
    public void Release(double heldSeconds = 0)
    {
        if (!Held) return;
        Held = false;
        if (RepeatingLow)
            totalPasses = heldPasses + (heldSeconds >= chargeStageDuration * 3 ? 2 : heldSeconds >= chargeStageDuration * 2 ? 1 : 0);
    }

    public void Advance(float deltaSeconds, float framesPerSecond)
    {
        if (Finished) return;
        if (segment == Segment.WaitForRelease)
        {
            // Held time is not animation time. Never carry it into the released swing.
            debt = 0;
            if (Held) return;
            Frame = lowStart;
            StartLow();
            return;
        }
        debt += Math.Max(0, deltaSeconds) * Math.Max(0, framesPerSecond);
        switch (segment)
        {
            case Segment.HoldCheck:
                if (!Reach(checkFrame)) return;
                HoldDecided = true; RepeatingLow = Held; totalPasses = Held ? heldPasses : 1;
                segment = Segment.LowStart;
                if (Held) { segment = Segment.WaitForRelease; debt = 0; return; }
                goto case Segment.LowStart;
            case Segment.LowStart:
                if (!Reach(lowStart)) return;
                StartLow(); return;
            case Segment.LowEnd:
                if (!Reach(lowEnd)) return;
                DamagePhase = SwordHeavyDamagePhase.None;
                segment = LowPass < totalPasses ? Segment.RepeatLow : Segment.HighStart;
                return;
            case Segment.RepeatLow:
                Frame = lowStart; StartLow(); return;
            case Segment.HighStart:
                if (!Reach(highStart)) return;
                RepeatingLow = false; DamagePhase = SwordHeavyDamagePhase.High; WindowId++;
                segment = Segment.HighEnd; return;
            case Segment.HighEnd:
                if (!Reach(highEnd)) return;
                DamagePhase = SwordHeavyDamagePhase.None; segment = Segment.Finish; return;
            case Segment.Finish:
                if (!Reach(endFrame)) return;
                Finished = true; return;
        }
    }
    void StartLow()
    {
        LowPass++; WindowId++; DamagePhase = SwordHeavyDamagePhase.Low; segment = Segment.LowEnd;
    }
    bool Reach(float boundary)
    {
        double remaining = Math.Max(0, boundary - Frame);
        if (debt + .00001 < remaining) { Frame += (float)debt; debt = 0; return false; }
        debt = Math.Max(0, debt - remaining); Frame = boundary; return true;
    }
    public void Reset()
    {
        Frame = 0; LowPass = WindowId = 0; Held = HoldDecided = RepeatingLow = Finished = false;
        DamagePhase = SwordHeavyDamagePhase.None; segment = Segment.HoldCheck; debt = 0;
    }
}
