using UnityEngine;
using UnityEngine.AI;

public class Boss : MonoBehaviour
{
    public Transform player;
    public float chaseRange = 18f;

    private NavMeshAgent navMeshAgent;
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        navMeshAgent = GetComponent<NavMeshAgent>();

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
            animator.SetBool("isRunning", true);
            animator.SetBool("Idle", false);
            LookAtPlayer();
            MoveTowardsPlayer();
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
