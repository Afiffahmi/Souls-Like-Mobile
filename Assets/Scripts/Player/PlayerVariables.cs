using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerStateManager
{
    public CharacterController Controller;
    private Player playerInput;
    public int currentAttack = 0;
    public float timeSinceAttack;
    public bool isAttackState = false;
    public bool isAtackking = true;
    public float maxVelocity = 0.5f;
    public Animator anim;
    public float velocity = 0.0f;
    public float acceleration = 2f;
    public float deceleration = 2f;
    public Transform cameraMain;
    public float jumpTimer;
    public int jumpForce = 15;
    private CharacterController controller;
    public Vector3 MoveVector;
    private Vector3 lastEmit;
    public float delta = 1;
    public bool isJumped = false;
    public float gap = 0.5f;
    public ParticleSystem footstepParticleSystem;
    private int dir = 1;
    public Vector2 InputVector;
    public PlayerBaseState CurrentState;
    public float PlayerSpeed;
    public float gravityValue = -9.81f;
    [SerializeField] private float groundRayDistance = 0.2f;
    [SerializeField] public bool groundedPlayer;
    [SerializeField] public float jumpHeight = 1.0f;
    public float PlayerRotateSpeed;
    public Vector3 playerVelocity;
    private Vector3 _gravityVector;

    #region ConcreteStates
    public PlayerWalkState WalkingState = new PlayerWalkState();
    public PlayerIdleState IdlingState = new PlayerIdleState();
    public PlayerFallState FallingState = new PlayerFallState();
    public PlayerJumpState JumpingState = new PlayerJumpState();
    public PlayerRunState RunningState = new PlayerRunState();
    public PlayerIdlingAttackState IdlingAttackState = new PlayerIdlingAttackState();
    public PlayeLightAttack1State LightAttacking1 = new PlayeLightAttack1State();
    public PlayeLightAttack2State LightAttacking2 = new PlayeLightAttack2State();
    public PlayeLightAttack3State LightAttacking3 = new PlayeLightAttack3State();
    public PlayerArmingAttackState ArmingAttackState = new PlayerArmingAttackState();
    public PlayerDisarmingAttackState DisarmingAttackState = new PlayerDisarmingAttackState();
    public PlayerWalkAttackState WalkingAttackState = new PlayerWalkAttackState();
    
    #endregion


}