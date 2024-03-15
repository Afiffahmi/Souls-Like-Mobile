using UnityEngine;

public class PlayerJumpState : PlayerBaseState
{


    public override void EnterState(PlayerStateManager player)
    {

        Debug.Log("Jump state");
    }

    public override void ExitState(PlayerStateManager player)
    {
    }

    public override void UpdateState(PlayerStateManager player)
    {
        Vector3 jumpVelocity = Vector3.up * player.jumpForce;
        player.Controller.Move(jumpVelocity * Time.deltaTime);
        player.jumpTimer -= Time.deltaTime;
        Vector3 move = (player.cameraMain.forward * player.MoveVector.z + player.cameraMain.right * player.MoveVector.x);
        move.y = 0f;
        player.Controller.Move(player.PlayerSpeed * move * Time.deltaTime);
        if (move != Vector3.zero)
        {
            player.gameObject.transform.forward = move;

        }

        if(!player.groundedPlayer && player.jumpTimer <= 0f){
            player.ApplyGravity();
            player.SwitchState(player.FallingState);
            player.jumpForce = 15;
            player.jumpTimer = 0.8f;
        }

    }
}
