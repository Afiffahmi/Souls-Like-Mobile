using UnityEngine;

public partial class PlayerStateManager
{
    public const string BowSpecialLowPath = "Base Layer.Attack.Bow_SpecialAttack_Low";
    public const string BowSpecialHighPath = "Base Layer.Attack.Bow_SpecialAttack_High";
    public const string BowSpecialTimeParameter = "BowSpecialTime";
    public BowSpecialAttackSettings bowSpecial = new BowSpecialAttackSettings();
    readonly BowSpecialAttackTimeline bowSpecialTimeline = new BowSpecialAttackTimeline();
    AnimationClip bowSpecialClip;
    bool hasBowSpecialControl, bowSpecialActive, bowSpecialEntered, bowSpecialHigh;
    double bowSpecialCooldownUntil;
    int bowSpecialSequence;
    AttackDefaultSettings bowSpecialDefaults;
    WeaponAttackStats bowSpecialStats;
    BowSpecialAttackSettings bowSpecialSnapshot;
    float bowSpecialLowDamage;
    public bool IsBowSpecialAttacking => bowSpecialActive;
    public int BowSpecialSequence => bowSpecialSequence;
    public float BowSpecialAnimationFrame => bowSpecialTimeline.Frame;
    public int BowSpecialShots => bowSpecialTimeline.Shots;
    public BowSpecialDamagePhase CurrentBowSpecialDamagePhase => !bowSpecialActive ? BowSpecialDamagePhase.None :
        bowSpecialHigh ? BowSpecialDamagePhase.High : BowSpecialDamagePhase.Low;
    public BowSpecialAttackSettings CurrentBowSpecialSettings => bowSpecialSnapshot;
    public float CurrentBowSpecialDamage => bowSpecialHigh ? bowSpecialStats.Damage(bowSpecialDefaults.damage) * bowSpecialSnapshot.highDamageMultiplier : bowSpecialLowDamage;

    void InitializeBowSpecialAttack()
    {
        foreach (var state in anim.GetBehaviours<CombatBowSpecialAttackState>())
            if (state.animation != null) bowSpecialClip = state.animation;
        bool time = false;
        foreach (var p in anim.parameters)
            time |= p.name == BowSpecialTimeParameter && p.type == AnimatorControllerParameterType.Float;
        hasBowSpecialControl = time && bowSpecialClip != null && !bowSpecialClip.isLooping && bowSpecialClip.frameRate > 0 &&
            bowSpecialClip.length * bowSpecialClip.frameRate > 63 &&
            anim.HasState(0, Animator.StringToHash(BowSpecialLowPath)) && anim.HasState(0, Animator.StringToHash(BowSpecialHighPath));
    }
    public int BowSpecialUpgradeLevel
    {
        get {
            var equipment = GetComponent<PlayerWeaponEquipment>();
            var loadout = equipment != null && equipment.isActiveAndEnabled ? equipment.bow : null;
            return loadout?.definition != null && loadout.definition.weapon == PlayerCombatMode.Bow && loadout.definition.IsValid
                ? Mathf.Clamp(loadout.upgradeLevel, 0, loadout.definition.maxUpgradeLevel) : 0;
        }
    }
    bool TryBeginBowSpecialAttack()
    {
        var shooter = GetComponent<PlayerBowShooter>();
        if (!hasBowSpecialControl || AttackClock < bowSpecialCooldownUntil || shooter == null || !shooter.isActiveAndEnabled || shooter.arrowPrefab == null) return false;
        bowSpecialDefaults = CaptureAttackDefaults(PlayerCombatMode.Bow, CombatAttackInput.SpecialAttack);
        bowSpecialStats = CaptureWeaponStats(PlayerCombatMode.Bow, CombatAttackInput.SpecialAttack);
        bowSpecialSnapshot = (bowSpecial ?? new BowSpecialAttackSettings()).Snapshot(BowSpecialUpgradeLevel);
        bowSpecialLowDamage = bowSpecialStats.Damage(CaptureAttackDefaults(PlayerCombatMode.Bow, CombatAttackInput.LightAttack).damage) * bowSpecialSnapshot.lowDamageMultiplier;
        bowSpecialCooldownUntil = AttackClock + bowSpecialDefaults.cooldownSeconds;
        bowSpecialSequence++;
        bowSpecialActive = true;
        bowSpecialEntered = bowSpecialHigh = false;
        bowSpecialTimeline.Reset();
        ClearAttackTriggers();
        anim.ResetTrigger(ParryHash);
        anim.SetFloat(BowSpecialTimeParameter, 0);
        anim.Play(BowSpecialLowPath, 0, 0);
        return true;
    }
    public bool NotifyBowSpecialEntered(bool high)
    {
        if (!isActiveAndEnabled || !bowSpecialActive || high != bowSpecialHigh) return false;
        bowSpecialEntered = true;
        EquipmentPhase = PlayerEquipmentPhase.None;
        ActionState = PlayerActionState.Attack;
        AttackSubstate = PlayerAttackSubstate.Bow;
        SetActiveStatePath(high ? "Attack > Bow > Special > High damage" : "Attack > Bow > Special > Low damage");
        return true;
    }
    public void NotifyBowSpecialExited(bool high)
    {
        // The low state's normal exit is the handoff to the high phase.
        if (bowSpecialActive && high == bowSpecialHigh) ResetBowSpecialAttack();
    }
    void UpdateBowSpecialAttack()
    {
        anim.SetInteger(CombatModeHash, (int)PlayerCombatMode.Bow);
        anim.ResetTrigger(ParryHash);
        ClearAttackTriggers();
        if (!bowSpecialEntered) return;
        AdvanceBowSpecialAnimation(anim.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
    }
    void AdvanceBowSpecialAnimation(float dt)
    {
        if (!bowSpecialActive || !bowSpecialEntered) return;
        // Change states only after the third low arrow has spawned in LateUpdate.
        if (!bowSpecialHigh && bowSpecialTimeline.Shots == 3)
        {
            bowSpecialHigh = true;
            bowSpecialEntered = false;
            anim.Play(BowSpecialHighPath, 0, 0);
            return;
        }
        bool release = bowSpecialTimeline.Advance(dt * Mathf.Max(0, anim.speed) * bowSpecialDefaults.speed *
            bowSpecialStats.AgilityMultiplier * bowSpecialClip.frameRate, bowSpecialClip.length * bowSpecialClip.frameRate);
        anim.SetFloat(BowSpecialTimeParameter, BowSpecialAnimationFrame / (bowSpecialClip.length * bowSpecialClip.frameRate));
        if (release) BowReleased?.Invoke();
        if (bowSpecialTimeline.Finished)
        {
            attackRecoveryUntil = System.Math.Max(attackRecoveryUntil, AttackClock + bowSpecialStats.Duration(bowSpecialDefaults.recoverySeconds));
            ResetBowSpecialAttack();
            anim.Play("Base Layer.Attack.Bow", 0, 0);
        }
    }
    void ResetBowSpecialAttack()
    {
        bowSpecialActive = bowSpecialEntered = bowSpecialHigh = false;
        bowSpecialTimeline.Reset();
        // Cooldown survives interruption and disabling the player.
    }
}
