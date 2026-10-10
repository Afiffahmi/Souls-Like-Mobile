using UnityEngine;

public sealed class CombatAttackState : StateMachineBehaviour
{
    public CombatAttackConfiguration configuration;
    public CombatAttackInput input;
    public int stepIndex;
    private CombatAttackStep windupStep;
    private int windupParameter;
    private bool controlsWindup;
    private float agility = 1f;
    private float playbackRatio = 1f, recoverySeconds, comboWindup = 1f, lightRecovery = 1f;
    public static string WindupSpeedParameter(int index) => "SwordLightWindup" + (index + 1);

    public static string AttackSpeedParameter(PlayerCombatMode weapon, CombatAttackInput input, int index) =>
        weapon == PlayerCombatMode.Sword && input == CombatAttackInput.LightAttack ? WindupSpeedParameter(index) :
        weapon + "_" + input + "_" + index + "_Agility";

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var player = animator.GetComponentInParent<PlayerStateManager>();
        if (IsChargedSwordHeavy && (player == null || !player.IsSwordHeavyAttacking))
        {
            animator.Play(configuration.LocomotionStatePath, layerIndex, 0f);
            return;
        }
        if (player != null && !player.NotifyAttackEntered(configuration, input, stepIndex) && configuration != null)
        {
            animator.Play(configuration.LocomotionStatePath, layerIndex, 0f);
            return;
        }
        if (IsChargedSwordHeavy)
        {
            player.EnterSwordHeavyAttack(stateInfo.fullPathHash);
            LockWeapon(animator);
            return;
        }
        if (player != null && configuration != null && configuration.weapon == PlayerCombatMode.Sword && player.IsAttacking)
        {
            player.GetComponent<ElementalGems.GemSwordCombat>()?.Begin(stateInfo.fullPathHash, input,
                stepIndex + 1, player.IsSwordLightComboFinisher);
            var chain = configuration.Chain(input);
            var step = chain != null && stepIndex >= 0 && stepIndex < chain.Count ? chain[stepIndex] : null;
            var clip = input == CombatAttackInput.SpecialAttack ? configuration.specialAttack?.animation : step?.animation;
            var slash = input == CombatAttackInput.SpecialAttack ? configuration.specialAttack?.slash : step?.slash;
            var effects = player.GetComponent<ElementalGems.GemLightSlashEffects>();
            effects?.Begin(stateInfo.fullPathHash, clip, slash);
            effects?.Tick(stateInfo.fullPathHash, stateInfo.normalizedTime);
        }
        agility = player != null ? player.CurrentWeaponAttackStats.AgilityMultiplier : 1f;
        var defaults = player != null ? player.CurrentAttackDefaults : null;
        // Animator state speed comes from its asset. Cancel it so the manager's default wins without rebuilding the controller.
        playbackRatio = defaults != null ? defaults.speed / Mathf.Max(.01f,Mathf.Abs(stateInfo.speed)) : 1f;
        recoverySeconds = defaults != null ? defaults.recoverySeconds : 0;
        comboWindup = player != null ? Mathf.Clamp(player.swordComboWindupSpeed,1,8) : configuration.lightComboWindupSpeed;
        lightRecovery = player != null ? Mathf.Clamp(player.swordLightRecoverySpeed,1,8) : configuration.lightComboFinisherRecoverySpeed;
        LockWeapon(animator);
        controlsWindup = false;
        windupStep = null;
        if (configuration != null && configuration.weapon == PlayerCombatMode.Sword &&
            input == CombatAttackInput.LightAttack && stepIndex >= 0 && stepIndex < configuration.lightAttackChain.Count)
        {
            windupStep = configuration.lightAttackChain[stepIndex];
        }
        if (configuration != null)
        {
            windupParameter = Animator.StringToHash(AttackSpeedParameter(configuration.weapon, input, stepIndex));
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == windupParameter && parameter.type == AnimatorControllerParameterType.Float)
                    controlsWindup = true;
            UpdateWindup(animator, stateInfo);
        }
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LockWeapon(animator);
        if (IsChargedSwordHeavy) return;
        UpdateWindup(animator, stateInfo);
        if (configuration != null && configuration.weapon == PlayerCombatMode.Sword)
        {
            animator.GetComponentInParent<ElementalGems.GemLightSlashEffects>()?.Tick(stateInfo.fullPathHash, stateInfo.normalizedTime);
        }
    }
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (IsChargedSwordHeavy)
        {
            animator.GetComponentInParent<PlayerStateManager>()?.ExitSwordHeavyAttack();
            return;
        }
        if (controlsWindup) animator.SetFloat(windupParameter, 1f);
        controlsWindup = false;
        if (configuration != null && configuration.weapon == PlayerCombatMode.Sword)
            animator.GetComponentInParent<ElementalGems.GemLightSlashEffects>()?.FinishWindow(stateInfo.fullPathHash, stateInfo.normalizedTime);
        animator.GetComponentInParent<PlayerStateManager>()?.NotifyAttackExited(configuration, input, stepIndex, stateInfo.normalizedTime, agility,recoverySeconds);
        if (configuration != null && configuration.weapon == PlayerCombatMode.Sword)
        {
            animator.GetComponentInParent<ElementalGems.GemSwordCombat>()?.End(stateInfo.fullPathHash);
            animator.GetComponentInParent<ElementalGems.GemLightSlashEffects>()?.End(stateInfo.fullPathHash);
        }
    }

    private bool IsChargedSwordHeavy => configuration != null && configuration.UsesSwordHeavyCharge && input == CombatAttackInput.HeavyAttack && stepIndex == 0;

    private void UpdateWindup(Animator animator, AnimatorStateInfo stateInfo)
    {
        if (!controlsWindup) return;
        float delta = animator.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime :
            animator.updateMode == AnimatorUpdateMode.Fixed ? Time.fixedDeltaTime : Time.deltaTime;
        float multiplier = windupStep != null ? windupStep.WindupMultiplier(stateInfo.normalizedTime,
            delta * Mathf.Abs(stateInfo.speed * animator.speed) * playbackRatio * agility, stepIndex > 0 ? comboWindup : 1f,
            lightRecovery) : 1f;
        animator.SetFloat(windupParameter, multiplier * playbackRatio * agility);
    }

    private void LockWeapon(Animator animator)
    {
        if (configuration == null) return;
        animator.SetInteger("CombatMode", (int)configuration.weapon);
        animator.ResetTrigger("Parry");
        animator.ResetTrigger("LightAttack");
        animator.ResetTrigger("HeavyAttack");
        animator.ResetTrigger("SpecialAttack");
    }
}
