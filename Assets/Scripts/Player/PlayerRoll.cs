using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerStateManager
{
    [Header("Roll Debugging")]
    [Tooltip("Log roll button presses, rejection reasons, and animation entry. Does not log every frame.")]
    public bool logRollEvents = true;

    private static readonly int RollHash = Animator.StringToHash("Roll");
    private static readonly int RollPlaybackSpeedHash = Animator.StringToHash("RollPlaybackSpeed");
    private static readonly int RollXHash = Animator.StringToHash("RollX");
    private static readonly int RollYHash = Animator.StringToHash("RollY");
    private readonly Dictionary<PlayerCombatMode, RollDefinition> rollDefinitions = new Dictionary<PlayerCombatMode, RollDefinition>();
    private InputAction rollAction;
    private RollDefinition activeRoll;
    private bool rollRequested, rollEntered;
    private Vector3 rollDirection;
    private Vector2 rollAnimationDirection;
    private float rollElapsed, rollDistance, rollDuration, rollMovementDuration, rollRecovery;
    private double nextRollTime;
    public bool IsRolling => rollRequested;
    public Vector3 RollDirection => IsRolling ? rollDirection : Vector3.zero;
    public Vector2 RollAnimationDirection => IsRolling ? rollAnimationDirection : Vector2.zero;
    public float RollCooldownRemaining => Mathf.Max(0f, (float)(nextRollTime - Time.timeAsDouble));

    private void InitializeRolls()
    {
        rollDefinitions.Clear();
        if (anim == null || anim.runtimeAnimatorController == null) return;
        foreach (var state in anim.GetBehaviours<CombatRollState>())
        {
            var definition = state.configuration?.ForMode(state.mode);
            if (definition != null) rollDefinitions[state.mode] = definition;
            else if (logRollEvents)
                Debug.LogWarning($"[Roll] {name}: {state.mode} roll has no configuration/definition. Assign a RollConfiguration to its CombatRollState in the Animator.", this);
        }
    }
    private void BindRollInput()
    {
        rollAction = movementInput?.actions?.FindAction("Roll", false);
        if (logRollEvents && rollAction == null)
            Debug.LogWarning($"[Roll] {name}: Roll input action not found. Check the PlayerInput Actions asset.", this);
    }

    private void PollRollInput()
    {
        if (movementInput == null || !movementInput.isActiveAndEnabled ||
            !movementInput.inputIsActive || !Pressed(rollAction)) return;
        LogRoll($"Button pressed: {rollAction.activeControl?.path ?? rollAction.name}.");
        TryRoll();
    }

    private void OnRoll(InputValue value) { } // Polling is the single input dispatch point.
    public void Roll()
    {
        LogRoll("Button/API called Roll().");
        TryRoll();
    }

    public bool TryRoll()
    {
        if (!isActiveAndEnabled) return RejectRoll("PlayerStateManager is disabled.");
        if (anim == null) return RejectRoll("Animator is missing.");
        if (!hasCombatParameters) return RejectRoll("Animator needs CombatMode (Int), Parry (Trigger), and ParryReturnMode (Int).");
        if (!anim.isActiveAndEnabled || !anim.isInitialized) return RejectRoll("Animator is disabled or not initialized.");
        if (Controller == null || !Controller.enabled) return RejectRoll("CharacterController is missing or disabled.");
        if (IsRolling) return RejectRoll("Already rolling or waiting for the roll Animator state.");
        if (IsAttacking) return RejectRoll("An attack is active.");
        if (IsParrying) return RejectRoll("A parry is active.");
        if (IsChangingEquipment) return RejectRoll("Equipment change is active.");
        if (anim.IsInTransition(0)) return RejectRoll("Animator layer 0 is in transition.");
        if (Time.timeAsDouble < nextRollTime) return RejectRoll($"Recovery cooldown: {RollCooldownRemaining:F2}s remaining.");
        if (!rollDefinitions.TryGetValue(CombatMode, out var definition))
            return RejectRoll($"No roll definition loaded for {CombatMode}. Check the RollConfiguration reference on CombatRollState.");
        if (!RollConfiguration.Valid(definition))
            return RejectRoll($"Invalid {CombatMode} roll configuration. Check all eight non-looping clips, distance, duration, recovery, and 1 <= Movement End Frame <= Animation Frames.");
        if (definition.requireGrounded && !Controller.isGrounded) return RejectRoll("CharacterController.isGrounded is false.");
        var state = anim.GetCurrentAnimatorStateInfo(0);
        bool locomotion = CombatMode == PlayerCombatMode.Normal
            ? state.IsName("Base Layer.Normal.Idle") || state.IsName("Base Layer.Normal.Locomotion")
            : state.IsTag("CombatLocomotion") && anim.GetInteger(ParryReturnModeHash) == (int)CombatMode;
        if (!locomotion)
            return RejectRoll($"Not in settled {CombatMode} locomotion. State hash: {state.fullPathHash}, ParryReturnMode: {anim.GetInteger(ParryReturnModeHash)}.");
        string rollStatePath = RollConfiguration.StatePath(CombatMode);
        if (!anim.HasState(0, Animator.StringToHash(rollStatePath)))
            return RejectRoll($"Animator state '{rollStatePath}' is missing. Apply Roll Clips to Animator.");
        PrepareRoll(definition);
        anim.SetTrigger(RollHash);
        LogRoll($"Accepted: trigger sent for {rollStatePath}. Direction={rollAnimationDirection}, distance={rollDistance:F2}, duration={rollDuration:F2}s.");
        return true;
    }

    private void LogRoll(string message)
    {
        if (logRollEvents) Debug.Log($"[Roll] {name}: {message}", this);
    }

    private bool RejectRoll(string reason)
    {
        LogRoll($"Blocked: {reason}");
        return false;
    }

    private void PrepareRoll(RollDefinition definition)
    {
        activeRoll = definition;
        rollRequested = true; rollEntered = false; rollElapsed = 0f;
        // Snapshot tuning so Inspector changes cannot alter a roll partway through it.
        rollDistance = definition.distance; rollDuration = definition.duration; rollRecovery = definition.recovery;
        rollMovementDuration = rollDuration * ((float)definition.movementEndFrame / definition.animationFrames);
        Transform reference = MovementReferenceOverride != null ? MovementReferenceOverride : cameraMain;
        GetPlanarBasis(reference, out var forward, out var right);
        Vector2 input = new Vector2(MoveVector.x, MoveVector.z);
        rollDirection = input.sqrMagnitude > inputDeadzone * inputDeadzone
            ? (forward * input.y + right * input.x).normalized : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (rollDirection.sqrMagnitude < .001f) rollDirection = Vector3.forward;
        // Capture animation direction before any facing changes. A backward/sideways
        // roll must not turn the body and then accidentally select the forward clip.
        rollAnimationDirection = ToAnimatorDirection(transform.InverseTransformDirection(rollDirection));
        if (!definition.UsesDirectionalAnimations && facingMode != PlayerFacingMode.External)
            transform.rotation = Quaternion.LookRotation(rollDirection, Vector3.up);
        planarVelocity = Vector3.zero;
        EndEquipmentLegLocomotion();
        ResetLocomotionAnimation();
        // Directional children are authored with one-second normalized durations.
        anim.SetFloat(RollPlaybackSpeedHash, (definition.UsesDirectionalAnimations ? 1f : definition.animation.length) / rollDuration);
        if (definition.UsesDirectionalAnimations)
        {
            anim.SetFloat(RollXHash, rollAnimationDirection.x);
            anim.SetFloat(RollYHash, rollAnimationDirection.y);
        }
        anim.ResetTrigger(ParryHash);
        ClearAttackTriggers();
    }

    public bool NotifyRollEntered(RollDefinition definition)
    {
        if (!RollConfiguration.Valid(definition)) return RejectRoll("Animator entered roll with a missing or invalid RollConfiguration.");
        if (!isActiveAndEnabled) return RejectRoll("Animator entered roll while PlayerStateManager is disabled.");
        if (!rollRequested)
        {
            // Direct Animator triggers obey the same busy, ground, and recovery restrictions.
            if (IsAttacking || IsParrying || IsChangingEquipment || Time.timeAsDouble < nextRollTime ||
                Controller == null || !Controller.enabled || (definition.requireGrounded && !Controller.isGrounded))
                return RejectRoll("Direct Animator roll rejected: player is busy, recovering, not grounded, or CharacterController is unavailable.");
            PrepareRoll(definition);
        }
        if (activeRoll != definition) return RejectRoll("Animator roll configuration does not match the requested definition.");
        rollEntered = true;
        LogRoll($"Started: entered {RollConfiguration.StatePath(definition.mode)}.");
        MaintainRollLock();
        ActionState = definition.mode == PlayerCombatMode.Normal ? PlayerActionState.Normal : PlayerActionState.Attack;
        AttackSubstate = definition.mode switch { PlayerCombatMode.Sword => PlayerAttackSubstate.Sword,
            PlayerCombatMode.Magic => PlayerAttackSubstate.Magic, PlayerCombatMode.Bow => PlayerAttackSubstate.Bow, _ => PlayerAttackSubstate.None };
        SetActiveStatePath(definition.mode == PlayerCombatMode.Normal ? "Normal > Roll" : $"Attack > {definition.mode} > Roll");
        return true;
    }

    public void MaintainRollLock()
    {
        if (!IsRolling || activeRoll == null) return;
        anim.SetInteger(CombatModeHash, (int)activeRoll.mode);
        anim.ResetTrigger(ParryHash);
        ClearAttackTriggers();
        if (rollEntered) anim.ResetTrigger(RollHash);
        if (activeRoll.UsesDirectionalAnimations)
        {
            anim.SetFloat(RollXHash, rollAnimationDirection.x);
            anim.SetFloat(RollYHash, rollAnimationDirection.y);
        }
        ResetLocomotionAnimation();
    }

    private void MoveRoll(float dt)
    {
        if (Controller == null || !Controller.enabled || dt <= 0f) return;
        // Clamp the final travel step at the cutoff, even if this update straddles it.
        // Keep the roll state active until the Animator finishes the remaining frames.
        float step = rollEntered ? Mathf.Min(dt, Mathf.Max(0f, rollMovementDuration - rollElapsed)) : 0f;
        rollElapsed += step;
        Vector3 displacement = rollDirection * (rollDistance * step / rollMovementDuration);
        planarVelocity = displacement / dt;
        groundedPlayer = Controller.isGrounded;
        if (groundedPlayer && playerVelocity.y < 0f) playerVelocity.y = -2f;
        playerVelocity.y = Mathf.Max(playerVelocity.y + gravityValue * dt, -50f);
        Controller.Move(displacement + Vector3.up * playerVelocity.y * dt);
        groundedPlayer = Controller.isGrounded;
        PlayerSpeed = CurrentSpeed; velocity = CurrentSpeed;
        ResetLocomotionAnimation();
    }

    private void NotifyRollLocomotionEntered()
    {
        if (!rollRequested || !rollEntered) return;
        nextRollTime = Time.timeAsDouble + rollRecovery;
        LogRoll($"Finished: recovery={rollRecovery:F2}s.");
        ResetRoll();
    }
    private void ResetRoll()
    {
        if (rollRequested) planarVelocity = Vector3.zero;
        rollRequested = false; rollEntered = false; activeRoll = null; rollElapsed = 0f;
        if (anim != null && rollDefinitions.Count > 0) anim.ResetTrigger(RollHash);
    }
}
