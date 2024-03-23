using UnityEngine;

public class PlayerIdlingAttackState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
       Debug.Log("enter idle attack 1");
        player.anim.SetTrigger("IdleAttack");
        player.anim.ResetTrigger("Draw");
        player.anim.SetBool("isFall",false);
        player.anim.SetBool("isSwordWalk",false);

    }
    public override void ExitState(PlayerStateManager player){
        player.anim.ResetTrigger("IdleAttack");
    }
    public override void UpdateState(PlayerStateManager player){
        if(player.MoveVector.magnitude != 0){
            player.SwitchState(player.WalkingAttackState);
        }
        
    }
}
