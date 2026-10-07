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
        Vector3 move = (player.cameraMain.forward * player.MoveVector.z + player.cameraMain.right * player.MoveVector.x);
        move.y = 0f;
        player.Controller.Move(player.PlayerSpeed * move * Time.deltaTime);
        if (move != Vector3.zero)
        {
            player.gameObject.transform.forward = move;
        }
        if(player.groundedPlayer && !player.isAttackState){
            player.SwitchState(player.IdlingState);
        } 
    
    }
}
