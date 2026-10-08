using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum LockOnMovementSpace
{
    TargetRelative,
    CameraRelative
}

/// <summary>Target selection and target-relative facing, independent of the locomotion motor.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStateManager), typeof(PlayerInput))]
public sealed class PlayerLockOn : MonoBehaviour
{
    [Header("References")]
    public Camera viewCamera;
    [Header("Movement")]
    [Tooltip("CameraRelative keeps isometric joystick directions consistent. TargetRelative makes up approach the enemy and left/right orbit it.")]
    public LockOnMovementSpace movementSpace = LockOnMovementSpace.TargetRelative;
    [Header("Area Targeting")]
    [Tooltip("Optional wall blocking. When off, being inside an available enemy's lock-on area is sufficient.")]
    public bool requireLineOfSight = false;
    [Tooltip("Layers that block targeting. Player and target colliders are ignored.")]
    public LayerMask obstructionMask = ~0;
    [Min(0f)] public float obstructionGraceTime = 0.5f;
    [Min(1f)] public float facingSpeed = 720f;
    [SerializeField] private LockOnTarget currentTarget;

    public LockOnTarget CurrentTarget => currentTarget;
    public bool IsLockedOn => ownsMovementFrame && currentTarget != null;
    public event Action<LockOnTarget> TargetChanged;

    private PlayerStateManager player;
    private PlayerInput playerInput;
    private InputAction toggleAction;
    private Transform movementFrame;
    private Transform previousFrame;
    private PlayerFacingMode previousFacing;
    private bool previousOmnidirectionalRun;
    private bool ownsMovementFrame;
    private float blockedTime;

    private void Awake()
    {
        player = GetComponent<PlayerStateManager>();
        playerInput = GetComponent<PlayerInput>();
        var frame = new GameObject(name + " LockOn Movement Frame");
        frame.hideFlags = HideFlags.HideAndDontSave;
        movementFrame = frame.transform;
        ResolveCamera();
    }

    private void Start()
    {
        if (playerInput.actions != null) toggleAction = playerInput.actions.FindAction("LockOn", false);
    }

    // PlayerInput uses Send Messages in this scene. Poll once in Update so the
    // callback and polling cannot toggle twice for the same press.
    private void OnLockOn(InputValue value) { }

    private void Update()
    {
        ResolveCamera();
        if (playerInput.isActiveAndEnabled && playerInput.inputIsActive && toggleAction != null
            && toggleAction.enabled && toggleAction.WasPressedThisFrame()) ToggleLock();

        if (!ownsMovementFrame) return;
        if (!player.isActiveAndEnabled || !IsValidTarget(currentTarget, true))
        {
            Unlock();
            return;
        }
        if (!requireLineOfSight || HasLineOfSight(currentTarget)) blockedTime = 0f;
        else blockedTime += Time.deltaTime;
        if (blockedTime > obstructionGraceTime)
        {
            Unlock();
            return;
        }
        UpdateMovementFrame(Time.deltaTime);
    }

    private void ResolveCamera()
    {
        if (viewCamera != null) return;
        if (player.cameraMain != null) viewCamera = player.cameraMain.GetComponent<Camera>();
        if (viewCamera == null) viewCamera = Camera.main;
    }

    /// <summary>Can also be wired to a mobile UI Button's OnClick.</summary>
    public void ToggleLock()
    {
        if (ownsMovementFrame) { Unlock(); return; }
        ResolveCamera();
        LockOnTarget best = null;
        bool bestVisible = false;
        float bestScore = float.PositiveInfinity;
        float bestDistance = float.PositiveInfinity;
        foreach (LockOnTarget candidate in LockOnTarget.ActiveTargets)
        {
            if (!IsValidTarget(candidate) || (requireLineOfSight && !HasLineOfSight(candidate))) continue;
            Vector3 viewport = viewCamera != null ? viewCamera.WorldToViewportPoint(candidate.AimPosition) : Vector3.zero;
            bool visible = viewCamera != null && viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f
                && viewport.y >= 0f && viewport.y <= 1f;
            float distance = (candidate.AimPosition - transform.position).sqrMagnitude;
            // Visibility is a preference only. Off-screen areas remain eligible.
            float score = distance;
            if (visible)
            {
                float x = (viewport.x - 0.5f) * viewCamera.pixelWidth;
                float y = (viewport.y - 0.5f) * viewCamera.pixelHeight;
                score = x * x + y * y;
            }
            if (best == null || (visible && !bestVisible) || (visible == bestVisible
                && (score < bestScore || (Mathf.Approximately(score, bestScore) && distance < bestDistance))))
            { bestVisible = visible; bestScore = score; bestDistance = distance; best = candidate; }
        }
        if (best != null) TryLockOn(best);
    }

