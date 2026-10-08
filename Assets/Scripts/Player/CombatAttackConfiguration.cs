using System;
using System.Collections.Generic;
using UnityEngine;

public enum CombatAttackInput { LightAttack, HeavyAttack, SpecialAttack }

[Serializable]
public sealed class CombatAttackStep
{
    public AnimationClip animation;
    [Min(0.01f)] public float playbackSpeed = 1f;
    [Tooltip("Normalized animation time at which a follow-up press starts being accepted.")]
    [Range(0f, 1f)] public float comboWindowStart = 0.25f;
    [Range(0f, 1f)] public float comboWindowEnd = 0.65f;
    [Tooltip("A buffered follow-up starts here. Must be at or after the window end.")]
    [Range(0f, 1f)] public float chainTransitionTime = 0.85f;
}

[Serializable]
public sealed class CombatSpecialAttack
{
    public AnimationClip animation;
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
    public List<CombatAttackStep> lightAttackChain = new List<CombatAttackStep>();
    public List<CombatAttackStep> heavyAttackChain = new List<CombatAttackStep>();
    public CombatSpecialAttack specialAttack = new CombatSpecialAttack();

    public string AttackMachineName => weapon + "_Attacks";
    public string LocomotionStatePath => combatStateMachinePath + "." + weapon;
    public string StateName(CombatAttackInput input, int index) => weapon + "_" + input +
        (input == CombatAttackInput.SpecialAttack ? "" : "_" + (index + 1));
    public string StatePath(CombatAttackInput input, int index) => combatStateMachinePath + "." + AttackMachineName + "." + StateName(input, index);
    public List<CombatAttackStep> Chain(CombatAttackInput input) => input == CombatAttackInput.LightAttack
        ? lightAttackChain : input == CombatAttackInput.HeavyAttack ? heavyAttackChain : null;

    public bool Validate(out string error)
    {
        error = null;
        if (weapon == PlayerCombatMode.Normal || animatorController == null || string.IsNullOrWhiteSpace(combatStateMachinePath))
            error = "Assign a combat weapon, Animator Controller, and combat parent path.";
        else if (lightAttackChain == null || heavyAttackChain == null)
            error = "Attack chains must be lists (an empty list disables that attack input).";
        else
        {
            foreach (var chain in new [] { lightAttackChain, heavyAttackChain })
            foreach (var step in chain)
            {
                if (step == null || step.animation == null || step.animation.length <= 0f || step.animation.isLooping ||
                    !Finite(step.playbackSpeed) || step.playbackSpeed <= 0f || !Finite(step.comboWindowStart) ||
                    !Finite(step.comboWindowEnd) || !Finite(step.chainTransitionTime) ||
                    step.comboWindowStart < 0f || step.comboWindowStart >= step.comboWindowEnd ||
                    step.comboWindowEnd > step.chainTransitionTime || step.chainTransitionTime > 1f)
                { error = "Each chain step needs a non-looping clip, positive speed, and 0 <= window start < window end <= chain point <= 1."; return false; }
            }
            if (specialAttack == null || specialAttack.animation == null || specialAttack.animation.length <= 0f ||
                specialAttack.animation.isLooping || !Finite(specialAttack.playbackSpeed) || specialAttack.playbackSpeed <= 0f ||
                !Finite(specialAttack.cooldown) || specialAttack.cooldown < 0f)
                error = "Special needs a non-looping clip, positive speed, and nonnegative cooldown.";
        }
        return error == null;
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
