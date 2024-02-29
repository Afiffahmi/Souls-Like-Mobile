using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JumpAttackState_Melee : EnemyState
{

    private Enemy_Melee enemy;
    private Vector3 lastPlayerPos;

    private float jumpAttackMovementSpeed;
    public JumpAttackState_Melee(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName) : base(enemyBase, stateMachine, animBoolName)
    {
        enemy = enemyBase as Enemy_Melee;
    }

    public override void Enter()
    {
        base.Enter();

        lastPlayerPos = enemy.player.position;
        enemy.agent.isStopped = true;
        enemy.agent.velocity = Vector3.zero;
        
        
        float distanceToPlayer = Vector3.Distance(lastPlayerPos, enemy.transform.position);
        

        jumpAttackMovementSpeed = distanceToPlayer / enemy.travelTimetoTarget;

    }
    
    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();
        Vector3 myPos = enemy.transform.position;
        enemy.agent.enabled = !enemy.ManualMovementActive();

       if(enemy.ManualMovementActive()){
            enemy.transform.position = Vector3.MoveTowards(myPos, lastPlayerPos, jumpAttackMovementSpeed * Time.deltaTime);
            
        }

        if(triggerCalled){
            stateMachine.ChangeState(enemy.recoveryState);
        }
    }
}
