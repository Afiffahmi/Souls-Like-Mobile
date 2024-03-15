using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
        Debug.Log("idle state");
        player.anim.SetTrigger("Idle");
    }
    public override void ExitState(PlayerStateManager player){
        player.anim.ResetTrigger("Idle");
    }
    public override void UpdateState(PlayerStateManager player){
        if(player.MoveVector.magnitude != 0){
            player.SwitchState(player.WalkingState);
        }
    }
}
