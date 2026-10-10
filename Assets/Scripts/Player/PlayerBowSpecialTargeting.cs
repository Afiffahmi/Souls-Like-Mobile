using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class PlayerBowShooter
{
    readonly Dictionary<Enemy, int> specialTargetUses = new Dictionary<Enemy, int>();
    readonly List<GameObject> specialRoots = new List<GameObject>();
    readonly List<Enemy> specialEnemies = new List<Enemy>();
    readonly List<Collider> specialTargetColliders = new List<Collider>();
    RaycastHit[] specialSightHits = new RaycastHit[16];
    int specialTargetSequence = -1;

    // Choose at release so dead/out-of-range targets are replaced. Prefer enemies that
    // have received fewer shots in this special; distance breaks ties deterministically.
    Enemy SelectSpecialLowTarget(Vector3 start, ReleaseRequest request, out Vector3 aim)
    {
        if (specialTargetSequence != request.specialSequence)
        {
            specialTargetUses.Clear();
            specialTargetSequence = request.specialSequence;
        }
        aim = default;
        Enemy selected = null;
        int leastUses = int.MaxValue;
        float nearest = float.PositiveInfinity;
        float rangeSquared = request.specialSettings.range * request.specialSettings.range;
        specialRoots.Clear();
        gameObject.scene.GetRootGameObjects(specialRoots);
        foreach (var root in specialRoots)
        {
            specialEnemies.Clear();
            root.GetComponentsInChildren(false, specialEnemies);
            foreach (var enemy in specialEnemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDead || enemy.transform.IsChildOf(transform)) continue;
                float distance = (enemy.transform.position - transform.position).sqrMagnitude;
                if (distance > rangeSquared) continue;
                specialTargetUses.TryGetValue(enemy, out int uses);
                if (uses > leastUses || (uses == leastUses && distance >= nearest)) continue;
                if (!TryGetSpecialEnemyAim(enemy, out var candidateAim) || !SpecialTargetVisible(start, candidateAim)) continue;
                selected = enemy; aim = candidateAim; leastUses = uses; nearest = distance;
            }
        }
        if (selected != null) specialTargetUses[selected] = leastUses + 1;
        return selected;
    }

    bool TryGetSpecialEnemyAim(Enemy enemy, out Vector3 aim)
    {
        aim = default;
        var target = enemy.GetComponentInChildren<LockOnTarget>();
        if (target != null && !target.IsAvailable) return false;
        specialTargetColliders.Clear();
        enemy.GetComponentsInChildren(false, specialTargetColliders);
        Collider body = null;
        foreach (var collider in specialTargetColliders)
        {
            if (!collider.enabled || collider.isTrigger || (hitLayers.value & (1 << collider.gameObject.layer)) == 0 ||
                collider.GetComponentInParent<Enemy>() != enemy) continue;
            if (body == null || collider.bounds.size.sqrMagnitude > body.bounds.size.sqrMagnitude) body = collider;
        }
        if (body == null) return false;
        // A physical arrow needs to aim inside a hittable body, even when a marker is above its head.
        aim = target != null ? body.ClosestPoint(target.AimPosition) : body.bounds.center;
        aim = Vector3.Lerp(aim, body.bounds.center, .1f);
        return true;
    }

    bool SpecialTargetVisible(Vector3 start, Vector3 aim)
    {
        Vector3 offset = aim - start;
        if (offset.sqrMagnitude < .0001f) return true;
        var physics = gameObject.scene.GetPhysicsScene();
        int count;
        while ((count = physics.Raycast(start, offset.normalized, specialSightHits, offset.magnitude, hitLayers,
            QueryTriggerInteraction.Ignore)) == specialSightHits.Length)
            System.Array.Resize(ref specialSightHits, specialSightHits.Length * 2);
        for (int i = 0; i < count; i++)
        {
            var collider = specialSightHits[i].collider;
            if (!collider.transform.IsChildOf(transform) && !BowArrowProjectile.IsCharacterCollider(collider)) return false;
        }
        return true;
    }

    // Set the body yaw on the release frame before sampling the socket again.
    // Lock-on retains ownership of facing while active.
    void FaceUnlockedSpecialTarget(Vector3 aim)
    {
        if (lockOn != null && lockOn.isActiveAndEnabled && lockOn.IsLockedOn) return;
        Vector3 facing = Vector3.ProjectOnPlane(aim - player.transform.position, Vector3.up);
        if (facing.sqrMagnitude > .0001f)
            player.transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
    }

    Vector3 GetSpecialHighDirection(Vector3 start)
    {
        // An unlocked special still releases its final wave along the player's current facing.
        // Never run the low-shot auto-target selector for the high shot.
        Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .0001f) forward = Vector3.forward;
        return TryGetSpecialLockedDirection(start, out var lockedDirection) ? lockedDirection : forward;
    }

    bool TryGetSpecialLockedDirection(Vector3 start, out Vector3 direction)
    {
        direction = default;
        if (lockOn == null || !lockOn.isActiveAndEnabled || !lockOn.IsLockedOn) return false;
        var target = lockOn.CurrentTarget;
        if (target == null || !target.IsAvailable || target.gameObject.scene != gameObject.scene ||
            target.transform.IsChildOf(transform)) return false;
        var enemy = target.GetComponentInParent<Enemy>();
        if (enemy != null && (enemy.IsDead || !enemy.isActiveAndEnabled)) return false;
        // The area wave travels horizontally, aimed precisely from its actual release origin.
        direction = Vector3.ProjectOnPlane(target.AimPosition - start, Vector3.up).normalized;
        return direction.sqrMagnitude > .0001f;
    }
}
