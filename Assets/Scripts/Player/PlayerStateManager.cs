using UnityEngine;


public partial class PlayerStateManager : MonoBehaviour
{

    private void Awake()
    {
        Controller = GetComponent<CharacterController>();
        playerInput = new Player();
        PlayerSpeed = 10f;
        PlayerRotateSpeed = 100;

        _gravityVector = new Vector3(0, -9.81f, 0);
    }

    void Start()
    {
        lastEmit = transform.position;
        isJumped = false;
        jumpTimer = 0.8f;
        CurrentState = IdlingState;
        CurrentState.EnterState(this);
    }

    void Update()
    {
        timeSinceAttack += Time.deltaTime;
        if (CurrentState != FallingState && CurrentState != JumpingState && !groundedPlayer && CurrentState != WalkingState && CurrentState != IdlingState && CurrentState != RunningState&& CurrentState != IdlingAttackState ){
            SwitchState(FallingState);
        }
        

        CurrentState.UpdateState(this);
        ApplyGravity();
    }
    private void FixedUpdate()
    {
        groundedPlayer = Physics.Raycast(transform.position, Vector3.down, groundRayDistance);
        if(timeSinceAttack > 2)
        {
            currentAttack = 0;
        }
    }
    
    public void SwitchState(PlayerBaseState state){
        CurrentState.ExitState(this);
        CurrentState = state;
        state.EnterState(this);
    }

    #region Movement
    public void ApplyGravity(){
        Controller.Move(_gravityVector * Time.deltaTime);
    }
    public void Jump(){
        playerVelocity.y += Mathf.Sqrt(jumpHeight * -3.0f * gravityValue);
    }

    public void Move()
    {
        Vector3 move = (cameraMain.forward * MoveVector.z + cameraMain.right * MoveVector.x);
        move.y = 0f;

        Controller.Move(PlayerSpeed * move * Time.deltaTime);

        if (move != Vector3.zero)
        {
            velocity += Time.deltaTime * acceleration;
            velocity = Mathf.Min(velocity, maxVelocity);
            gameObject.transform.forward = move;

        }
        anim.SetFloat("Blend", velocity);
    }
    


    #endregion


}