using UnityEngine;
using System.Collections;

public class PlayerArmingAttackState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
        player.anim.SetTrigger("Draw");
    }
    public override void ExitState(PlayerStateManager player){
    }
    public override void UpdateState(PlayerStateManager player){
        player.StartCoroutine(WaitAndSwitch(player));
    }
    IEnumerator WaitAndSwitch(PlayerStateManager player)
    {
        yield return new WaitForSeconds(0.7f);

        player.SwitchState(player.IdlingAttackState);
    }
}
 