using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossVisual_Melee : MonoBehaviour
{
    private Enemy_Melee enemy;

    [SerializeField] private ParticleSystem landingEffect;

    [SerializeField] private ParticleSystem recoveryEffect;

    [SerializeField] private TrailRenderer rightHandTrail;
    [SerializeField] private TrailRenderer leftHandTrail;

    [SerializeField] private List<Transform> spawnStone = new List<Transform>();
    public float animationDuration = 1f;
    public float sinkDelay = 1f; 
    public float sinkSpeed = 1f;




    private void Awake()
    {
        enemy = GetComponentInParent<Enemy_Melee>();

        rightHandTrail.enabled = false;
        leftHandTrail.enabled = false;
        landingEffect.transform.parent = null;
        landingEffect.Stop();
        
    }

    public void PlaceLandingEffect()
    {
        landingEffect.transform.position = enemy.transform.position;
        landingEffect.Clear();
        landingEffect.Play();
    }

    public void PlaceRecoveryEffect()
    {
        recoveryEffect.transform.position = enemy.transform.position;
        recoveryEffect.Clear();
        recoveryEffect.Play();
    }

    public void StopRecoveryEffect()
    {
        recoveryEffect.Stop();
    }

    public void StartRightHandTrail()
    {
        rightHandTrail.enabled = true;
    }

    public void StopRightHandTrail()
    {
        rightHandTrail.enabled = false;
    }

    public void StartLeftHandTrail()
    {
        leftHandTrail.enabled = true;
    }

    public void StopLeftHandTrail()
    {
        leftHandTrail.enabled = false;
    }

    public virtual void SpawnStone()
    {
        float distanceInFront = 4f; // Set this to the distance you want

        for (int i = 0; i < spawnStone.Count; i++)
        {
            Vector3 baseSpawnPosition = enemy.transform.position + enemy.transform.forward * distanceInFront;
            Vector3 spawnPosition = baseSpawnPosition;

            StartCoroutine(AnimateStonePosition(spawnStone[i].transform, spawnPosition));

            distanceInFront += 1.5f;
        }
    }

   IEnumerator AnimateStonePosition(Transform stoneTransform, Vector3 targetPosition)
    {
        Vector3 initialPosition = stoneTransform.position;
        Vector3 targetXZPosition = new Vector3(targetPosition.x, initialPosition.y, targetPosition.z); // Preserve initial Y position
        float elapsedTime = 0f;


        // Ensure the stone reaches the target X and Z position precisely
        stoneTransform.position = targetXZPosition;

        // Animate Y position
        initialPosition = targetXZPosition; // Start Y animation from the new XZ position
        elapsedTime = 0f;

        while (elapsedTime < animationDuration)
        {
            float t = elapsedTime / animationDuration;
            stoneTransform.position = Vector3.Lerp(initialPosition, targetPosition, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure the stone reaches the target position precisely
        stoneTransform.position = targetPosition;

        // Delay before sinking the stone
        yield return new WaitForSeconds(sinkDelay);

        // Sink the stone
        StartCoroutine(SinkStone(stoneTransform));
    }

     IEnumerator SinkStone(Transform stoneTransform)
{
    Vector3 initialPosition = stoneTransform.position;
    Vector3 targetPosition = new Vector3(initialPosition.x, initialPosition.y - 1f, initialPosition.z); // Sink 1 unit along the Z-axis

    while (stoneTransform.position.z > targetPosition.z)
    {
        float step = 4f; // Adjust this value to control the rate of sinking
        stoneTransform.position -= new Vector3(0, 0, step * Time.deltaTime);
        yield return null;
    }

    // Ensure the stone reaches the final sunk position precisely
    stoneTransform.position = targetPosition;
}

   
}
