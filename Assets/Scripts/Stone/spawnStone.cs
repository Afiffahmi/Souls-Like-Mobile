using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spawnStone : BossVisual_Melee
{
    private BossVisual_Melee bossVisual;
    public Animator animator;
    
    protected virtual void Awake()
    {
        bossVisual = GetComponentInParent<BossVisual_Melee>();
        animator = GetComponent<Animator>();

    }

    protected virtual void Start()
    {
        
    }

    // Update is called once per frame
    protected virtual void Update()
    {

    }

    public override void SpawnStone(){
        animator.SetBool("isSpawn", true);
        Debug.Log("Spawn Stone");
    }
}
