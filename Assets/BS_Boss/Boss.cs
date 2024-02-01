using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boss : MonoBehaviour
{
    public Transform player;

    public void Update()
    {
        LookAtPlayer();
    }

    public void LookAtPlayer()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0; // This line prevents the boss from tilting upwards/downwards.

        Quaternion rotation = Quaternion.LookRotation(direction);
        transform.rotation = rotation;
    }
}