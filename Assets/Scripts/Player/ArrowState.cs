using UnityEngine;
using System.Collections;

public class ArrowState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
        player.anim.SetBool("Arrow",true);
    }
    public override void ExitState(PlayerStateManager player){
    }
    public override void UpdateState(PlayerStateManager player){
        player.StartCoroutine(WaitAndSwitch(player));
    }
    IEnumerator WaitAndSwitch(PlayerStateManager player)
    {
        yield return new WaitForSeconds(0.7f);

        player.SwitchState(player.EquipBowState);
    }
}