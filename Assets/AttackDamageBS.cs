using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackDamageBS : MonoBehaviour
{
    BoxCollider boxCollider;
    public ParticleSystem hitParticle1,hitParticle2;
    GameObject player;
    public Player playerHP;
    public Animator anim;
    public BoxCollider LeftArm, RightArm;

    void Start()
    {
        hitParticle1.Stop();
        hitParticle2.Stop();
        player = GameObject.FindWithTag("Player");
        LeftArm.enabled = false;
        RightArm.enabled = false;
    }

    void EnableSwingAttack()
    {
        LeftArm.enabled = true;
        Debug.Log("Swing");
        RightArm.enabled = true;
    }

    void EnableHeavyAttack()
    {
        LeftArm.enabled = true;
        Debug.Log("Heavy");
        RightArm.enabled = true;
    }
    

    void EnableLightAttack()
    {
        LeftArm.enabled = true;
        Debug.Log("Light");
        RightArm.enabled = true;
    }
    void EnableJumpAttack()
    {
        LeftArm.enabled = true;
        Debug.Log("Light");
        RightArm.enabled = true;
    }

    void DisableSwingAttack()
    {
        LeftArm.enabled = false;
        RightArm.enabled = false;
    }
    void DisableJumpAttack()
    {
        LeftArm.enabled = false;
        RightArm.enabled = false;
    }
    void DisableHeavyAttack()
    {
        LeftArm.enabled = false;
        RightArm.enabled = false;
    }

    void DisableLightAttack()
    {
        LeftArm.enabled = false;
        RightArm.enabled = false;
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            anim.ResetTrigger("Idle");
            anim.ResetTrigger("Running");
            anim.SetTrigger("Hit");
            playerHP.TakeDamage(4);
            hitParticle1.Play();
            hitParticle2.Play();
        }
    }



}
