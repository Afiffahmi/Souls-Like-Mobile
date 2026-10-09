using UnityEngine;

public partial class PlayerStateManager
{
    [Header("Parry")]
    [Tooltip("Unity animation timeline frame to hold while the button is pressed.")]
    [Min(0)] public int parryHoldFrame = 20;
    [Tooltip("Seconds after pressing during which an incoming hit can be parried. Holding does not extend this window.")]
    [Min(0.01f)] public float parryWindowSeconds = 0.2f;
    [Tooltip("Playback speed while raising the guard to the held frame. Does not shorten the timing window.")]
    [Min(.1f)] public float parryRaiseSpeed = 3f;
    [Tooltip("Playback speed of the full follow-through after a successful parry.")]
    [Min(.1f)] public float parrySuccessSpeed = 2.5f;
    public bool logParryEvents = false;

    private static readonly int ParryTimeHash = Animator.StringToHash("ParryTime");
    private static readonly int ParryFinishedHash = Animator.StringToHash("ParryFinished");
    private AnimationClip parryClip;
    private bool hasParryControl, parryEntered, parrySucceeded, parryEnding;
    private bool parryUsesInput, parryButtonHeld;
    private float parryPlaybackTime;
    private double parryDeadline;

    public bool IsParryWindowOpen => hasParryControl && isActiveAndEnabled &&
        anim != null && anim.isActiveAndEnabled && parryRequested &&
        !parryEnding && !parrySucceeded && Time.timeAsDouble <= parryDeadline;
    public bool IsHoldingParry => parryEntered && !parryEnding && !parrySucceeded &&
        parryPlaybackTime >= ParryHoldTime;
    // Defense requires the guard pose and a currently held button. Merely
    // playing a missed/released parry animation does not grant a block.
    public bool IsDefending => IsHoldingParry && (parryUsesInput
        ? movementInput != null && movementInput.isActiveAndEnabled && movementInput.inputIsActive &&
            parryAction != null && parryAction.enabled && parryAction.IsPressed()
        : parryButtonHeld);
    public event System.Action ParrySucceeded;

    private float ParryHoldTime => parryClip == null ? 0f :
        Mathf.Min(Mathf.Max(0, parryHoldFrame) / parryClip.frameRate,
            Mathf.Max(0f, parryClip.length - 1f / parryClip.frameRate));

    private void InitializeParry()
    {
        bool time = false, finished = false;
        foreach (var parameter in anim.parameters)
        {
            time |= parameter.nameHash == ParryTimeHash && parameter.type == AnimatorControllerParameterType.Float;
            finished |= parameter.nameHash == ParryFinishedHash && parameter.type == AnimatorControllerParameterType.Bool;
        }
        foreach (var state in anim.GetBehaviours<CombatAnimatorState>())
            if (state.isParry && state.parryAnimation != null)
            {
                parryClip = state.parryAnimation;
                break;
            }
        hasParryControl = time && finished && parryClip != null &&
            parryClip.length > 0f && parryClip.frameRate > 0f;
    }

    private void PrepareParry()
    {
        parryEntered = parrySucceeded = parryEnding = false;
        parryUsesInput = parryButtonHeld = false;
        parryPlaybackTime = 0f;
        parryDeadline = Time.timeAsDouble + Mathf.Max(0.01f, parryWindowSeconds);
        anim.SetFloat(ParryTimeHash, 0f);
        anim.SetBool(ParryFinishedHash, false);
        // Keep the current gait on the masked legs layer while the base layer parries.
        TrackEquipmentLocomotion(parryReturnMode, anim.GetCurrentAnimatorStateInfo(0).normalizedTime);
        BeginEquipmentLegLocomotion();
        LogParry("Pressed: timing window opened.");
    }

    private void UpdateParry()
    {
        if (!hasParryControl || parryEnding) return;
        bool held = parryUsesInput
            ? movementInput != null && movementInput.isActiveAndEnabled && movementInput.inputIsActive &&
                parryAction != null && parryAction.enabled && parryAction.IsPressed()
            : parryButtonHeld;

        // One-shot RequestParry/TryParry calls get a timing window without latching a hold.
        if (!parrySucceeded && !held &&
            (parryUsesInput || Time.timeAsDouble > parryDeadline))
        {
            FinishParry();
            return;
        }
        if (!parryEntered) return;
        float dt = anim.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float speed = parrySucceeded ? parrySuccessSpeed : parryRaiseSpeed;
        parryPlaybackTime = Mathf.Min(parryPlaybackTime + dt * Mathf.Max(0f, anim.speed) * Mathf.Max(.1f, speed),
            parrySucceeded ? parryClip.length : ParryHoldTime);
        anim.SetFloat(ParryTimeHash, parryPlaybackTime / parryClip.length);
        if (parrySucceeded && parryPlaybackTime >= parryClip.length) FinishParry();
    }

    /// <summary>Call on mobile PointerDown. Pair with EndParryHold on PointerUp/cancel.</summary>
    public void BeginParryHold()
    {
        if (TryParry()) parryButtonHeld = true;
    }

    public void EndParryHold()
    {
        parryButtonHeld = false;
        if (parryRequested && !parrySucceeded) FinishParry();
    }

    /// <summary>
    /// Call once when a parryable enemy attack actually hits, before applying damage.
    /// True means consume that hit; false means resolve damage/blocking normally.
    /// </summary>
    public bool TryReceiveParryableHit()
    {
        if (!IsParryWindowOpen) return false;
        parrySucceeded = true;
        // Show the deflection immediately even when impact occurs during guard startup.
        parryPlaybackTime = Mathf.Max(parryPlaybackTime, ParryHoldTime);
        anim.SetFloat(ParryTimeHash, parryPlaybackTime / parryClip.length);
        LogParry("Success: playing the rest of the animation.");
        ParrySucceeded?.Invoke();
        return true;
    }

    private void FinishParry()
    {
        parryEnding = true;
        anim.SetBool(ParryFinishedHash, true);
        LogParry(parrySucceeded ? "Animation finished." : "Released/missed: returning to locomotion.");
    }

    private void ResetParry()
    {
        // Keep walking through both the held pose and successful follow-through.
        if (parryRequested || parryEntered) EndEquipmentLegLocomotion();
        parryRequested = parryEntered = parrySucceeded = parryEnding = false;
        parryUsesInput = parryButtonHeld = false;
        parryPlaybackTime = 0f;
        parryDeadline = 0d;
        if (!hasParryControl || anim == null) return;
        // A disabled player must not leave the zero-speed parry state latched.
        anim.SetBool(ParryFinishedHash, true);
        anim.SetFloat(ParryTimeHash, 0f);
    }

    private void LogParry(string message)
    {
        if (logParryEvents) Debug.Log($"[Parry] {name}: {message}", this);
    }
}
