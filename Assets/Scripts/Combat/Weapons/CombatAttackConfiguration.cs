using System;
using System.Collections.Generic;
using UnityEngine;

public enum CombatAttackInput { LightAttack, HeavyAttack, SpecialAttack }

/// <summary>Tracks animation steps within one sword light combo, never accumulated enemy hits.</summary>
public sealed class SwordLightComboProgress
{
    public int Step { get; private set; }
    public bool IsFinisher => Step == 3;
    public void Enter(int stepIndex, bool continuingSameCombo)
    {
        Step = stepIndex == 0 ? 1 : continuingSameCombo && Step == stepIndex ? stepIndex + 1 : 0;
    }
    public void Reset() => Step = 0;
}

[Serializable]
public sealed class CombatAttackStep
{
    public AnimationClip animation;
    [Tooltip("Optional elemental slash: frame window, direction, placement and sweep. Changes apply without rebuilding the Animator.")]
    public ElementalGems.GemSlashSettings slash = new ElementalGems.GemSlashSettings();
    [Min(0.01f)] public float playbackSpeed = 1f;
    [Tooltip("Normalized animation time at which a follow-up press starts being accepted.")]
    [Range(0f, 1f)] public float comboWindowStart = 0.25f;
    [Range(0f, 1f)] public float comboWindowEnd = 0.65f;
    [Tooltip("A buffered follow-up starts here. Must be at or after the window end.")]
    [Range(0f, 1f)] public float chainTransitionTime = 0.85f;
    [Tooltip("Use an exact clip frame for the earliest follow-up. A press after that frame chains immediately, until Combo Window End.")]
    public bool useChainTransitionFrame;
    [Min(1)] public int chainTransitionFrame = 1;
    [Tooltip("Block new attacks for this many seconds after this step finishes its animation. Usually set only on the final step.")]
    [Min(0f)] public float postAttackRecovery;
    public float ChainTransitionNormalized => useChainTransitionFrame && animation != null
        ? chainTransitionFrame / (animation.length * animation.frameRate) : chainTransitionTime;

    public float WindupMultiplier(float normalizedTime, float normalAdvance, float requestedSpeed, float recoverySpeed = 1f)
    {
        if (slash == null || !slash.enabled || !slash.IsValid(animation)) return 1f;
        // Only accelerate recovery after the final slash frame; its damage window stays at normal speed.
        if (normalizedTime * animation.length * animation.frameRate >= slash.endFrame)
            return recoverySpeed;
        float remaining = slash.startFrame / animation.frameRate - normalizedTime * animation.length;
        if (remaining <= .0001f) return 1f;
        // Cap the final accelerated step at the slash boundary; retain normal playback after it.
        return Mathf.Min(requestedSpeed, normalAdvance > 0 ? Mathf.Max(1f, remaining / normalAdvance) : requestedSpeed);
    }
}

[Serializable]
public sealed class CombatSpecialAttack
{
    public AnimationClip animation;
    public ElementalGems.GemSlashSettings slash = new ElementalGems.GemSlashSettings();
    [Min(0.01f)] public float playbackSpeed = 1f;
    [Min(0f)] public float cooldown = 5f;
}

[CreateAssetMenu(menuName = "Combat/Attack Configuration", fileName = "AttackConfiguration")]
public sealed class CombatAttackConfiguration : ScriptableObject
{
    public PlayerCombatMode weapon = PlayerCombatMode.Sword;
    public RuntimeAnimatorController animatorController;
    [Tooltip("Full path to the existing combat parent state machine.")]
    public string combatStateMachinePath = "Base Layer.Attack";
    [Tooltip("Sword light-combo follow-ups play faster before their slash Start Frame, then return to the step's normal Playback Speed. 1 disables fast-forward.")]
    [Range(1f, 8f)] public float lightComboWindupSpeed = 3f;
    [Tooltip("All sword light attacks: animation speed after Slash End Frame until the clip finishes or chains. 1 = original speed, 2.5 = faster recovery. Applies immediately without rebuilding the Animator. Post Attack Recovery adds a separate delay AFTER the animation.")]
    [InspectorName("Light Attack Recovery Speed"), Range(1f, 8f)] public float lightComboFinisherRecoverySpeed = 2.5f;
    public List<CombatAttackStep> lightAttackChain = new List<CombatAttackStep>();
    public List<CombatAttackStep> heavyAttackChain = new List<CombatAttackStep>();
    [InspectorName("Sword Heavy Attack")]
    public SwordHeavyChargeSettings swordHeavyCharge = new SwordHeavyChargeSettings();
    public bool UsesSwordHeavyCharge => weapon == PlayerCombatMode.Sword && swordHeavyCharge != null && swordHeavyCharge.enabled && heavyAttackChain != null && heavyAttackChain.Count > 0;
    public CombatSpecialAttack specialAttack = new CombatSpecialAttack();
    public SwordSpecialAttackSettings swordSpecial = new SwordSpecialAttackSettings();
    public bool UsesSwordSpecial => weapon == PlayerCombatMode.Sword && swordSpecial != null && swordSpecial.enabled;

