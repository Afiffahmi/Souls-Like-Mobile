using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaseState_Melee : EnemyState
{
    public Enemy_Melee enemy;  
    public float lastTimeUpdatedDestination;

    public ChaseState_Melee(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName) : base(enemyBase, stateMachine, animBoolName)
    {
        enemy = enemyBase as Enemy_Melee;
    }

    public override void Enter()
    {
        base.Enter();

        enemy.agent.speed = enemy.chaseSpeed; 
        enemy.agent.isStopped = false;
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();
        if(enemy.PlayerInAttackRange()){
            stateMachine.ChangeState(enemy.attackState);
            return;
        }

        enemy.transform.rotation = enemy.FaceTarget(GetNextPatrolPoint());

        if(CanUpdateDestination()){
            enemy.agent.destination = enemy.player.transform.position;
        }
    }

    private bool CanUpdateDestination(){
        if(Time.time > lastTimeUpdatedDestination + .25f){
            lastTimeUpdatedDestination = Time.time;
            return true;
        }
        return false;
    }
}
