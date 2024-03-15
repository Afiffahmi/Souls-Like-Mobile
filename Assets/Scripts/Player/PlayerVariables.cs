using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerStateManager
{
    public CharacterController Controller;
    private Player playerInput;
    public Animator anim;
    
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
    #endregion


}