using UnityEngine;
using System.Collections;

public class DisarmBowState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
        player.anim.SetBool("isEquip",false);
    }
    public override void ExitState(PlayerStateManager player){
    }
    public override void UpdateState(PlayerStateManager player){
        player.StartCoroutine(WaitAndSwitch(player));
    }
    IEnumerator WaitAndSwitch(PlayerStateManager player)
    {
        yield return new WaitForSeconds(0.0f);

        player.SwitchState(player.WalkingState);
    }
}