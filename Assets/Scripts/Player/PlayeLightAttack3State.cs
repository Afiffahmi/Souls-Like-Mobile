using UnityEngine;

public class PlayeLightAttack3State : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
         Debug.Log("enter Attack3");
         player.anim.SetBool("Light3",true);
         
    }
    public override void ExitState(PlayerStateManager player){
        Debug.Log("Exit Attack3");
        player.anim.SetBool("Light3",false);

    }
    public override void UpdateState(PlayerStateManager player){

    }
}
