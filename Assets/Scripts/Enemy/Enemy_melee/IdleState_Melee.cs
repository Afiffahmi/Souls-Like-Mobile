using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IdleState_Melee : EnemyState
{
    public Enemy_Melee enemy;

    public IdleState_Melee(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName) : base(enemyBase, stateMachine, animBoolName)
    {
        enemy = enemyBase as Enemy_Melee;
    }

    public override void Enter()
    {
        base.Enter();
        stateTimer = enemyBase.idleTime;


    }

    public override void Exit()
    {
        base.Exit();
    
    }

    public override void Update()
    {
        base.Update();
         if(enemy.PlayerInAggressionRange()){
            stateMachine.ChangeState(enemy.recoveryState);
            return;
        }
        
        if (stateTimer < 0)
        {
            stateMachine.ChangeState(enemy.moveState);
        }
    }
    
}
