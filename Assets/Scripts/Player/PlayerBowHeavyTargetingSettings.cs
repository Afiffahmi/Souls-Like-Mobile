using UnityEngine;

/// <summary>Shared tuning for bow heavy ground shots and charged volleys.</summary>
[System.Serializable]
public sealed class BowHeavyTargetingSettings
{
    [Tooltip("Horizontal enemy detection radius and maximum arrow landing distance from the player, in metres. Also limits single heavy shots.")]
    [Min(0.1f)] public float attackRadius = 5f;
    [Tooltip("Ground blast and lingering field radius around each arrow's impact, in metres.")]
    [Min(0.1f)] public float impactRadius = 4f;
    [Tooltip("Duration, damage per second, tick interval, and elemental ground effect prefabs.")]
    public ElementalGems.GemGroundFieldSettings groundField;
    [Tooltip("Minimum horizontal distance between an enemy root and a charged arrow landing point.")]
    [Min(0.1f)] public float minOffset = 0.75f;
    [Tooltip("Maximum random landing offset from the selected enemy. Increase for larger enemies.")]
    [Min(0.1f)] public float maxOffset = 2f;
    [Tooltip("Extra clearance around every enemy collider's footprint.")]
    [Min(0.05f)] public float enemyClearance = 0.25f;
    [Tooltip("Minimum distance between the three landing points in a charged volley.")]
    [Min(0.05f)] public float pointSeparation = 0.35f;
    [Tooltip("Terrain/floor layers. Player and enemy colliders are also filtered by component.")]
    public LayerMask groundLayers = ~((1 << 3) | (1 << 7));
    [Min(0.1f)] public float groundProbeHeight = 10f;
    [Min(0.1f)] public float groundProbeDepth = 30f;
}

public partial class PlayerStateManager
{
    public BowHeavyTargetingSettings bowHeavyTargeting = new BowHeavyTargetingSettings();
    [SerializeField, HideInInspector] private bool bowHeavyTargetingInitialized;

    // Called by the Inspector and at runtime so unopened legacy scenes/prefabs retain their tuning.
    public void InitializeBowHeavyTargeting()
    {
        if (bowHeavyTargetingInitialized) return;
        var shooter = GetComponent<PlayerBowShooter>();
        if (shooter == null) return;
        if (bowHeavyTargeting == null) bowHeavyTargeting = new BowHeavyTargetingSettings();
        shooter.CopyLegacyHeavyTargeting(bowHeavyTargeting);
        bowHeavyTargetingInitialized = true;
    }
}
