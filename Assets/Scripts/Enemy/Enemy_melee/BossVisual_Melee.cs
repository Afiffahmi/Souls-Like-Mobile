using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossVisual_Melee : MonoBehaviour
{
    private Enemy_Melee enemy;

    [SerializeField] private ParticleSystem landingEffect;
    [SerializeField] private List<Transform> spawnStone = new List<Transform>();

    private void Awake()
    {
        enemy = GetComponentInParent<Enemy_Melee>();

        landingEffect.transform.parent = null;
        landingEffect.Stop();
    }

    public void PlaceLandingEffect()
    {
        landingEffect.transform.position = enemy.transform.position;
        landingEffect.Clear();
        landingEffect.Play();
    }

    public void SpawnStone()
    {
        float distanceInFront = 4f; // Set this to the distance you want

        for (int i = 0; i < spawnStone.Count; i++)
        {
            Vector3 baseSpawnPosition = enemy.transform.position + enemy.transform.forward * distanceInFront;
            Vector3 spawnPosition = baseSpawnPosition;

            spawnStone[i].transform.position = spawnPosition;
            distanceInFront += 1.5f;
        }
    }

    private IEnumerator DestroyStone(Transform stone)
    {
        yield return new WaitForSeconds(2f);


    }
}
