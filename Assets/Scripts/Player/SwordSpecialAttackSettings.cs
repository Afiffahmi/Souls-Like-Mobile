using System;
using System.Collections.Generic;
using UnityEngine;
using ElementalGems;

[Serializable]
public sealed class SwordSpecialAttackStep
{
    public AnimationClip animation;
    public GemSlashSettings slash = new GemSlashSettings { enabled = true };
    [Tooltip("Optional second visual sweep. Shares this hit's damage set, so the final two-part slash hits once.")]
    public GemSlashSettings followThrough = new GemSlashSettings();
    public bool IsValid => animation != null && animation.length > 0 && !animation.isLooping &&
        slash != null && slash.enabled && slash.IsValid(animation) &&
        (followThrough == null || !followThrough.enabled ||
         (followThrough.IsValid(animation) && followThrough.startFrame >= slash.endFrame));
}

[Serializable]
public sealed class SwordSpecialAttackSettings
{
    public const int HitCount = 5;
    public const float SecondsPerHit = 1f;
    public const float CycleSeconds = HitCount * SecondsPerHit;
    public bool enabled;
    [Min(1)] public float targetRadius = 12;
    [Min(.1f)] public float arrivalDistance = 1.5f;
    [Tooltip("Base travel time per dash. Attack-speed bonuses shorten this along with the hit.")]
    [Range(.01f, .15f)] public float dashSeconds = .05f;
    [Min(0)] public float baseKnockback = 5f;
    public List<SwordSpecialAttackStep> steps = new List<SwordSpecialAttackStep>();
    public bool IsValid => steps != null && steps.Count == HitCount && steps.TrueForAll(s => s != null && s.IsValid) &&
        Finite(targetRadius) && targetRadius > 0 && Finite(arrivalDistance) && arrivalDistance > 0 &&
        Finite(dashSeconds) && dashSeconds > 0 && Finite(baseKnockback) && baseKnockback > 0;
    static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}
