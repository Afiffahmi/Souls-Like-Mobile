using UnityEngine;

public class PlayerWalkState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
    }
    public override void ExitState(PlayerStateManager player){
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
