using System.Collections.Generic;
using UnityEngine;

/// <summary>Attach to an enemy/object that the player may lock onto. No enemy AI is required.</summary>
[DisallowMultipleComponent]
public sealed class LockOnTarget : MonoBehaviour
{
    private static readonly HashSet<LockOnTarget> targets = new HashSet<LockOnTarget>();
    public static IEnumerable<LockOnTarget> ActiveTargets => targets;

    [Tooltip("Optional chest/head transform. Without one, Local Aim Offset is used.")]
    public Transform aimPoint;
    public Vector3 localAimOffset = new Vector3(0f, 1f, 0f);
    [Tooltip("Turn off when an enemy dies or should not be targeted.")]
    public bool targetable = true;

    public bool IsAvailable => targetable && isActiveAndEnabled;

    [Header("Lock-On Area (world metres)")]
    [Tooltip("Optional area centre. Defaults to the enemy root, independently of its aim point.")]
    public Transform areaCenter;
    [Tooltip("The player must be inside this sphere when pressing lock-on. No trigger collider is required.")]
    [Min(0.1f)] private float lockOnRadius = 12.7f;
    [Tooltip("An existing lock releases outside this larger sphere. Must be at least Lock On Radius.")]
    [Min(0.1f)] private float releaseRadius = 13f;
    private Enemy enemy;

    public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.TransformPoint(localAimOffset);
    public Vector3 AreaCenter => areaCenter != null ? areaCenter.position : transform.position;

    private void Awake()
    {
        enemy = GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            enemy.OnDeath += HandleEnemyDeath;
        }
    }

    private void OnDestroy()
    {
        if (enemy != null)
        {
            enemy.OnDeath -= HandleEnemyDeath;
        }
    }

    private void HandleEnemyDeath() => SetTargetable(false);

    /// <summary>Call on death/invalidation to remove the target from lock-on eligibility.</summary>
    public void SetTargetable(bool available) => targetable = available;

    public bool ContainsPlayer(Vector3 playerPosition, bool retainingLock = false)
    {
        float radius = Mathf.Max(0.1f, lockOnRadius);
        if (retainingLock) radius = Mathf.Max(radius, releaseRadius);
        return (playerPosition - AreaCenter).sqrMagnitude <= radius * radius;
    }

    private void OnValidate()
    {
        lockOnRadius = Mathf.Max(0.1f, lockOnRadius);
        releaseRadius = Mathf.Max(lockOnRadius, releaseRadius);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => targets.Clear();
    private void OnEnable() => targets.Add(this);
    private void OnDisable() => targets.Remove(this);

    private void OnDrawGizmosSelected()
    {
        // Inner cyan sphere: press the button here. Outer orange sphere: release boundary.
        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.7f);
        Gizmos.DrawWireSphere(AreaCenter, Mathf.Max(0.1f, lockOnRadius));
        Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(AreaCenter, Mathf.Max(lockOnRadius, releaseRadius));
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(AimPosition, 0.15f);
        Gizmos.DrawLine(transform.position, AimPosition);
    }
}
