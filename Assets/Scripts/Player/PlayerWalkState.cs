using UnityEngine;

public class PlayerWalkState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
        Debug.Log("walkState");
        player.anim.SetTrigger("Run");
    }
    public override void ExitState(PlayerStateManager player){
        player.anim.ResetTrigger("Run");
    }
    public override void UpdateState(PlayerStateManager player){
        if (player.MoveVector.magnitude == 0){
            player.SwitchState(player.IdlingState);
        }
        else if(player.groundedPlayer) {
            player.Move();
        }
    }
    
}
