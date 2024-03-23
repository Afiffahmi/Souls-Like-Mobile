using UnityEngine;
using System.Collections;

public class PlayeLightAttack1State : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
        player.currentAttack++;
        player.isAtackking = true;
        player.anim.SetTrigger("Attack" + player.currentAttack);
        Debug.Log(" attack 1");
        
    }
    public override void ExitState(PlayerStateManager player){
        
    }
    public override void UpdateState(PlayerStateManager player){
        
        player.StartCoroutine(WaitAndSwitch(player));   
        if(player.currentAttack > 3)
        {
            player.currentAttack = 0;

        } 
    }

    IEnumerator WaitAndSwitch(PlayerStateManager player)
    {
        yield return new WaitForSeconds(0.4f);
        player.isAtackking = false;
        player.SwitchState(player.IdlingAttackState);
    }
}
