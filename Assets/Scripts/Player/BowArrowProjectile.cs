using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Straight, constant-speed flight with a swept tip test and no gravity.</summary>
[DisallowMultipleComponent]
public sealed class BowArrowProjectile : MonoBehaviour
{
    [Tooltip("Distance from the projectile origin to the arrow tip along local +Z.")]
    [Min(0f)] public float tipOffset = 0.4f;
    [Min(0.001f)] public float hitRadius = 0.015f;
    [Tooltip("Lifetime after world/normal-arrow impacts. Gem arrows lodged in enemies remain until a different arrow gem hits.")]
    [Min(0f)] public float impactLifetime = 2f;

    private Transform owner;
    private Enemy specialTarget;
    private bool specialTargeted;
    private Vector3 direction;
    private float speed;
    private float remainingLifetime;
    private LayerMask hitLayers;
    private bool flying;
    private bool groundShot, groundPointFound;
    private Vector3 groundPoint;
    private float remainingDistance;
    private RaycastHit[] flightHits = new RaycastHit[16];
    public bool IsFlying => flying;
    public bool IsGroundShot => groundShot;
    public Vector3 GroundPoint => groundPoint;

    public void Launch(Transform source, Vector3 forward, float flightSpeed, float lifetime, LayerMask layers)
    {
        groundShot = specialTargeted = false;
        specialTarget = null;
        BeginFlight(source, forward, flightSpeed, lifetime, layers);
        Advance(0f);
    }

    /// <summary>Only the assigned enemy can intercept a special low arrow; world geometry still blocks it.</summary>
    public void LaunchSpecialTargeted(Transform source, Enemy target, Vector3 forward, float flightSpeed, float lifetime, LayerMask layers)
    {
        groundShot = false;
        specialTargeted = true;
        specialTarget = target;
        BeginFlight(source, forward, flightSpeed, lifetime, layers);
        Advance(0f);
    }

    public void LaunchAtGround(Transform source, Vector3 point, bool foundGround, float flightSpeed, float lifetime, LayerMask layers)
    {
        groundShot = true;
        specialTargeted = false;
        specialTarget = null;
        groundPoint = point;
        groundPointFound = foundGround;
        Vector3 delta = point - transform.position;
        Vector3 forward = delta.sqrMagnitude > 0.000001f ? delta.normalized : Vector3.down;
        remainingDistance = Mathf.Max(0f, delta.magnitude - tipOffset);
        // Give even a slow configured projectile enough time to reach its chosen ground point.
        BeginFlight(source, forward, flightSpeed, Mathf.Max(lifetime, remainingDistance / Mathf.Max(0.1f, flightSpeed) + 0.1f), layers);
        Advance(0f);
    }

    private void BeginFlight(Transform source, Vector3 forward, float flightSpeed, float lifetime, LayerMask layers)
    {
        owner = source;
        direction = forward.normalized;
        speed = Mathf.Max(0.1f, flightSpeed);
        remainingLifetime = Mathf.Max(0.1f, lifetime);
        hitLayers = layers;
        flying = true;
    }

    private void Update()
    {
        if (!flying) return;
        float dt = Mathf.Min(Time.deltaTime, remainingLifetime);
        Advance(speed * dt);
        remainingLifetime -= dt;
        if (flying && remainingLifetime <= 0f) Destroy(gameObject);
    }

    private void Advance(float distance)
    {
        if (!flying) return;
        if (groundShot) distance = Mathf.Min(distance, remainingDistance);
        float nearestDistance = float.PositiveInfinity;
        bool hitSomething = false;
        RaycastHit nearestHit = default;
        var physics = gameObject.scene.GetPhysicsScene();
        int count;
        while ((count = physics.SphereCast(transform.position, hitRadius, direction, flightHits,
            distance + tipOffset, hitLayers, QueryTriggerInteraction.Ignore)) == flightHits.Length)
            System.Array.Resize(ref flightHits, flightHits.Length * 2);
        for (int i = 0; i < count; i++)
        {
            var hit = flightHits[i];
            if (hit.collider.transform.IsChildOf(transform) ||
                (owner != null && hit.collider.transform.IsChildOf(owner))) continue;
            // Heavy attacks land below characters rather than stopping at their chest collider.
            if (groundShot && IsCharacterCollider(hit.collider)) continue;
            // Keep the 1/1/1 or 2/1 distribution when another enemy stands in front.
            if (specialTargeted && IsCharacterCollider(hit.collider) &&
                (specialTarget == null || hit.collider.GetComponentInParent<Enemy>() != specialTarget)) continue;
            if (hit.distance >= nearestDistance) continue;
            nearestDistance = hit.distance;
            nearestHit = hit;
            hitSomething = true;
        }

        if (hitSomething)
        {
            transform.position += direction * Mathf.Max(0f, nearestDistance - tipOffset);
            StopFlight(true, nearestHit.collider, nearestHit.point, nearestHit.normal);
            return;
        }
        transform.position += direction * distance;
        if (groundShot)
        {
            remainingDistance = Mathf.Max(0f, remainingDistance - distance);
            if (remainingDistance <= 0f)
            {
                transform.position = groundPoint - direction * tipOffset;
                StopFlight(groundPointFound);
            }
        }
    }

    public static bool IsCharacterCollider(Collider collider) => collider is CharacterController ||
        collider.GetComponentInParent<PlayerStateManager>() != null ||
        collider.GetComponentInParent<LockOnTarget>() != null || collider.GetComponentInParent<Enemy>() != null;

    private void StopFlight(bool hit, Collider collider = null, Vector3? contact = null, Vector3? normal = null)
    {
        flying = false;
        // Attach before damage applies knockback so the arrow follows the struck object.
        // Preserve its impact pose; ground shots retain their world-space landing point.
        if (hit && !groundShot && collider != null)
            transform.SetParent(collider.transform, true);
        var payload = GetComponent<ElementalGems.GemArrowPayload>();
        if (hit) payload?.Impact(collider, contact ?? groundPoint, normal ?? Vector3.up, groundShot);
        name = hit ? "Arrow (Hit)" : "Arrow (Range End)";
        // Embedded gem arrows and their tip effects live with the enemy, without a timer.
        if (Application.isPlaying && (payload == null || !payload.IsEmbeddedInEnemy))
            Destroy(gameObject, hit ? impactLifetime : 0f);
    }
}
