using System;
using UnityEngine;

public enum BowSpecialDamagePhase { None, Low, High }

[Serializable]
public sealed class BowSpecialAttackSettings
{
    [Tooltip("Each automatic shot uses Bow Light base damage times this value, with Special equipment bonuses.")]
    [Min(0)] public float lowDamageMultiplier = 1;
    [Tooltip("Final wave uses Bow Special base damage times this value.")]
    [Min(0)] public float highDamageMultiplier = 2;
    [Min(0)] public float baseKnockback = 5;
    [Min(0)] public float lowKnockbackMultiplier = 1, highKnockbackMultiplier = 1.5f;
    [Tooltip("Low special auto-target radius and high wave travel distance, in metres.")]
    [Min(.1f)] public float range = 22;
    [Min(.1f)] public float waveSpeed = 32;
    [Tooltip("Full width of the trailing edge of the pointed wave, in metres.")]
    [Min(.1f)] public float width = 6;
    [Min(.1f)] public float length = 4, height = 2;
    [Tooltip("Extra width per Bow weapon upgrade level. Set to zero to disable automatic widening.")]
    [Min(0)] public float widthPercentPerUpgrade = 5;
    [Tooltip("Additional multiplier for future skill-area upgrades. Changes apply to the next special.")]
    [Min(.1f)] public float areaWidthMultiplier = 1;

    [Header("Special Wave Visuals")]
    [Tooltip("Brightness of the high-shot core, glow, ribbons and particles.")]
    [Range(.25f,3)] public float vfxIntensity = 1.25f;
    [Tooltip("Density of sparks, splinters and wisps. Particle budgets remain capped.")]
    [Range(.25f,2)] public float vfxParticleDensity = 1;
    [Tooltip("Opacity of the broad layered front. Its size follows the upgraded damage width.")]
    [Range(.25f,2)] public float vfxFrontFill = 1;

    [Tooltip("Visual front size relative to the upgraded attack area. Does not alter damage reach.")]
    [Range(.3f,1.2f)] public float vfxFrontScale = .7f;
    [Tooltip("Thickness of the plasma trail, glow, braided ribbons and wake particles.")]
    [Range(.5f,2)] public float vfxTrailWidth = 1;

    public BowSpecialAttackSettings Snapshot(int upgradeLevel)
    {
        var copy = (BowSpecialAttackSettings)MemberwiseClone();
        copy.lowDamageMultiplier = Safe(lowDamageMultiplier, 1, 0, 100);
        copy.highDamageMultiplier = Safe(highDamageMultiplier, 2, 0, 100);
        copy.baseKnockback = Safe(baseKnockback, 5, 0, 100);
        copy.lowKnockbackMultiplier = Safe(lowKnockbackMultiplier, 1, 0, 10);
        copy.highKnockbackMultiplier = Safe(highKnockbackMultiplier, 1.5f, 0, 10);
        copy.range = Safe(range, 22, .1f, 200);
        copy.waveSpeed = Safe(waveSpeed, 32, .1f, 200);
        copy.width = Safe(width, 6, .1f, 100) * Safe(areaWidthMultiplier, 1, .1f, 10) *
            (1 + Mathf.Clamp(upgradeLevel, 0, 100) * Safe(widthPercentPerUpgrade, 5, 0, 100) / 100);
        copy.width = Mathf.Min(copy.width, 100);
        copy.length = Safe(length, 4, .1f, 50);
        copy.height = Safe(height, 2, .1f, 20);
        copy.vfxIntensity = Safe(vfxIntensity, 1.25f, .25f, 3);
        copy.vfxParticleDensity = Safe(vfxParticleDensity, 1, .25f, 2);
        copy.vfxFrontFill = Safe(vfxFrontFill, 1, .25f, 2);
        copy.vfxFrontScale = Safe(vfxFrontScale, .7f, .3f, 1.2f);
        copy.vfxTrailWidth = Safe(vfxTrailWidth, 1, .5f, 2);
        return copy;
    }
    static float Safe(float value, float fallback, float min, float max) =>
        Mathf.Clamp(WeaponStatModifier.Finite(value, fallback), min, max);
}

/// <summary>Stops on each release pose, retaining excess frame time for the next update.</summary>
public sealed class BowSpecialAttackTimeline
{
    public static readonly int[] ReleaseFrames = { 17, 27, 35, 63 };
    public float Frame { get; private set; }
    public int Shots { get; private set; }
    public bool Finished { get; private set; }
    float debt;
    public void Reset() { Frame = debt = 0; Shots = 0; Finished = false; }
    public bool Advance(float frames, float lastFrame)
    {
        if (Finished || frames <= 0) return false;
        debt += Mathf.Max(0, frames);
        float target = Shots < ReleaseFrames.Length ? ReleaseFrames[Shots] : lastFrame;
        float step = Mathf.Min(debt, Mathf.Max(0, target - Frame));
        Frame += step; debt -= step;
        if (Frame + .0001f < target) return false;
        Frame = target;
        if (Shots < ReleaseFrames.Length) { Shots++; return true; }
        Finished = true;
        return false;
    }
}