    public string AttackMachineName => weapon + "_Attacks";
    public string LocomotionStatePath => combatStateMachinePath + "." + weapon;
    public string StateName(CombatAttackInput input, int index) => weapon + "_" + input +
        (input == CombatAttackInput.SpecialAttack && (!UsesSwordSpecial || index == 0) ? "" : "_" + (index + 1));
    public string StatePath(CombatAttackInput input, int index) => combatStateMachinePath + "." + AttackMachineName + "." + StateName(input, index);
    public List<CombatAttackStep> Chain(CombatAttackInput input) => input == CombatAttackInput.LightAttack
        ? lightAttackChain : input == CombatAttackInput.HeavyAttack ? heavyAttackChain : null;

    public bool Validate(out string error)
    {
        error = null;
        if (weapon == PlayerCombatMode.Normal || animatorController == null || string.IsNullOrWhiteSpace(combatStateMachinePath))
            error = "Assign a combat weapon, Animator Controller, and combat parent path.";
        else if (UsesSwordSpecial && !swordSpecial.IsValid)
            error = "Sword special needs exactly five non-looping clips with valid slash windows and positive target radius, arrival distance and knockback.";
        else if (lightAttackChain == null || heavyAttackChain == null)
            error = "Attack chains must be lists (an empty list disables that attack input).";
        else if (UsesSwordHeavyCharge && (heavyAttackChain.Count != 1 || !swordHeavyCharge.IsValid(heavyAttackChain[0])))
            error = "Sword heavy needs one clip, Hold Check before the low slash, at least one held low pass, and valid separate low/high damage windows in order.";
        else if (!Finite(lightComboWindupSpeed) || lightComboWindupSpeed < 1f || lightComboWindupSpeed > 8f)
            error = "Light Combo Windup Speed must be between 1 and 8.";
        else if (!Finite(lightComboFinisherRecoverySpeed) || lightComboFinisherRecoverySpeed < 1f || lightComboFinisherRecoverySpeed > 8f)
            error = "Light Attack Recovery Speed must be between 1 and 8.";
        else
        {
            foreach (var chain in new [] { lightAttackChain, heavyAttackChain })
            foreach (var step in chain)
            {
                if (step == null || step.animation == null || step.animation.length <= 0f || step.animation.isLooping ||
                    !Finite(step.playbackSpeed) || step.playbackSpeed <= 0f || !Finite(step.comboWindowStart) ||
                    !Finite(step.comboWindowEnd) || !Finite(step.chainTransitionTime) ||
                    step.comboWindowStart < 0f || step.comboWindowStart >= step.comboWindowEnd ||
                    step.comboWindowEnd > 1f || step.chainTransitionTime > 1f ||
                    (!step.useChainTransitionFrame && step.comboWindowEnd > step.chainTransitionTime) ||
                    (step.useChainTransitionFrame && (step.chainTransitionFrame < 1 || AnimationFrameInvalid(step))) ||
                    !Finite(step.postAttackRecovery) || step.postAttackRecovery < 0f)
                { error = "Each step needs a non-looping clip, positive speed, 0 <= window start < window end <= 1, a valid chain point/frame, and nonnegative recovery. In normalized mode, window end must not exceed the chain point."; return false; }
            }
            if (specialAttack == null || specialAttack.animation == null || specialAttack.animation.length <= 0f ||
                specialAttack.animation.isLooping || !Finite(specialAttack.playbackSpeed) || specialAttack.playbackSpeed <= 0f ||
                !Finite(specialAttack.cooldown) || specialAttack.cooldown < 0f)
                error = "Special needs a non-looping clip, positive speed, and nonnegative cooldown.";
        }
        return error == null;
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool AnimationFrameInvalid(CombatAttackStep step) => !Finite(step.animation.frameRate) ||
        step.animation.frameRate <= 0 || step.chainTransitionFrame > step.animation.length * step.animation.frameRate;
}
