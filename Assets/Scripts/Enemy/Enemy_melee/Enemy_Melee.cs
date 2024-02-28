using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct AttackData{
    public string attackName;
    public float attackRange;
    public float moveSpeed;
    public float attackIndex;
    [Range(1,2)]
    public float animationSpeed;
    public AttackType attackType;
}

public enum AttackType{
    Melee,
    Ranged
}

public class Enemy_Melee : Enemy
{   
    [Header("Junp attack")]
    public float travelTimetoTarget = 1;
    public float jumpAttackCooldown = 10;
    public float lastTimeJumped;
    public float minJumpDistance;
    


    
    public IdleState_Melee IdleState { get; private set; }

    public MoveState_Melee moveState { get; private set; }

    public RecoveryState_Melee recoveryState { get; private set; }

    public ChaseState_Melee chaseState { get; private set; }

    public AttackState_Melee attackState { get; private set; }
    public DeadState_Melee deadState { get; private set; } 
    public JumpAttackState_Melee jumpAttackState { get; private set; } 

    public BossVisual_Melee VisualBoss { get; private set; }



    [Header("Attack data")]
    public AttackData attackData;
    public List<AttackData> attackList;

    protected override void Awake()
    {
        base.Awake();
        VisualBoss = GetComponent<BossVisual_Melee>();


        IdleState = new IdleState_Melee(this, stateMachine, "Idle");
        moveState = new MoveState_Melee(this, stateMachine, "Move");
        recoveryState = new RecoveryState_Melee(this, stateMachine, "Recovery");
        chaseState= new ChaseState_Melee(this, stateMachine, "Chase");
        attackState = new AttackState_Melee(this, stateMachine, "Attack");
        deadState = new DeadState_Melee(this, stateMachine, "Idle"); 
        jumpAttackState = new JumpAttackState_Melee(this, stateMachine, "JumpAttack");   
    }
    

    protected override void Start()
    {
        base.Start();
        stateMachine.Initialize(IdleState);
    }

    protected override void Update()
    {
        base.Update();
        stateMachine.CurrentState.Update();
    }

    public override void GetHit(){
        base.GetHit();

        if(healthPoints <= 0){
            stateMachine.ChangeState(deadState);
        }
    }

    public bool CanDoJumpAttack(){

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        if(distanceToPlayer < minJumpDistance){
            return false;
        }

        if(Time.time > lastTimeJumped + jumpAttackCooldown){
            lastTimeJumped = Time.time;
            return true;
        }
        return false;
    }

    protected override void OnDrawGizmosSelected() {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackData.attackRange);
    }

    public bool PlayerInAttackRange(){
        return Vector3.Distance(transform.position, player.position) < attackData.attackRange;
    }
}
