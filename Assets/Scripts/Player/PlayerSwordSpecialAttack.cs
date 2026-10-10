using System.Collections.Generic;
using UnityEngine;
using ElementalGems;

public partial class PlayerStateManager
{
    public const string SwordSpecialTimeParameter = "SwordSpecialTime";
    private SwordSpecialAttackSettings swordSpecialSettings;
    private bool swordSpecialActive, swordSpecialEntered;
    private float swordSpecialElapsed, swordSpecialRealElapsed;
    private readonly float[] swordSpecialDurations = new float[5];
    private GemSwordSpecialEffects swordSpecialEffects;
    public float SwordSpecialCycleDuration { get; private set; }
    public float SwordSpecialStepProgress => swordSpecialActive ? swordSpecialElapsed - swordSpecialStep : 0;
    private double swordSpecialLastClock;
    private int swordSpecialStep, swordSpecialHash;
    private Vector3 swordSpecialOrigin, swordSpecialDestination;
    private Enemy swordSpecialTarget;
    private readonly List<Enemy> swordSpecialCandidates = new List<Enemy>();
    private readonly List<Enemy> swordSpecialBag = new List<Enemy>();
    private readonly WeaponAttackStats[] swordSpecialStats = new WeaponAttackStats[5];
    private GemSlashSettings swordSpecialSlash;
    public bool IsSwordSpecialAttacking => swordSpecialActive;
    public Enemy SwordSpecialTarget => swordSpecialTarget;
    public float SwordSpecialElapsed => swordSpecialRealElapsed;

    private bool FindSwordSpecialTargets(CombatAttackConfiguration config)
    {
        swordSpecialCandidates.Clear();
        var melee = GetComponent<GemSwordCombat>();
        var contacts = Physics.OverlapSphere(transform.position, config.swordSpecial.targetRadius,
            melee != null ? melee.enemyLayers : (LayerMask)~0, QueryTriggerInteraction.Collide);
        foreach (var contact in contacts)
        {
            var enemy = contact.GetComponentInParent<Enemy>();
            if (ValidSpecialTarget(enemy) && !enemy.transform.IsChildOf(transform) &&
                !swordSpecialCandidates.Contains(enemy)) swordSpecialCandidates.Add(enemy);
        }
        return swordSpecialCandidates.Count > 0;
    }

    private static bool ValidSpecialTarget(Enemy enemy) => enemy != null && enemy.isActiveAndEnabled && !enemy.IsDead;

    private void PrepareSwordSpecialAttack(CombatAttackConfiguration config)
    {
        // Freeze timing, effects and equipment for the accepted five-hit cycle.
        swordSpecialSettings = JsonUtility.FromJson<SwordSpecialAttackSettings>(JsonUtility.ToJson(config.swordSpecial));
        var defaults = CaptureAttackDefaults(PlayerCombatMode.Sword, CombatAttackInput.SpecialAttack);
        SwordSpecialCycleDuration = 0;
        for (int i = 0; i < 5; i++)
        {
            swordSpecialStats[i] = CaptureWeaponStats(PlayerCombatMode.Sword, CombatAttackInput.SpecialAttack, i + 1);
            swordSpecialDurations[i] = swordSpecialStats[i].Duration(SwordSpecialAttackSettings.SecondsPerHit / defaults.speed);
            SwordSpecialCycleDuration += swordSpecialDurations[i];
        }
        if (swordSpecialEffects == null) swordSpecialEffects = GetComponent<GemSwordSpecialEffects>();
        if (swordSpecialEffects == null) swordSpecialEffects = gameObject.AddComponent<GemSwordSpecialEffects>();
        swordSpecialBag.Clear();
        swordSpecialElapsed = swordSpecialRealElapsed = 0;
        swordSpecialActive = true;
        swordSpecialEffects.Begin();
        swordSpecialEntered = false;
        swordSpecialStep = 0;
        planarVelocity = playerVelocity = Vector3.zero;
        anim.SetFloat(SwordSpecialTimeParameter, 0);
    }

