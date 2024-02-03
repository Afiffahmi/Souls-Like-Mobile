using UnityEngine;

public class BossRun : StateMachineBehaviour
{
    public float speed = 10.0f;
    public float attackRange = 3f;
    public float chaseRange = 10f;
    public float idleRange = 5f; // New parameter for idle range

    private Transform player;
    private Rigidbody rb;
    private Boss boss;
    private Animator animator;
    private BossIdle bossIdle;

    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        rb = animator.GetComponent<Rigidbody>();
        boss = animator.GetComponent<Boss>();
        bossIdle = animator.GetComponent<BossIdle>();
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (player == null || rb == null || animator == null || bossIdle == null)
        {
            Debug.LogError("Null reference detected.");
            return;
        }

        boss.LookAtPlayer();
        float distanceToPlayer = Vector3.Distance(animator.transform.position, player.position);

        if (distanceToPlayer <= chaseRange)
        {
            Vector3 target = new Vector3(player.position.x, rb.position.y, rb.position.z);
            Vector3 newPos = Vector3.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);

            if (distanceToPlayer < attackRange)
            {
                TriggerAttack(animator);
            }
            else if (distanceToPlayer > chaseRange)
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
