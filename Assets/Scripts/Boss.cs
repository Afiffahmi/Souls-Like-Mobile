using UnityEngine;
using UnityEngine.AI;

public class Boss : MonoBehaviour
{
    public Transform player;
    public float chaseRange = 10f;

    public NavMeshAgent navMeshAgent;
    public Animator animator;

    void Start()
    {

        if (!player)
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
        }
    }

    void Update()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= chaseRange)
        {
            animator.SetBool("Idle", false);
            animator.SetBool("isRunning", true);
            LookAtPlayer();
            MoveTowardsPlayer();
        }else{
            animator.SetBool("Idle", true);
            animator.SetBool("isRunning", false);
        }
    }

    public void LookAtPlayer()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0;
        Quaternion rotation = Quaternion.LookRotation(direction);
        transform.rotation = rotation;
    }

    void MoveTowardsPlayer()
    {
        // Set the destination for NavMeshAgent
        navMeshAgent.SetDestination(player.position);
    }
}
