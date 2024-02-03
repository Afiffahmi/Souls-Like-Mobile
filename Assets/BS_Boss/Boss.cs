using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boss : MonoBehaviour
{
    public Transform player;
    public float speed = 4.0f;
    float chaseRange = 18f;
    private BossIdle bossIdle;
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        if (!player)
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
        }
    }

    void Update()
    {
        bossIdle = animator.GetComponent<BossIdle>();
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= chaseRange)
        {
            animator.SetBool("isRunning", true);
            animator.SetBool("Idle", false);
            LookAtPlayer();
            MoveTowardsPlayer();
            
            
        }else {
            animator.SetBool("isRunning", false);
            animator.SetBool("Idle", true);
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
        Vector3 direction = (player.position - transform.position).normalized;
        transform.position += direction * speed * Time.deltaTime;
    }
}