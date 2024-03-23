using UnityEngine;

public class PlayeLightAttack2State : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
         Debug.Log("enter Attack2");
         player.anim.SetBool("isLight2",true);
         
    }
    public override void ExitState(PlayerStateManager player){
        Debug.Log("Exit Attack2");
        player.anim.SetBool("isLight2",true);
       

    }
    public override void UpdateState(PlayerStateManager player){
    
    }
}
