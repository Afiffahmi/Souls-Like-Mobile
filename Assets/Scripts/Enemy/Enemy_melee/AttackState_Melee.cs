using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackState_Melee : EnemyState
{
    public Enemy_Melee enemy;
    private Vector3 attackDirection;
    private float attackMoveSpeed;

    private const float MAX_ATTACK_DISTANCE = 50f; 

    public AttackState_Melee(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName) : base(enemyBase, stateMachine, animBoolName)
    {
        enemy = enemyBase as Enemy_Melee;
    }

    public override void Enter()
    {
        base.Enter();
        attackMoveSpeed = enemy.attackData.moveSpeed;
        enemy.anim.SetFloat("AttackAnimationSpeed",enemy.attackData.animationSpeed);
        enemy.anim.SetFloat("AttackIndex", enemy.attackData.attackIndex);

        enemy.agent.isStopped = true;
        enemy.agent.velocity = Vector3.zero; 

        attackDirection = enemy.player.position + (enemy.transform.forward * MAX_ATTACK_DISTANCE);
        
    }

    public override void Exit()
    {
        base.Exit();
        SetupNextAttack();
        
    }

    public override void Update()
    {
        base.Update();
        if(enemy.ManualMovementActive()){
            enemy.transform.rotation = enemy.FaceTarget(enemy.player.position);
             attackDirection = enemy.player.position + (enemy.transform.forward * MAX_ATTACK_DISTANCE);
        }
            

        if(enemy.ManualMovementActive()){
             enemy.transform.position = Vector3.MoveTowards(enemy.transform.position, attackDirection, attackMoveSpeed * Time.deltaTime);
        }

        if(triggerCalled){
            stateMachine.ChangeState(enemy.recoveryState);
        }
    }

    private bool PlayerClose() => Vector3.Distance(enemy.transform.position, enemy.player.position) <= 1;

    private AttackData UpdatedAttackData(){
       List<AttackData> validAttacks = new List<AttackData>(enemy.attackList);  

       if(PlayerClose())
       validAttacks.RemoveAll(parameter => parameter.attackType == AttackType.Melee);

       int random = Random.Range(0, validAttacks.Count);
       return validAttacks[random];
    }

    private void SetupNextAttack(){

        int recoveryIndex = PlayerClose() ? 1 : 0;
        enemy.anim.SetFloat("AttackIndex", recoveryIndex);
        enemy.attackData = UpdatedAttackData();

    }
}
