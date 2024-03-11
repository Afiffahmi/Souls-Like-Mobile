using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAnimationEvent : MonoBehaviour
{
    private Enemy enemy;
    private Enemy_Melee enemyMelee;

    private void Awake()
    {
        enemy = GetComponentInParent<Enemy>();
    }

    public void AnimationTrigger()
    {
        enemy.AnimationTrigger();
    }

    public void StartManualMovement(){
        enemy.ActiveManualMovement(true);
    }

    public void EndManualMovement(){
        enemy.ActiveManualMovement(false);
    }

    public void StartManualRotation(){
        enemy.ActiveManualRotation(true);
    }

    public void EndManualRotation(){
        enemy.ActiveManualRotation(false);
    }

    public void BossJumpImpact(){
        if(enemyMelee == null){
            enemyMelee = GetComponentInParent<Enemy_Melee>();
        }

        enemyMelee.JumpImpact();
    }
    

}
