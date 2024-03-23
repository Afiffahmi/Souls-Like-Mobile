using UnityEngine;

public class PlayerRunState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
        player.anim.SetFloat("Blend", 1);
        Debug.Log("Run State");
    }
    public override void ExitState(PlayerStateManager player){
    }
    public override void UpdateState(PlayerStateManager player){
        Vector3 move = (player.cameraMain.forward * player.MoveVector.z + player.cameraMain.right * player.MoveVector.x);
        move.y = 0f;

        player.Controller.Move(20 * move * Time.deltaTime);

        if (move != Vector3.zero)
        {
            player.gameObject.transform.forward = move;
            player.EmitFootprint();
        }
        else if (player.MoveVector.magnitude == 0){
            player.SwitchState(player.IdlingState);
        }
    }
    
}
