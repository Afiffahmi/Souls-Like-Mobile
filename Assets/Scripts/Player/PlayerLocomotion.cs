using UnityEngine;

public enum PlayerFacingMode
{
    CameraForward,
    MovementDirection,
    External
}

public partial class PlayerStateManager
{
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private Vector3 planarVelocity;
    private bool hasLocomotionParameters;
    private PlayerLockOn locomotionLockOn;

    private bool RequiresWalking
    {
        get
        {
            if (locomotionLockOn == null) TryGetComponent(out locomotionLockOn);
            return IsCombatActive || (locomotionLockOn != null && locomotionLockOn.IsLockedOn);
        }
    }

    /// <summary>
    /// Optional movement frame supplied by another system. Null uses the camera.
    /// A facing controller can set this and choose External facing, without
    /// changing this motor or its character-local animation directions.
    /// </summary>
    public Transform MovementReferenceOverride { get; set; }

    /// <summary>Enabled by target-relative controllers that support eight-way running.</summary>
    public bool AllowOmnidirectionalRun { get; set; }

    // Unlocked exploration turns the body toward travel, so every stick
    // direction uses the forward animation. External facing keeps strafing.
    public bool UsesForwardLocomotion => facingMode == PlayerFacingMode.MovementDirection
        && MovementReferenceOverride == null;

    // Three forward sectors of the eight-way input (front +/- 67.5 degrees).
    public static bool IsForwardRunDirection(Vector2 direction) =>
        direction.y > 0.0001f && direction.y >= Mathf.Abs(direction.x) * 0.41421356f;

    public bool CanRun
    {
        get
        {
            Vector2 input = new Vector2(MoveVector.x, MoveVector.z);
            if (IsRolling || RequiresWalking || !SprintHeld || !HasMoveInput) return false;
            if (AllowOmnidirectionalRun || UsesForwardLocomotion) return true;
            if (!IsForwardRunDirection(input)) return false;
            Transform reference = MovementReferenceOverride != null ? MovementReferenceOverride : cameraMain;
            GetPlanarBasis(reference, out Vector3 forward, out Vector3 right);
            Vector3 local = transform.InverseTransformDirection(forward * input.y + right * input.x);
            return IsForwardRunDirection(new Vector2(local.x, local.z));
        }
    }

    public void Move() => Move(CanRun ? runSpeed : walkSpeed);

    public void Move(float targetSpeed)
    {
        if (Controller == null || !Controller.enabled) return;
        if (IsRolling)
        {
            MoveRoll(Time.deltaTime);
            return;
        }

        if (IsSwordSpecialAttacking)
        {
            planarVelocity = Vector3.zero;
            ResetLocomotionAnimation();
            return; // The special timeline owns jump movement, gravity and collision.
        }

        if (RequiresWalking) targetSpeed = Mathf.Min(targetSpeed, walkSpeed);
        if (IsAttackMovementLocked) targetSpeed = 0f;
        float dt = Time.deltaTime;
        if (cameraMain == null && Camera.main != null) cameraMain = Camera.main.transform;
        Transform reference = MovementReferenceOverride != null ? MovementReferenceOverride : cameraMain;
        GetPlanarBasis(reference, out Vector3 forward, out Vector3 right);

        // Clamp length only for translation: diagonals must not move faster.
        Vector2 input = Vector2.ClampMagnitude(new Vector2(MoveVector.x, MoveVector.z), 1f);
        // Keep gravity/collision active, but ignore travel and input-facing during Light attacks.
        if (IsAttackMovementLocked || input.sqrMagnitude <= inputDeadzone * inputDeadzone) input = Vector2.zero;
        Vector3 desiredDirection = forward * input.y + right * input.x;
        // Match this frame's input immediately, including reversals and release.
        // Smoothing velocity here retains momentum in the previous direction.
        planarVelocity = desiredDirection * targetSpeed;

        ApplyFacing(forward, desiredDirection, dt);
        Vector3 localVelocity = transform.InverseTransformDirection(planarVelocity);
        if (!AllowOmnidirectionalRun && !UsesForwardLocomotion
            && !IsForwardRunDirection(new Vector2(localVelocity.x, localVelocity.z)))
            planarVelocity = Vector3.ClampMagnitude(planarVelocity, walkSpeed);
        groundedPlayer = Controller.isGrounded;
        if (groundedPlayer && playerVelocity.y < 0f) playerVelocity.y = -2f;
        playerVelocity.y = Mathf.Max(playerVelocity.y + gravityValue * dt, -50f);
        // Elemental mobility scales translation only; input, gravity, attack locks and animation timing remain owned by this motor.
        var gems = GetComponent<ElementalGems.GemManager>();
        Controller.Move((planarVelocity * (gems != null ? gems.MovementScale : 1f) + Vector3.up * playerVelocity.y) * dt);
        groundedPlayer = Controller.isGrounded;

        // Retained for compatibility with the project's older state scripts.
        PlayerSpeed = targetSpeed;
        velocity = CurrentSpeed;
        UpdateLocomotionAnimation(dt);
    }

