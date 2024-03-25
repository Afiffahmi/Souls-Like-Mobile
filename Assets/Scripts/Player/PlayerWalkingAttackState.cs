using UnityEngine;

public class PlayerWalkAttackState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
        player.anim.SetBool("isSwordWalk",true);

    }
    public override void ExitState(PlayerStateManager player){
        
    }
    public override void UpdateState(PlayerStateManager player){
        Vector3 move = (player.cameraMain.forward * player.MoveVector.z + player.cameraMain.right * player.MoveVector.x);
        move.y = 0f;

        player.Controller.Move(5 * move * Time.deltaTime);

        if (move != Vector3.zero && !player.isAtackking)
        {
            player.gameObject.transform.forward = move;
        }
        else if (player.MoveVector.magnitude == 0){
            player.SwitchState(player.IdlingAttackState);
        }
    }
    
}
