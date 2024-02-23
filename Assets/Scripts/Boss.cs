// Boss.cs

using UnityEngine;
using UnityEngine.AI;

public class Boss : MonoBehaviour
{
    public Transform player;
    public float chaseRange = 10f;

    public NavMeshAgent navMeshAgent;
    public Animator animator;

    private bool isMoving =false;

    void Start()
    {
        if (!player)
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
        }
    }

    void Update()
    {
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        bool isAttacking = currentState.IsTag("Attack");
        bool isBlocking = currentState.IsTag("Block");

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (!isAttacking && !isBlocking && distanceToPlayer <= chaseRange)
        {
            SetAnimatorFlags(false, true);
            LookAtPlayer();
            MoveTowardsPlayer();
            
        }else if(isBlocking){
            LookAtPlayer();
        }
        else
        {
            SetAnimatorFlags(true, false);
        }
    }

    void SetAnimatorFlags(bool idle, bool isRunning)
    {
        animator.SetBool("Idle", idle);
        animator.SetBool("isRunning", isRunning);
    }


    public void LookAtPlayer()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0;
        Quaternion rotation = Quaternion.LookRotation(direction);
        transform.rotation = rotation;
    Quaternion targetRotation = Quaternion.LookRotation(direction);

        float rotationSpeed = 5f; // Adjust this value as needed for desired rotation speed
    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    void MoveTowardsPlayer()
    {
        if (navMeshAgent.enabled) // Check if not currently attacking
        {
            Vector3 targetPosition = player.position;
            navMeshAgent.SetDestination(targetPosition);
        }
    }
}
