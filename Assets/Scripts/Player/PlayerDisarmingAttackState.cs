using UnityEngine;
using System.Collections;

public class PlayerDisarmingAttackState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player){
        Debug.Log("enter disarming attack");
        player.anim.SetTrigger("Sheath");
    }
    public override void ExitState(PlayerStateManager player){
    }
    public override void UpdateState(PlayerStateManager player){
        player.StartCoroutine(WaitAndSwitch(player));
    }
    IEnumerator WaitAndSwitch(PlayerStateManager player)
    {
        yield return new WaitForSeconds(0.8f);

        player.SwitchState(player.IdlingState);
    }
}
