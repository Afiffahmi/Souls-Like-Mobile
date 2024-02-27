using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackDamageBS : MonoBehaviour
{
    BoxCollider boxCollider;
    GameObject player;
    public Animator anim;
    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.FindWithTag("Player"); 
        boxCollider = GetComponentInChildren<BoxCollider>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void EnableSwingAttack()
    {
        boxCollider.enabled = true;

        // Find the player and apply damage
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            Player player = playerObject.GetComponent<Player>();
            if (player != null)
            {
                Debug.Log("kena swing");
                anim.ResetTrigger("Idle");
                anim.ResetTrigger("Running");
                anim.SetTrigger("Hit");
                CancelInvoke("EndCombo");
                player.TakeDamage(4);
            }

        }
    }

    void EnableLightAttack()
    {
        boxCollider.enabled = true;

        // Find the player and apply damage
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            Player player = playerObject.GetComponent<Player>();
            if (player != null)
            {
                Debug.Log("kena Light");
                anim.ResetTrigger("Idle");
                anim.ResetTrigger("Running");
                anim.SetTrigger("Hit");
                CancelInvoke("EndCombo");
                player.TakeDamage(4);
            }

        }
    }

    void DisableSwingAttack()
    {
        boxCollider.enabled = false;
    }
    void DisableLightAttack()
    {
        boxCollider.enabled = false;
    }
}
