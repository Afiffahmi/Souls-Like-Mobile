using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Launches one arrow on the bow timeline's release, after the bow pose updates.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStateManager), typeof(PlayerBowVisuals))]
[DefaultExecutionOrder(100)]
public sealed class PlayerBowShooter : MonoBehaviour
{
    public BowArrowProjectile arrowPrefab;
    [Tooltip("Optional custom launch point. Otherwise uses the animated bow model's origin.")]
    public Transform launchPoint;
    [Tooltip("Offset in the launch point or bow model's local coordinates.")]
    public Vector3 launchOffset;
    [Min(0.1f)] public float arrowSpeed = 30f;
    [Min(0.1f)] public float arrowLifetime = 5f;
    public LayerMask hitLayers = ~0;

    [Header("Heavy attack ground targeting")]
    [Tooltip("Maximum horizontal landing distance measured from the player. A closer locked enemy uses its own ground position.")]
    [Min(0.1f)] public float heavyGroundDistance = 5f;
    [Tooltip("Terrain/floor layers. Enemy and player colliders are also filtered by component.")]
    public LayerMask heavyGroundLayers = ~((1 << 3) | (1 << 7));
    [Min(0.1f)] public float groundProbeHeight = 10f;
    [Min(0.1f)] public float groundProbeDepth = 30f;

    [Header("Elemental damage (shared GemManager)")] public float arrowDamage = 18f, heavyArrowDamage = 30f;
    [Tooltip("Ground blast and lingering field radius in metres. Try 3 to 5.")]
    [Min(.1f)] public float heavyImpactRadius = 4f;
    [Tooltip("Duration, damage per second, tick interval, and elemental ground effect prefabs.")]
    public ElementalGems.GemGroundFieldSettings heavyGroundField;
    private PlayerStateManager player;
    private PlayerBowVisuals visuals;
    private PlayerLockOn lockOn;
    private RaycastHit[] groundHits = new RaycastHit[16];
    private struct ReleaseRequest
    {
        public Vector3 direction, groundPoint;
        public bool heavy, foundGround;
        public ElementalGems.GemAttack gem;
        public ElementalGems.GemManager owner;
        public ElementalGems.GroundFieldSnapshot field;
    }
    private readonly Queue<ReleaseRequest> pendingReleases = new Queue<ReleaseRequest>();
    public event System.Action<BowArrowProjectile> ArrowSpawned;

    private void Awake()
    {
        player = GetComponent<PlayerStateManager>();
        visuals = GetComponent<PlayerBowVisuals>();
        lockOn = GetComponent<PlayerLockOn>();
    }

    private void OnEnable() => player.BowReleased += QueueArrow;

    private void OnDisable()
    {
        player.BowReleased -= QueueArrow;
        pendingReleases.Clear();
    }

    private void QueueArrow()
    {
        var request = new ReleaseRequest { direction = player.transform.forward.normalized, heavy = player.IsBowHeavyAttacking };
        request.owner = GetComponent<ElementalGems.GemManager>();
        request.gem = request.owner != null ? request.owner.Capture() : new ElementalGems.GemAttack(null);
        request.field = request.heavy && heavyGroundField != null ? heavyGroundField.Capture(request.gem.element, heavyImpactRadius) : null;
        // Snapshot each release separately; later target movement cannot redirect an arrow in flight.
        if (request.heavy) request.foundGround = TryGetHeavyGroundPoint(out request.groundPoint);
        pendingReleases.Enqueue(request);
    }

    private void LateUpdate()
    {
        while (pendingReleases.Count > 0) SpawnArrow(pendingReleases.Dequeue());
    }

    private void SpawnArrow(ReleaseRequest request)
    {
        if (arrowPrefab == null || !visuals.isActiveAndEnabled) return;
        Transform origin = launchPoint != null ? launchPoint : visuals.BowModel;
        if (origin == null) return;
        Vector3 start = origin.TransformPoint(launchOffset);
        Vector3 releaseDirection = request.heavy ? (request.groundPoint - start).normalized : request.direction;
        if (releaseDirection.sqrMagnitude < 0.0001f) releaseDirection = Vector3.down;
        var arrow = Instantiate(arrowPrefab, start,
            Quaternion.LookRotation(releaseDirection, Mathf.Abs(releaseDirection.y) > 0.999f ? Vector3.forward : Vector3.up));
        arrow.name = "Arrow (Flying)";
        var payload = arrow.GetComponent<ElementalGems.GemArrowPayload>() ?? arrow.gameObject.AddComponent<ElementalGems.GemArrowPayload>();
        payload.Initialize(request.gem, request.owner, request.heavy ? heavyArrowDamage : arrowDamage, heavyImpactRadius, request.field);
        float elementalSpeed = arrowSpeed * request.gem.projectileSpeed;
        if (request.heavy)
            arrow.LaunchAtGround(player.transform, request.groundPoint, request.foundGround, elementalSpeed, arrowLifetime, hitLayers);
        else
            arrow.Launch(player.transform, releaseDirection, elementalSpeed, arrowLifetime, hitLayers);
        ArrowSpawned?.Invoke(arrow);
    }

    public bool TryGetHeavyGroundPoint(out Vector3 point)
    {
        Vector3 playerPosition = transform.position;
        Vector3 planar = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * Mathf.Max(0.1f, heavyGroundDistance);
        float referenceHeight = playerPosition.y;
        if (lockOn != null && lockOn.isActiveAndEnabled && lockOn.IsLockedOn && lockOn.CurrentTarget.IsAvailable)
        {
            // Use the enemy root, never the chest/head aim point used by the lock-on camera.
            Vector3 targetPosition = lockOn.CurrentTarget.transform.position;
            planar = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(targetPosition - playerPosition, Vector3.up),
                Mathf.Max(0.1f, heavyGroundDistance));
            referenceHeight = Mathf.Max(referenceHeight, targetPosition.y);
        }
        point = playerPosition + planar;
        Vector3 probe = point;
        probe.y = referenceHeight + Mathf.Max(0.1f, groundProbeHeight);
        var physics = gameObject.scene.GetPhysicsScene();
        int count;
        // Do not lose the floor under a pile of colliders when the buffer fills.
        while ((count = physics.Raycast(probe, Vector3.down, groundHits,
            probe.y - playerPosition.y + Mathf.Max(0.1f, groundProbeDepth), heavyGroundLayers,
            QueryTriggerInteraction.Ignore)) == groundHits.Length)
            System.Array.Resize(ref groundHits, groundHits.Length * 2);
        float closest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.distance >= closest || hit.normal.y <= 0.1f || hit.collider.transform.IsChildOf(transform) ||
                BowArrowProjectile.IsCharacterCollider(hit.collider)) continue;
            closest = hit.distance;
            point = hit.point;
        }
        // No floor (e.g. a pit): retain a bounded destination, without claiming a ground impact.
        return closest < float.PositiveInfinity;
    }
}
