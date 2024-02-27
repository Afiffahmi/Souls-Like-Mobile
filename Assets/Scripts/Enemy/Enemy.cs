using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float turnSpeed;
    public float aggressionRange;

    [Header("Attack data")]
    public float attackRange;
    public float attackMoveSpeed;

    [Header("Idle Info")]
    public float idleTime;

    [Header("Move data")]
    public float moveSpeed;
    public float chaseSpeed;
    private bool manualMovement;

    [SerializeField] private Transform[] patrolPoints;
    private int currentPatrolIndex;

    public Transform player {get; private set;}
    public Animator anim {get; private set;}
    public UnityEngine.AI.NavMeshAgent agent {get; private set;}
    public EnemyStateMachine stateMachine {get; private set;}

    

    protected virtual void Awake(){
        player = GameObject.Find("Player").GetComponent<Transform>();
        stateMachine = new EnemyStateMachine();
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();  
        anim = GetComponentInChildren<Animator>();
    }

    protected virtual void Start(){
        InitializePatrolPoints();
    }

    

    protected virtual void Update(){
    
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, aggressionRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    public void ActiveManualMovement(bool manualMovement) => this.manualMovement = manualMovement;
    public bool ManualMovementActive() => manualMovement;
    public void AnimationTrigger() => stateMachine.CurrentState.AnimationTrigger();

    public bool PlayerInAggressionRange(){
        return Vector3.Distance(transform.position, player.position) < aggressionRange;
    }

    public bool PlayerInAttackRange(){
        return Vector3.Distance(transform.position, player.position) < attackRange;
    }

    public Vector3 GetPatrolDestination(){
        Vector3 destination = patrolPoints[currentPatrolIndex].transform.position;

        currentPatrolIndex++;

        if(currentPatrolIndex == patrolPoints.Length){
            currentPatrolIndex = 0;
        }

        return destination;
    }

    private void InitializePatrolPoints(){
        foreach(Transform point in patrolPoints){
            point.parent = null;
        }
    }
   
    public Quaternion FaceTarget(Vector3 target){
        Quaternion targetRotation = Quaternion.LookRotation(target - transform.position);

        Vector3 currentEulerAngels = transform.rotation.eulerAngles;
        float yRotation = Mathf.LerpAngle(currentEulerAngels.y, targetRotation.eulerAngles.y, turnSpeed * Time.deltaTime);

        return Quaternion.Euler(currentEulerAngels.x, yRotation, currentEulerAngels.z);
    }
}
