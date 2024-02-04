using UnityEngine;
using UnityEngine.AI;

public class BossRun : StateMachineBehaviour
{
    public float attackRange = 3f;
    public float chaseRange = 10f;
    public float idleRange = 5f;
    private NavMeshObstacle navMeshObstacle;
    private Transform player;
    private NavMeshAgent navMeshAgent;
    private Boss boss;
    private Animator animator;
    private BossIdle bossIdle;

    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        navMeshObstacle = animator.GetComponent<NavMeshObstacle>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        navMeshAgent = animator.GetComponent<NavMeshAgent>();
        boss = animator.GetComponent<Boss>();
        bossIdle = animator.GetComponent<BossIdle>();

        if (navMeshAgent == null)
        {
            Debug.LogError("NavMeshAgent component not found.");
        }
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
{
    if (player == null || navMeshAgent == null || animator == null || bossIdle == null || navMeshObstacle == null)
    {
        Debug.LogError("Null reference detected.");
        return;
    }
    
    boss.LookAtPlayer();
    float distanceToPlayer = Vector3.Distance(animator.transform.position, player.position);

    if (distanceToPlayer <= chaseRange)
    {
        // Set the destination for NavMeshAgent
        navMeshAgent.SetDestination(player.position);

        // Add avoidance priority to avoid NavMesh modifiers
        navMeshAgent.avoidancePriority = 50;

        // Set stopping distance to prevent getting too close to obstacles
        navMeshAgent.stoppingDistance = attackRange;

        if (distanceToPlayer < attackRange)
        {
            TriggerAttack(animator);
        }
        else if (distanceToPlayer > chaseRange || distanceToPlayer < 1.0f)
        {
            Debug.Log("Transition to idle");
            bossIdle.TransitionToIdle();
        }
    }
    else if (distanceToPlayer <= idleRange)
    {
        Debug.Log("Transition to idle");
        bossIdle.TransitionToIdle();
    }
}


    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("The boss has collided with " + collision.gameObject.name);
    }

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Clean up or additional actions on state exit if needed
    }

    private void TriggerAttack(Animator animator)
    {
        if (animator != null)
        {
            animator.SetTrigger("LightAttack");
            Debug.Log("LightAttack");
        }
        else
        {
            Debug.LogError("Animator is null when trying to trigger attack.");
        }
    }
}