    public void EnterSwordSpecialAttack(int index)
    {
        if (!swordSpecialActive || swordSpecialEntered || index != 0) return;
        swordSpecialEntered = true;
        swordSpecialLastClock = AttackClock;
        BeginSwordSpecialStep(0);
    }

    public void ExitSwordSpecialAttack(int index)
    {
        // Internal step changes update the index before Animator exits the old state.
        // An exit from the still-current step is an external interruption.
        if (swordSpecialActive && swordSpecialEntered && swordSpecialStep == index)
            ResetAttackSequence();
    }

    private void BeginSwordSpecialStep(int index)
    {
        EndSwordSpecialWindow();
        swordSpecialStep = activeAttackIndex = index;
        activeWeaponStats = swordSpecialStats[index];
        swordSpecialHash = Animator.StringToHash(activeAttackConfiguration.StatePath(CombatAttackInput.SpecialAttack, index));
        swordSpecialCandidates.RemoveAll(e => !ValidSpecialTarget(e));
        swordSpecialBag.RemoveAll(e => !ValidSpecialTarget(e));
        // A shuffled bag visits everyone once before repeating. One survivor receives all remaining hits.
        if (swordSpecialBag.Count == 0) swordSpecialBag.AddRange(swordSpecialCandidates);
        swordSpecialTarget = null;
        if (swordSpecialBag.Count > 0)
        {
            int pick = Random.Range(0, swordSpecialBag.Count);
            swordSpecialTarget = swordSpecialBag[pick];
            swordSpecialBag.RemoveAt(pick);
        }
        swordSpecialOrigin = swordSpecialDestination = transform.position;
        UpdateSwordSpecialDestination();
        anim.SetFloat(SwordSpecialTimeParameter, 0);
        if (index > 0) anim.Play(swordSpecialHash, 0, 0);
        SetActiveStatePath($"Attack > Sword > Sword_SpecialAttack_{index + 1}");
        GetComponent<GemSwordCombat>()?.Begin(swordSpecialHash, CombatAttackInput.SpecialAttack, index + 1,
            minimumKnockback: swordSpecialSettings.baseKnockback);
        SetSwordSpecialSlash(swordSpecialSettings.steps[index].slash);
    }

