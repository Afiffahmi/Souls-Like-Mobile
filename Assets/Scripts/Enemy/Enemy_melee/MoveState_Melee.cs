using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveState_Melee : EnemyState
{
    private Enemy_Melee enemy;
    private Vector3 destination;

    public MoveState_Melee(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName) : base(enemyBase, stateMachine, animBoolName)
    {
        enemy = enemyBase as Enemy_Melee;
    }

    public override void Enter()
    {
        base.Enter();

        enemy.agent.speed = enemy.moveSpeed;

        destination = enemy.GetPatrolDestination();
        enemy.agent.SetDestination(destination);
        
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
        

        enemy.transform.rotation = enemy.FaceTarget(GetNextPatrolPoint());
        
        enemy.agent.SetDestination(destination);

   

        if(enemy.agent.remainingDistance <= enemy.agent.stoppingDistance + 0.5f){
            stateMachine.ChangeState(enemy.IdleState);
        }
    }


   
}
