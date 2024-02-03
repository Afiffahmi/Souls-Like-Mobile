using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossIdle : MonoBehaviour
{
    private Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void TransitionToIdle()
    {
        animator.SetBool("isRunning", true);
        animator.ResetTrigger("LightAttack");
        animator.SetBool("isRunning", false);
    }
}


