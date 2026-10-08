using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public partial class PlayerStateManager : MonoBehaviour
{
    private PlayerInput movementInput;
    private InputAction moveAction;
    private InputAction sprintAction;

    public bool SprintHeld { get; private set; }
    public bool HasMoveInput => new Vector2(MoveVector.x, MoveVector.z).sqrMagnitude > inputDeadzone * inputDeadzone;
    public float CurrentSpeed => planarVelocity.magnitude;

    private void Awake()
    {
        Controller = GetComponent<CharacterController>();
        movementInput = GetComponent<PlayerInput>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        if (cameraMain == null && Camera.main != null) cameraMain = Camera.main.transform;

        foreach (Animator childAnimator in GetComponentsInChildren<Animator>(true))
            childAnimator.applyRootMotion = false;

        // The CharacterController owns movement; a dynamic Rigidbody would fight it.
        if (TryGetComponent(out Rigidbody body))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        CacheAnimatorParameters();
        InitializeCombat();
        CurrentState = IdlingState;
        CurrentState.EnterState(this);
    }

    private void Start()
    {
        BindCombatInput();
        // PlayerInput creates its own action copy during OnEnable, before Start.
        if (movementInput != null && movementInput.actions != null)
        {
            moveAction = movementInput.actions.FindAction("Move", false);
            sprintAction = movementInput.actions.FindAction("Sprint", false);
        }
    }

    private void Update()
    {
        if (movementInput != null)
        {
            bool inputActive = movementInput.isActiveAndEnabled && movementInput.inputIsActive;
            SetMoveInput(inputActive && moveAction != null && moveAction.enabled
                ? moveAction.ReadValue<Vector2>() : Vector2.zero);
            SetSprintInput(inputActive && sprintAction != null && sprintAction.enabled && sprintAction.IsPressed());
        }

        UpdateCombat();
        SwitchState(!HasMoveInput ? IdlingState : CanRun ? (PlayerBaseState)RunningState : WalkingState);
        CurrentState.UpdateState(this);
    }

    private void OnDisable()
    {
        DisableCombat();
        SetMoveInput(Vector2.zero);
        SetSprintInput(false);
        planarVelocity = Vector3.zero;
        playerVelocity = Vector3.zero;
        velocity = 0f;
        ResetLocomotionAnimation();
    }

    public void SwitchState(PlayerBaseState state)
    {
        if (state == null || state == CurrentState) return;
        CurrentState?.ExitState(this);
        CurrentState = state;
        CurrentState.EnterState(this);
    }

    // Existing non-locomotion states can still compile, but this controller only
    // selects Idle, Walk and Run. Combat gates sprint separately; no jump or targeting input is handled here.
    public void ApplyGravity()
    {
        if (Controller != null && Controller.enabled)
            Controller.Move(Vector3.up * gravityValue * Time.deltaTime);
    }



    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0.1f, walkSpeed);
        runSpeed = Mathf.Max(walkSpeed + 0.1f, runSpeed);
        acceleration = Mathf.Max(0.1f, acceleration);
        deceleration = Mathf.Max(0.1f, deceleration);
        PlayerRotateSpeed = Mathf.Max(1f, PlayerRotateSpeed);
        inputDeadzone = Mathf.Clamp(inputDeadzone, 0f, 0.95f);
        gravityValue = Mathf.Min(-0.01f, gravityValue);
    }
}
