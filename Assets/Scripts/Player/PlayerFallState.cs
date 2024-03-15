using UnityEngine;

public class PlayerFallState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
        Debug.Log("fall state");

    }
    public override void ExitState(PlayerStateManager player){

    }
    public override void UpdateState(PlayerStateManager player){
        if(player.groundedPlayer){
            player.SwitchState(player.IdlingState);
        } 
    }
}
