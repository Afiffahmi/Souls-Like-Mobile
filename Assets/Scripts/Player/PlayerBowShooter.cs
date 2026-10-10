using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Launches one arrow on the bow timeline's release, after the bow pose updates.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStateManager), typeof(PlayerBowVisuals))]
[DefaultExecutionOrder(100)]
public sealed partial class PlayerBowShooter : MonoBehaviour
{
    public BowArrowProjectile arrowPrefab;
    [Tooltip("Optional custom launch point. Otherwise uses the animated bow model's origin.")]
    public Transform launchPoint;
    [Tooltip("Offset in the launch point or bow model's local coordinates.")]
    public Vector3 launchOffset;
    [Min(0.1f)] public float arrowSpeed = 30f;
    [Min(0.1f)] public float arrowLifetime = 5f;
    public LayerMask hitLayers = ~0;

    [HideInInspector] public float arrowDamage = 18f, heavyArrowDamage = 30f; // Legacy attack default migration.
    // Keep serialized values from existing scenes/prefabs for one-time migration.
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("heavyGroundDistance")]
    private float legacy_heavyGroundDistance = 5f;
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("heavyGroundLayers")]
    private LayerMask legacy_heavyGroundLayers = ~((1 << 3) | (1 << 7));
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("groundProbeHeight")]
    private float legacy_groundProbeHeight = 10f;
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("groundProbeDepth")]
    private float legacy_groundProbeDepth = 30f;
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("heavyImpactRadius")]
    private float legacy_heavyImpactRadius = 4f;
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("heavyGroundField")]
    private ElementalGems.GemGroundFieldSettings legacy_heavyGroundField = null;
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("volleyMinOffset")]
    private float legacy_volleyMinOffset = 0.75f;
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("volleyMaxOffset")]
    private float legacy_volleyMaxOffset = 2f;
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("volleyEnemyClearance")]
    private float legacy_volleyEnemyClearance = 0.25f;
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("volleyPointSeparation")]
    private float legacy_volleyPointSeparation = 0.35f;

    private BowHeavyTargetingSettings HeavySettings
    {
        get
        {
            if (player == null) player = GetComponent<PlayerStateManager>();
            player.InitializeBowHeavyTargeting();
            return player.bowHeavyTargeting;
        }
    }

    // Compatibility accessors: the State Manager is the only editable source.
    public float heavyGroundDistance { get => HeavySettings.attackRadius; set => HeavySettings.attackRadius = value; }
    public LayerMask heavyGroundLayers { get => HeavySettings.groundLayers; set => HeavySettings.groundLayers = value; }
    public float groundProbeHeight { get => HeavySettings.groundProbeHeight; set => HeavySettings.groundProbeHeight = value; }
    public float groundProbeDepth { get => HeavySettings.groundProbeDepth; set => HeavySettings.groundProbeDepth = value; }
    public float heavyImpactRadius { get => HeavySettings.impactRadius; set => HeavySettings.impactRadius = value; }
    public ElementalGems.GemGroundFieldSettings heavyGroundField { get => HeavySettings.groundField; set => HeavySettings.groundField = value; }
    public float volleyMinOffset { get => HeavySettings.minOffset; set => HeavySettings.minOffset = value; }
    public float volleyMaxOffset { get => HeavySettings.maxOffset; set => HeavySettings.maxOffset = value; }
    public float volleyEnemyClearance { get => HeavySettings.enemyClearance; set => HeavySettings.enemyClearance = value; }
    public float volleyPointSeparation { get => HeavySettings.pointSeparation; set => HeavySettings.pointSeparation = value; }

    internal void CopyLegacyHeavyTargeting(BowHeavyTargetingSettings settings)
    {
        settings.attackRadius = legacy_heavyGroundDistance;
        settings.groundLayers = legacy_heavyGroundLayers;
        settings.groundProbeHeight = legacy_groundProbeHeight;
        settings.groundProbeDepth = legacy_groundProbeDepth;
        settings.impactRadius = legacy_heavyImpactRadius;
        settings.groundField = legacy_heavyGroundField;
        settings.minOffset = legacy_volleyMinOffset;
        settings.maxOffset = legacy_volleyMaxOffset;
        settings.enemyClearance = legacy_volleyEnemyClearance;
        settings.pointSeparation = legacy_volleyPointSeparation;
    }
    private PlayerStateManager player;
    private PlayerBowVisuals visuals;
    private PlayerLockOn lockOn;
    private RaycastHit[] groundHits = new RaycastHit[16];
    private struct ReleaseRequest
    {
        public Vector3 direction, groundPoint;
        public bool heavy, foundGround, special, highSpecial;
        public int specialSequence;
        public BowSpecialAttackSettings specialSettings;
        public float damage, knockbackDuration;
        public ElementalGems.GemAttack gem;
        public ElementalGems.GemManager owner;
        public ElementalGems.GroundFieldSnapshot field;
    }
    private readonly Queue<ReleaseRequest> pendingReleases = new Queue<ReleaseRequest>();
    public event System.Action<BowArrowProjectile> ArrowSpawned;
    public event System.Action<BowSpecialWave> SpecialWaveSpawned;

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
        specialTargetUses.Clear();
        specialTargetSequence = -1;
    }

    private void QueueArrow()
    {
        var request = new ReleaseRequest { direction = player.transform.forward.normalized, heavy = player.IsBowHeavyAttacking };
        var stats = player.CurrentWeaponAttackStats;
        var defaults = player.CurrentAttackDefaults ?? player.CaptureAttackDefaults(PlayerCombatMode.Bow,request.heavy?CombatAttackInput.HeavyAttack:CombatAttackInput.LightAttack);
        request.knockbackDuration = stats.KnockbackDurationMultiplier * defaults.knockbackDurationScale;
        request.damage = stats.Damage(defaults.damage);
        request.special = player.IsBowSpecialAttacking;
        request.specialSequence = player.BowSpecialSequence;
        request.highSpecial = request.special && player.CurrentBowSpecialDamagePhase == BowSpecialDamagePhase.High;
        if (request.special) { request.damage = player.CurrentBowSpecialDamage; request.specialSettings = player.CurrentBowSpecialSettings; }
        request.owner = GetComponent<ElementalGems.GemManager>();
        // Light shots earn gem powers only after a full hold; heavy shots keep their own rules.
        bool useGem = request.special || request.heavy || player.IsBowLightGemCharged;
        request.gem = useGem && request.owner != null ? request.owner.Capture() : new ElementalGems.GemAttack(null);
        if (request.special) request.gem = request.gem.WithKnockbackMultiplier(request.highSpecial ? request.specialSettings.highKnockbackMultiplier : request.specialSettings.lowKnockbackMultiplier, request.specialSettings.baseKnockback);
        request.field = request.heavy && heavyGroundField != null ? heavyGroundField.Capture(request.gem.element, heavyImpactRadius, stats.DamageMultiplier, request.knockbackDuration) : null;
        // Snapshot each release separately; later target movement cannot redirect an arrow in flight.
        if (request.heavy && player.IsBowHeavyChargedAttack)
        {
            if (!TryGetChargedVolleyPoint(player.BowHeavyArrowsReleased - 1, out request.groundPoint))
            {
                Debug.LogWarning("[Bow] No safe ground near the volley target. Skipping this arrow instead of aiming directly at an enemy.", this);
                return;
            }
            request.foundGround = true;
        }
        else if (request.heavy) request.foundGround = TryGetHeavyGroundPoint(out request.groundPoint);
        pendingReleases.Enqueue(request);
    }

    private void LateUpdate()
    {
        while (pendingReleases.Count > 0) SpawnArrow(pendingReleases.Dequeue());
    }

    private void SpawnArrow(ReleaseRequest request)
    {
        if (request.special && (!player.IsBowSpecialAttacking || player.BowSpecialSequence != request.specialSequence)) return;
        if (arrowPrefab == null || !visuals.isActiveAndEnabled) return;
        Transform origin = launchPoint != null ? launchPoint : visuals.BowModel;
        if (origin == null) return;
        Vector3 start = origin.TransformPoint(launchOffset);
        if (request.highSpecial)
        {
            // Aim at the lock when available; otherwise release forward instead of dropping the shot.
            request.direction = GetSpecialHighDirection(start);
            var waveObject = new GameObject("Bow Special (Piercing Force)");
            SceneManager.MoveGameObjectToScene(waveObject, gameObject.scene);
            waveObject.transform.position = start;
            var wave = waveObject.AddComponent<BowSpecialWave>();
            wave.Initialize(player.transform, request.owner, request.gem, request.damage, request.knockbackDuration, request.specialSettings, request.direction, hitLayers);
            SpecialWaveSpawned?.Invoke(wave);
            return;
        }
        Vector3 releaseDirection = request.heavy ? (request.groundPoint - start).normalized : request.direction;
        Enemy specialTarget = null;
        if (request.special)
        {
            specialTarget = SelectSpecialLowTarget(start, request, out var aim);
            if (specialTarget != null)
            {
                FaceUnlockedSpecialTarget(aim);
                // Rotating the body also moves the animated bow socket. Spawn from its new position.
                start = origin.TransformPoint(launchOffset);
                releaseDirection = (aim - start).normalized;
            }
        }
        if (releaseDirection.sqrMagnitude < 0.0001f) releaseDirection = Vector3.down;
        var arrow = Instantiate(arrowPrefab, start,
            Quaternion.LookRotation(releaseDirection, Mathf.Abs(releaseDirection.y) > 0.999f ? Vector3.forward : Vector3.up));
        SceneManager.MoveGameObjectToScene(arrow.gameObject, gameObject.scene);
        arrow.name = request.special ? "Arrow (Special Low)" : "Arrow (Flying)";
        var payload = arrow.GetComponent<ElementalGems.GemArrowPayload>() ?? arrow.gameObject.AddComponent<ElementalGems.GemArrowPayload>();
        payload.Initialize(request.gem, request.owner, request.damage, heavyImpactRadius, request.field, request.knockbackDuration);
        float elementalSpeed = arrowSpeed * request.gem.projectileSpeed;
        if (request.heavy)
            arrow.LaunchAtGround(player.transform, request.groundPoint, request.foundGround, elementalSpeed, arrowLifetime, hitLayers);
        else if (specialTarget != null)
            arrow.LaunchSpecialTargeted(player.transform, specialTarget, releaseDirection, elementalSpeed, arrowLifetime, hitLayers);
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
        return TryProjectHeavyGround(point, referenceHeight, out point);
    }

    private bool TryProjectHeavyGround(Vector3 desired, float referenceHeight, out Vector3 point)
    {
        Vector3 playerPosition = transform.position;
        point = desired;
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
