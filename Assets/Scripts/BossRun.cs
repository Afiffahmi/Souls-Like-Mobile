//BossRun.cs

using UnityEngine;
using System.Collections;
using UnityEngine.AI;


public class BossRun : StateMachineBehaviour
{
    public float speed = 10.0f;
    public float attackRange = 3f;
    public float chaseRange = 10f;
    public float idleRange = 5f;

    public float lightAttackCooldown = 1.5f;
    public float heavyAttackCooldown = 3.0f;
    public float rangeAttackCooldown = 2.0f;

    private Transform player;
    private Rigidbody rb;
    private Boss boss;
    private Vector3 rangeAttackStartPosition; // Store the position before range attack
    private Vector3 rangeAttackTargetPosition;

    private float lightAttackTimer = 0f;
    private float heavyAttackTimer = 0f;
    private float rangeAttackTimer = 0f;

    private bool isBlocking = false;
    private float blockingStartTime = 1f;
    private float blockingDuration = 10f;
    

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        rb = animator.GetComponent<Rigidbody>();
        boss = animator.GetComponent<Boss>();
        rangeAttackStartPosition = rb.position;
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (player == null || rb == null || boss == null)
        {
            Debug.LogError("Missing components");
            return;
        }

        // Update cooldown timers
        lightAttackTimer -= Time.deltaTime;
        heavyAttackTimer -= Time.deltaTime;
        rangeAttackTimer -= Time.deltaTime;

        float distanceToPlayer = Vector3.Distance(animator.transform.position, player.position);

        if (distanceToPlayer <= chaseRange)
        {
            boss.LookAtPlayer();

            if (distanceToPlayer < attackRange && !isBlocking && distanceToPlayer > 2f)
            {
                RandomAttack(animator);
            }
            else if (distanceToPlayer > chaseRange)
            {
                animator.SetBool("Idle", true);
            }
            else if(!isBlocking && ShouldStartBlocking(animator))
            {
                StartBlocking(animator);
            }
            
            
        }
        else if (distanceToPlayer <= idleRange)
        {
            animator.SetBool("Idle", true);
        }

        // Check if the blocking duration has elapsed
        if (isBlocking && Time.time - blockingStartTime >= blockingDuration)
        {
            StopBlocking(animator);
        }
    }

    void RandomAttack(Animator animator)
{
    if (lightAttackTimer <= 0f)
    {
        lightAttackTimer = lightAttackCooldown;
        animator.SetTrigger("LightAttack");
        Debug.Log("LightAttack");
    }
    else if (heavyAttackTimer <= 0f)
    {
        heavyAttackTimer = heavyAttackCooldown;
        animator.SetTrigger("HeavyAttack");
        Debug.Log("HeavyAttack");
    }
    else if (rangeAttackTimer <= 0f)
    {
        rangeAttackTimer = rangeAttackCooldown;
        animator.SetTrigger("RangeAttack");
        RangeAttack(animator);
    }
    // Add more conditions for additional attack types with their respective cooldowns
}
        public  NavMeshAgent navMeshAgent;
    void RangeAttack(Animator animator)
    {
        
        animator.SetTrigger("RangeAttack");
        rangeAttackStartPosition = rb.position; // Update the position before range attack
        rangeAttackTargetPosition = navMeshAgent.destination; // Set the target position 10 units away from the player
        UpdateRangeAttackPosition();
        Debug.Log("RangeAttack");
    }

    void UpdateRangeAttackPosition()
    {
        rb.position = rangeAttackTargetPosition; // Set boss position directly to the target position
    }


    private bool ShouldStartBlocking(Animator animator)
    {
        float distanceToPlayer = Vector3.Distance(animator.transform.position, player.position);
        return distanceToPlayer < 1f;
    }

    private void StartBlocking(Animator animator)
    {
        isBlocking = true;
        blockingStartTime = Time.time;
        animator.SetBool("isBlocking", true);
    }

    private void StopBlocking(Animator animator)
    {
        isBlocking = false;
        animator.SetBool("isBlocking", false);
    }

    public void TakeDamage(int damage)
    {
        if (!isBlocking)
        {
            // Handle damage normally
        }
        else
        {
            // Boss is blocking, reduce or negate damage
        }
    }
}