    private void UpdateSwordSpecialDestination()
    {
        if (!ValidSpecialTarget(swordSpecialTarget)) return;
        Vector3 direction = Vector3.ProjectOnPlane(swordSpecialTarget.transform.position - swordSpecialOrigin, Vector3.up);
        if (direction.sqrMagnitude < .0001f) direction = transform.forward;
        direction.Normalize();
        swordSpecialDestination = swordSpecialTarget.transform.position - direction * swordSpecialSettings.arrivalDistance;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void UpdateSwordSpecialAttack()
    {
        anim.SetInteger(CombatModeHash, (int)PlayerCombatMode.Sword);
        anim.ResetTrigger(ParryHash);
        ClearAttackTriggers();
        if (!swordSpecialEntered) return;
        double now = AttackClock;
        float delta = (float)System.Math.Max(0, now - swordSpecialLastClock);
        swordSpecialLastClock = now;
        // Each captured one-second base slot is divided by its default speed and combined level/equipment speed.
        if (anim.speed <= 0) return;
        AdvanceSwordSpecialAttack(delta);
    }

    private void AdvanceSwordSpecialAttack(float delta)
    {
        if (!swordSpecialActive || !swordSpecialEntered) return;
        float remaining = Mathf.Max(0, WeaponStatModifier.Finite(delta));
        // Carry real time across boundaries, including different per-hit accessory speed bonuses.
        while (swordSpecialActive && remaining > 0)
        {
            float duration = Mathf.Max(.001f, swordSpecialDurations[swordSpecialStep]);
            float boundary = swordSpecialStep + 1f;
            float seconds = Mathf.Min(remaining, Mathf.Max(0, boundary - swordSpecialElapsed) * duration);
            float advance = seconds / duration;
            float next = Mathf.Min(boundary, swordSpecialElapsed + advance);
            SampleSwordSpecialStep(next - swordSpecialStep, advance, seconds);
            swordSpecialRealElapsed += seconds;
            remaining = Mathf.Max(0, remaining - seconds);
            swordSpecialElapsed = next;
            if (next < boundary - .000001f) break;
            swordSpecialElapsed = boundary;
            if (swordSpecialStep < 4) BeginSwordSpecialStep(swordSpecialStep + 1);
            else
            {
                string locomotion = activeAttackConfiguration.LocomotionStatePath;
                attackRecoveryUntil = System.Math.Max(attackRecoveryUntil,
                    AttackClock + activeAttackDefaults.recoverySeconds / activeWeaponStats.AgilityMultiplier);
                ResetSwordSpecialAttack();
                anim.Play(locomotion, 0, 0);
            }
        }
    }

    private void SampleSwordSpecialStep(float normalized, float delta, float realSeconds)
    {
        var step = swordSpecialSettings.steps[swordSpecialStep];
        float frames = step.animation.length * step.animation.frameRate;
        // Blink-like travel occupies only 50 ms at base speed, never the whole windup.
        float dashEnd = Mathf.Min(step.slash.startFrame / frames,
            swordSpecialSettings.dashSeconds / SwordSpecialAttackSettings.SecondsPerHit);
        float previous = normalized - delta;
        Vector3 movement = Vector3.zero;
        if (previous < dashEnd)
        {
            UpdateSwordSpecialDestination();
            float t = Mathf.Clamp01(normalized / dashEnd);
            Vector3 desired = Vector3.Lerp(swordSpecialOrigin, swordSpecialDestination, t);
            // Dash horizontally into the slice; terrain and gravity determine height.
            movement = Vector3.ProjectOnPlane(desired - transform.position, Vector3.up);
        }
        if (Controller != null && Controller.enabled)
        {
            if (Controller.isGrounded) playerVelocity.y = -2;
            else playerVelocity.y = Mathf.Max(-50, playerVelocity.y + gravityValue * realSeconds);
            movement.y = playerVelocity.y * realSeconds;
            Vector3 beforeMove = transform.position;
            Controller.Move(movement);
            if (previous < dashEnd) swordSpecialEffects?.TraceDash(beforeMove, transform.position);
        }
        anim.SetFloat(SwordSpecialTimeParameter, normalized);
        var effects = GetComponent<GemLightSlashEffects>();
        if (step.followThrough != null && step.followThrough.enabled && normalized * frames >= step.followThrough.startFrame &&
            swordSpecialSlash != step.followThrough)
        {
            effects?.TickSpecial(swordSpecialHash, Mathf.Min(normalized, step.followThrough.startFrame / frames));
            effects?.FinishWindow(swordSpecialHash, normalized);
            SetSwordSpecialSlash(step.followThrough);
        }
        effects?.TickSpecial(swordSpecialHash, normalized);
    }

    private void SetSwordSpecialSlash(GemSlashSettings slash)
    {
        swordSpecialSlash = slash;
        GetComponent<GemLightSlashEffects>()?.Begin(swordSpecialHash, swordSpecialSettings.steps[swordSpecialStep].animation, slash);
    }

    private void EndSwordSpecialWindow()
    {
        GetComponent<GemLightSlashEffects>()?.End(swordSpecialHash);
        GetComponent<GemSwordCombat>()?.End(swordSpecialHash);
        swordSpecialSlash = null;
    }

    private void ResetSwordSpecialAttack()
    {
        if (swordSpecialActive) EndSwordSpecialWindow();
        if (swordSpecialEffects != null) swordSpecialEffects.End();
        swordSpecialActive = swordSpecialEntered = false;
        swordSpecialTarget = null;
        swordSpecialCandidates.Clear();
        swordSpecialBag.Clear();
    }
}
