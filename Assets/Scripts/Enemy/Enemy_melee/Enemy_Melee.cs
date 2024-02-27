using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_Melee : Enemy
{   
    
    public IdleState_Melee IdleState { get; private set; }

    public MoveState_Melee moveState { get; private set; }

    public RecoveryState_Melee recoveryState { get; private set; }

    public ChaseState_Melee chaseState { get; private set; }

    public AttackState_Melee attackState { get; private set; }


    protected override void Awake()
    {
        base.Awake();
        IdleState = new IdleState_Melee(this, stateMachine, "Idle");
        moveState = new MoveState_Melee(this, stateMachine, "Move");
        recoveryState = new RecoveryState_Melee(this, stateMachine, "Recovery");
        chaseState= new ChaseState_Melee(this, stateMachine, "Chase");
        attackState = new AttackState_Melee(this, stateMachine, "Attack");
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
}
