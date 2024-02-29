using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StoneDamage : MonoBehaviour
{
    BoxCollider boxCollider;
    GameObject player;
    public Animator anim;
    public Player playerHP;
    void Start()
    {
        
    }

    // Update is called once per frame
    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            anim.ResetTrigger("Idle");
            anim.ResetTrigger("Running");
            anim.SetTrigger("Hit");
            playerHP.TakeDamage(4);
        }
    }

}
