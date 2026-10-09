using UnityEngine;

public sealed class CombatAttackState : StateMachineBehaviour
{
    public CombatAttackConfiguration configuration;
    public CombatAttackInput input;
    public int stepIndex;
    private CombatAttackStep windupStep;
    private int windupParameter;
    private bool controlsWindup;
    public static string WindupSpeedParameter(int index) => "SwordLightWindup" + (index + 1);

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var player = animator.GetComponentInParent<PlayerStateManager>();
        if (player != null && !player.NotifyAttackEntered(configuration, input, stepIndex) && configuration != null)
        {
            animator.Play(configuration.LocomotionStatePath, layerIndex, 0f);
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
        LockWeapon(animator);
        controlsWindup = false;
        windupStep = null;
        if (configuration != null && configuration.weapon == PlayerCombatMode.Sword &&
            input == CombatAttackInput.LightAttack && stepIndex >= 0 && stepIndex < configuration.lightAttackChain.Count)
        {
            windupStep = configuration.lightAttackChain[stepIndex];
            windupParameter = Animator.StringToHash(WindupSpeedParameter(stepIndex));
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == windupParameter && parameter.type == AnimatorControllerParameterType.Float)
                    controlsWindup = true;
            UpdateWindup(animator, stateInfo);
        }
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LockWeapon(animator);
        UpdateWindup(animator, stateInfo);
        if (configuration != null && configuration.weapon == PlayerCombatMode.Sword)
        {
            animator.GetComponentInParent<ElementalGems.GemLightSlashEffects>()?.Tick(stateInfo.fullPathHash, stateInfo.normalizedTime);
        }
    }
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (controlsWindup) animator.SetFloat(windupParameter, 1f);
        controlsWindup = false;
        if (configuration != null && configuration.weapon == PlayerCombatMode.Sword)
            animator.GetComponentInParent<ElementalGems.GemLightSlashEffects>()?.FinishWindow(stateInfo.fullPathHash, stateInfo.normalizedTime);
        animator.GetComponentInParent<PlayerStateManager>()?.NotifyAttackExited(configuration, input, stepIndex, stateInfo.normalizedTime);
        if (configuration != null && configuration.weapon == PlayerCombatMode.Sword)
        {
            animator.GetComponentInParent<ElementalGems.GemSwordCombat>()?.End(stateInfo.fullPathHash);
            animator.GetComponentInParent<ElementalGems.GemLightSlashEffects>()?.End(stateInfo.fullPathHash);
        }
    }

    private void UpdateWindup(Animator animator, AnimatorStateInfo stateInfo)
    {
        if (!controlsWindup) return;
        float delta = animator.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime :
            animator.updateMode == AnimatorUpdateMode.Fixed ? Time.fixedDeltaTime : Time.deltaTime;
        float multiplier = windupStep != null ? windupStep.WindupMultiplier(stateInfo.normalizedTime,
            delta * Mathf.Abs(stateInfo.speed * animator.speed), stepIndex > 0 ? configuration.lightComboWindupSpeed : 1f,
            configuration.lightComboFinisherRecoverySpeed) : 1f;
        animator.SetFloat(windupParameter, multiplier);
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
