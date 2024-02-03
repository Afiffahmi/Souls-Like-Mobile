/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossWeapon : MonoBehaviour
{
    public int attackDamage =20;
    public int enragedAttackDamage = 40;

    public float attackRange = 1f;
    public Vector3 attackOffset;
    public LayerMask attackMask;

    public void Attack()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position + transform.TransformVector(attackOffset), attackRange, attackMask);
        if (hits.Length > 0)
        {
            hits[0].GetComponent<PlayerHealth>().TakeDamage(attackDamage);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
*/
 