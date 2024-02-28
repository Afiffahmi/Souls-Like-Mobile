using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossVisual_Melee : MonoBehaviour
{
 private Enemy_Melee enemy;

 [SerializeField] private ParticleSystem landingEffect;

    private void Awake() {
        enemy = GetComponentInParent<Enemy_Melee>();

        landingEffect.transform.parent = null;
        landingEffect.Stop();
    }


    public void PlaceLandingEffect(Vector3 target){
        landingEffect.transform.position = target;
        landingEffect.Clear();

        landingEffect.Play();
    }   
}