    private void ApplyFacing(Vector3 frameForward, Vector3 desiredDirection, float dt)
    {
        if (facingMode == PlayerFacingMode.External || desiredDirection.sqrMagnitude < 0.000001f) return;
        Vector3 facing = facingMode == PlayerFacingMode.CameraForward ? frameForward : desiredDirection;
        transform.rotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(facing, Vector3.up), PlayerRotateSpeed * dt);
    }

    public static void GetPlanarBasis(Transform reference, out Vector3 forward, out Vector3 right)
    {
        forward = reference != null ? Vector3.ProjectOnPlane(reference.forward, Vector3.up) : Vector3.forward;
        // A camera looking straight down still has a usable horizontal right axis.
        if (forward.sqrMagnitude < 0.0001f && reference != null)
            forward = Vector3.Cross(Vector3.ProjectOnPlane(reference.right, Vector3.up), Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        right = Vector3.Cross(Vector3.up, forward).normalized;
    }

    public static Vector2 ToAnimatorDirection(Vector3 localVelocity)
    {
        Vector2 direction = new Vector2(localVelocity.x, localVelocity.z);
        float largestAxis = Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
        // Square coordinates put diagonals exactly at (+/-1, +/-1), as authored
        // in both trees. Speed carries magnitude separately from direction.
        return largestAxis > 0.0001f ? direction / largestAxis : Vector2.zero;
    }

    private void UpdateLocomotionAnimation(float dt)
    {
        if (anim == null || !anim.isActiveAndEnabled || !hasLocomotionParameters) return;
        float speed = CurrentSpeed;
        if (speed < 0.01f)
        {
            ResetLocomotionAnimation();
            return;
        }

        // Animation describes travel relative to the body, independently of the
        // joystick's movement frame. A camera-relative input can therefore play
        // a strafe/backpedal while the body continues facing the locked enemy.
        Vector2 direction = UsesForwardLocomotion ? Vector2.up
            : ToAnimatorDirection(transform.InverseTransformDirection(planarVelocity));
        float blendSpeed = speed <= walkSpeed ? speed / walkSpeed
            : 1f + (speed - walkSpeed) / Mathf.Max(0.1f, runSpeed - walkSpeed);
        // Never retain a Run weight during lock-on, combat or restricted strafing.
        if (RequiresWalking || (!AllowOmnidirectionalRun && !IsForwardRunDirection(direction)))
        {
            blendSpeed = Mathf.Min(blendSpeed, 1f);
            anim.SetFloat(SpeedHash, Mathf.Min(anim.GetFloat(SpeedHash), 1f));
        }
        if (UsesForwardLocomotion)
        {
            // Switch out of the strafing tree immediately on unlock, including
            // while the model is still smoothly turning to the new direction.
            anim.SetFloat(MoveXHash, 0f);
            anim.SetFloat(MoveYHash, 1f);
        }
        else
        {
            anim.SetFloat(MoveXHash, direction.x, animationDamping, dt);
            anim.SetFloat(MoveYHash, direction.y, animationDamping, dt);
        }
        anim.SetFloat(SpeedHash, Mathf.Clamp(blendSpeed, 0f, 2f), animationDamping, dt);
    }

    private void CacheAnimatorParameters()
    {
        hasLocomotionParameters = false;
        if (anim == null || anim.runtimeAnimatorController == null) return;
        bool x = false, y = false, speed = false;
        foreach (AnimatorControllerParameter parameter in anim.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Float) continue;
            x |= parameter.nameHash == MoveXHash;
            y |= parameter.nameHash == MoveYHash;
            speed |= parameter.nameHash == SpeedHash;
        }
        hasLocomotionParameters = x && y && speed;
        if (!hasLocomotionParameters)
            Debug.LogWarning("Assign PlayerLocomotion.controller to the player's Animator (MoveX, MoveY and Speed). Movement will still work.", this);
    }

    private void ResetLocomotionAnimation()
    {
        if (anim == null || !hasLocomotionParameters || !anim.isActiveAndEnabled) return;
        anim.SetFloat(MoveXHash, 0f);
        anim.SetFloat(MoveYHash, 0f);
        anim.SetFloat(SpeedHash, 0f);
    }
}
