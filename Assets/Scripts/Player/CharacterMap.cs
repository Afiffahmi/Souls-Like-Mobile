using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerStateManager
{
    private void OnMove(InputValue value)
    {
        InputVector = value.Get<Vector2>();
        MoveVector.x = InputVector.x;
        MoveVector.z = InputVector.y;
    }

    private void OnJump(InputValue value)
    {
       if (CurrentState != JumpingState && CurrentState != FallingState && !isAttackState && CurrentState != IdlingAttackState)
        {
            anim.SetTrigger("Jump");
            SwitchState(JumpingState);
        }
    }

    private void OnSprint(InputValue value)
    {
        
        if (CurrentState != JumpingState && CurrentState != FallingState && !isAttackState && CurrentState != IdlingAttackState)
        {
            SwitchState(RunningState);
        }
        else if (CurrentState == IdlingAttackState && CurrentState != WalkingState && CurrentState != FallingState  && CurrentState != RunningState && CurrentState != WalkingAttackState ){
            isAttackState = false;
            SwitchState(DisarmingAttackState);
        }
    }

    private void OnAttack(InputValue value)
    {
       if (CurrentState != JumpingState && CurrentState != FallingState && CurrentState != WalkingState && CurrentState == IdlingState && CurrentState != RunningState)
        {

            isAttackState = true;
            SwitchState(ArmingAttackState);
        }
        if(CurrentState == IdlingAttackState && isAttackState)
        {  
            timeSinceAttack = 0;
            SwitchState(LightAttacking1);
        }
        

        
    }

}