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
        player = GameObject.FindWithTag("Player");
        LeftArm.enabled = false;
        RightArm.enabled = false;
    }

    void EnableSwingAttack()
    {
        LeftArm.enabled = true;
        RightArm.enabled = true;
    }

    void EnableLightAttack()
    {
        LeftArm.enabled = true;
        RightArm.enabled = true;
    }

    void DisableSwingAttack()
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