    public bool TryLockOn(LockOnTarget target)
    {
        ResolveCamera();
        if (!isActiveAndEnabled || !player.isActiveAndEnabled || !IsValidTarget(target)
            || (requireLineOfSight && !HasLineOfSight(target))) return false;
        if (!ownsMovementFrame)
        {
            previousFrame = player.MovementReferenceOverride;
            previousFacing = player.facingMode;
            previousOmnidirectionalRun = player.AllowOmnidirectionalRun;
            ownsMovementFrame = true;
        }
        currentTarget = target;
        blockedTime = 0f;
        player.MovementReferenceOverride = movementFrame;
        player.facingMode = PlayerFacingMode.External;
        player.AllowOmnidirectionalRun = true;
        UpdateMovementFrame(0f);
        TargetChanged?.Invoke(currentTarget);
        return true;
    }

    public void Unlock()
    {
        if (!ownsMovementFrame) { currentTarget = null; return; }
        if (player != null)
        {
            if (player.MovementReferenceOverride == movementFrame) player.MovementReferenceOverride = previousFrame;
            player.facingMode = previousFacing;
            player.AllowOmnidirectionalRun = previousOmnidirectionalRun;
        }
        ownsMovementFrame = false;
        currentTarget = null;
        blockedTime = 0f;
        TargetChanged?.Invoke(null);
    }

    private void UpdateMovementFrame(float dt)
    {
        Vector3 forward = Vector3.ProjectOnPlane(currentTarget.AimPosition - transform.position, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        Quaternion facingRotation = Quaternion.LookRotation(forward, Vector3.up);
        Quaternion movementRotation = facingRotation;
        if (movementSpace == LockOnMovementSpace.CameraRelative)
        {
            Transform cameraReference = viewCamera != null ? viewCamera.transform : player.cameraMain;
            PlayerStateManager.GetPlanarBasis(cameraReference, out Vector3 cameraForward, out _);
            movementRotation = Quaternion.LookRotation(cameraForward, Vector3.up);
        }
        movementFrame.SetPositionAndRotation(transform.position, movementRotation);
        // Runs while idle too, so a moving enemy remains in front of the character.
        transform.rotation = Quaternion.RotateTowards(transform.rotation, facingRotation, facingSpeed * dt);
    }

    private bool IsValidTarget(LockOnTarget target, bool retainingLock = false)
    {
        return target != null && target.IsAvailable && !target.transform.IsChildOf(transform)
            && !transform.IsChildOf(target.transform)
            && target.ContainsPlayer(transform.position, retainingLock);
    }

    private bool HasLineOfSight(LockOnTarget target)
    {
        Vector3 origin = transform.position + Vector3.up;
        if (IsObstructed(origin, target)) return false;
        return viewCamera == null || !IsObstructed(viewCamera.transform.position, target);
    }

    private bool IsObstructed(Vector3 origin, LockOnTarget target)
    {
        Vector3 ray = target.AimPosition - origin;
        if (ray.sqrMagnitude < 0.0001f) return false;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, ray.normalized, ray.magnitude, obstructionMask, QueryTriggerInteraction.Ignore))
        {
            Transform hitTransform = hit.collider.transform;
            if (hitTransform.IsChildOf(transform) || hitTransform.IsChildOf(target.transform)) continue;
            return true;
        }
        return false;
    }

    private void OnDisable() => Unlock();
    private void OnDestroy()
    {
        Unlock();
        if (movementFrame != null) Destroy(movementFrame.gameObject);
    }

    private void OnValidate()
    {
        obstructionGraceTime = Mathf.Max(0f, obstructionGraceTime);
        facingSpeed = Mathf.Max(1f, facingSpeed);
    }
}
