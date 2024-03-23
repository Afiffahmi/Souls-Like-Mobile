using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
        player.anim.SetFloat("Blend", 0);
        player.velocity = 0.0f;
        Debug.Log( "enter idle");
    }
    public override void ExitState(PlayerStateManager player){
    }
    public override void UpdateState(PlayerStateManager player){
        if(player.MoveVector.magnitude != 0){
            player.SwitchState(player.WalkingState);
        }

    }
}
