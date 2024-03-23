using UnityEngine;

public class PlayerFallState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
        player.anim.SetBool("isFall",true);
    }
    public override void ExitState(PlayerStateManager player){
        player.anim.SetBool("isFall",false);
    }
    public override void UpdateState(PlayerStateManager player){
        if(player.groundedPlayer && !player.isAttackState){
            player.SwitchState(player.IdlingState);
        } 
        else if(player.groundedPlayer && player.isAttackState){
            player.SwitchState(player.IdlingAttackState);
        } 
    }
}
