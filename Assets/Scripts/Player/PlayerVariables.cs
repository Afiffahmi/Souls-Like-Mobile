using UnityEngine;

public partial class PlayerStateManager
{
    [Header("References")]
    public CharacterController Controller;
    public Animator anim;
    [Tooltip("Movement uses this camera's horizontal forward/right axes. Falls back to Camera.main.")]
    public Transform cameraMain;

    [Header("Locomotion")]
    [Min(0.1f)] public float walkSpeed = 6f;
    [Min(0.1f)] public float runSpeed = 9f;
    [Tooltip("Horizontal acceleration in metres per second squared.")]
    [Min(0.1f)] public float acceleration = 14f;
    [Min(0.1f)] public float deceleration = 20f;
    [Range(0f, 0.95f)] public float inputDeadzone = 0.1f;
    [Tooltip("CameraForward gives strafing/backpedalling. MovementDirection turns toward travel. External leaves facing to a future controller.")]
    public PlayerFacingMode facingMode = PlayerFacingMode.CameraForward;
    [Tooltip("Turning speed in degrees per second.")]
    [Min(1f)] public float PlayerRotateSpeed = 720f;
    public float gravityValue = -20f;
    [Min(0f)] public float animationDamping = 0.08f;

    [HideInInspector] public Vector2 InputVector;
    [HideInInspector] public Vector3 MoveVector;
    [HideInInspector] public Vector3 playerVelocity;
    [HideInInspector] public bool groundedPlayer;
    public PlayerBaseState CurrentState;

    public PlayerWalkState WalkingState = new PlayerWalkState();
    public PlayerIdleState IdlingState = new PlayerIdleState();
    public PlayerRunState RunningState = new PlayerRunState();

    // Kept to preserve serialization and references from older prototype states.
    // The locomotion controller does not select those states or handle their input.
    [HideInInspector] public PlayerFallState FallingState = new PlayerFallState();
    [HideInInspector] public int currentAttack;
    [HideInInspector] public float timeSinceAttack;
    [HideInInspector] public bool isAttackState;
    [HideInInspector] public bool isAtackking;
    [HideInInspector] public float maxVelocity = 1f;
    [HideInInspector] public float velocity;
    [HideInInspector] public float delta = 1f;
    [HideInInspector] public float gap = 0.5f;
    [HideInInspector] public ParticleSystem footstepParticleSystem;
    [HideInInspector] public float PlayerSpeed;
}
